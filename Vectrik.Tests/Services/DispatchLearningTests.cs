using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services;
using Vectrik.Tests.Helpers;

namespace Vectrik.Tests.Services;

public class DispatchLearningServiceTests
{
    private static (TenantDbContext db, DispatchLearningService svc) CreateLearningService()
    {
        var db = TestDbContextFactory.Create();
        var svc = new DispatchLearningService(db);
        return (db, svc);
    }

    [Fact]
    public async Task ProcessCompleted_CreatesOperatorProfile()
    {
        var (db, svc) = CreateLearningService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);
        var user = new User { Email = "operator@test.com", FullName = "Operator", PasswordHash = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Create setup history directly
        var history = new SetupHistory
        {
            SetupDispatchId = 999,
            MachineId = machine.Id,
            MachineProgramId = program.Id,
            OperatorUserId = user.Id,
            SetupDurationMinutes = 45,
            CompletedAt = DateTime.UtcNow
        };
        db.SetupHistories.Add(history);
        await db.SaveChangesAsync();

        // Simulate a dispatch with matching ID
        var dispatch = new SetupDispatch
        {
            Id = 999, DispatchNumber = "DSP-TEST", MachineId = machine.Id,
            MachineProgramId = program.Id, Status = DispatchStatus.Completed
        };
        db.SetupDispatches.Add(dispatch);
        await db.SaveChangesAsync();

        await svc.ProcessCompletedDispatchAsync(999);

        // Check machine-level profile was created
        var profile = await db.OperatorSetupProfiles
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.MachineId == machine.Id && p.MachineProgramId == null);
        Assert.NotNull(profile);
        Assert.Equal(45, profile!.AverageSetupMinutes);
        Assert.Equal(1, profile.SampleCount);
        Assert.Equal(45, profile.FastestSetupMinutes);
    }

    [Fact]
    public async Task ProcessCompleted_UpdatesExistingProfile_WithEma()
    {
        var (db, svc) = CreateLearningService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);
        var user = new User { Email = "op@test.com", FullName = "Op", PasswordHash = "x" };
        db.Users.Add(user);

        // Seed existing profile
        var existingProfile = new OperatorSetupProfile
        {
            UserId = user.Id, MachineId = machine.Id,
            AverageSetupMinutes = 50, SampleCount = 5, VarianceMinutes = 10,
            FastestSetupMinutes = 40, ProficiencyLevel = 3
        };
        db.OperatorSetupProfiles.Add(existingProfile);
        await db.SaveChangesAsync();

        var history = new SetupHistory
        {
            SetupDispatchId = 1000, MachineId = machine.Id, MachineProgramId = program.Id,
            OperatorUserId = user.Id, SetupDurationMinutes = 30, CompletedAt = DateTime.UtcNow
        };
        db.SetupHistories.Add(history);
        var dispatch = new SetupDispatch
        {
            Id = 1000, DispatchNumber = "DSP-T2", MachineId = machine.Id,
            MachineProgramId = program.Id, Status = DispatchStatus.Completed
        };
        db.SetupDispatches.Add(dispatch);
        await db.SaveChangesAsync();

        await svc.ProcessCompletedDispatchAsync(1000);

        var profile = await db.OperatorSetupProfiles
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.MachineId == machine.Id && p.MachineProgramId == null);

        Assert.NotNull(profile);
        Assert.Equal(6, profile!.SampleCount);
        // EMA: 0.3 * 30 + 0.7 * 50 = 9 + 35 = 44
        Assert.InRange(profile.AverageSetupMinutes!.Value, 43.5, 44.5);
        Assert.Equal(30, profile.FastestSetupMinutes); // New fastest
    }

    [Fact]
    public async Task ProcessCompleted_UpdatesProgramSetupEma()
    {
        var (db, svc) = CreateLearningService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);
        program.ActualAverageSetupMinutes = 60;
        program.SetupSampleCount = 3;
        await db.SaveChangesAsync();

        var user = new User { Email = "op@test.com", FullName = "Op", PasswordHash = "x" };
        db.Users.Add(user);

        var history = new SetupHistory
        {
            SetupDispatchId = 1001, MachineId = machine.Id, MachineProgramId = program.Id,
            OperatorUserId = user.Id, SetupDurationMinutes = 40, CompletedAt = DateTime.UtcNow
        };
        db.SetupHistories.Add(history);
        var dispatch = new SetupDispatch
        {
            Id = 1001, DispatchNumber = "DSP-T3", MachineId = machine.Id,
            MachineProgramId = program.Id, Status = DispatchStatus.Completed
        };
        db.SetupDispatches.Add(dispatch);
        await db.SaveChangesAsync();

        await svc.ProcessCompletedDispatchAsync(1001);

        var updated = await db.MachinePrograms.FindAsync(program.Id);
        // EMA: 0.3 * 40 + 0.7 * 60 = 12 + 42 = 54
        Assert.InRange(updated!.ActualAverageSetupMinutes!.Value, 53.5, 54.5);
        Assert.Equal(4, updated.SetupSampleCount);
    }

    [Fact]
    public async Task RecalculateProficiency_SetsCorrectLevels()
    {
        var (db, svc) = CreateLearningService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        // Create operators with varying setup times
        // Median will be around 50 (three profiles: 30, 50, 80)
        var users = new[] {
            new User { Email = "expert@test.com", FullName = "Expert", PasswordHash = "x" },
            new User { Email = "mid@test.com", FullName = "Mid", PasswordHash = "x" },
            new User { Email = "slow@test.com", FullName = "Slow", PasswordHash = "x" }
        };
        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        db.OperatorSetupProfiles.AddRange(
            new OperatorSetupProfile { UserId = users[0].Id, MachineId = machine.Id, AverageSetupMinutes = 30, SampleCount = 5 },
            new OperatorSetupProfile { UserId = users[1].Id, MachineId = machine.Id, AverageSetupMinutes = 50, SampleCount = 5 },
            new OperatorSetupProfile { UserId = users[2].Id, MachineId = machine.Id, AverageSetupMinutes = 80, SampleCount = 5 }
        );
        await db.SaveChangesAsync();

        await svc.RecalculateProficiencyLevelsAsync(machine.Id);

        var profiles = await db.OperatorSetupProfiles
            .Where(p => p.MachineId == machine.Id)
            .OrderBy(p => p.AverageSetupMinutes)
            .ToListAsync();

        // Median = 50. Expert at 30 (60% of median) → level 5
        Assert.Equal(5, profiles[0].ProficiencyLevel); // 30/50 = 0.60 → Expert
        Assert.Equal(3, profiles[1].ProficiencyLevel); // 50/50 = 1.00 → Competent
        Assert.Equal(1, profiles[2].ProficiencyLevel); // 80/50 = 1.60 → Novice
    }

    [Fact]
    public async Task RecalculateProficiency_SkipsWithFewerThan3Samples()
    {
        var (db, svc) = CreateLearningService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var user = new User { Email = "new@test.com", FullName = "New", PasswordHash = "x" };
        db.Users.Add(user);

        db.OperatorSetupProfiles.Add(new OperatorSetupProfile
        {
            UserId = user.Id, MachineId = machine.Id, AverageSetupMinutes = 30,
            SampleCount = 2, ProficiencyLevel = 1
        });
        await db.SaveChangesAsync();

        await svc.RecalculateProficiencyLevelsAsync(machine.Id);

        var profile = await db.OperatorSetupProfiles.FirstAsync(p => p.UserId == user.Id);
        Assert.Equal(1, profile.ProficiencyLevel); // Unchanged — not enough samples
    }

    [Fact]
    public async Task SuggestBestOperator_ReturnsHighestProficiency()
    {
        var (db, svc) = CreateLearningService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        var expert = new User { Email = "expert@test.com", FullName = "Expert", PasswordHash = "x" };
        var novice = new User { Email = "novice@test.com", FullName = "Novice", PasswordHash = "x" };
        db.Users.AddRange(expert, novice);
        await db.SaveChangesAsync();

        db.OperatorSetupProfiles.AddRange(
            new OperatorSetupProfile { UserId = expert.Id, MachineId = machine.Id, AverageSetupMinutes = 25, SampleCount = 10, ProficiencyLevel = 5 },
            new OperatorSetupProfile { UserId = novice.Id, MachineId = machine.Id, AverageSetupMinutes = 60, SampleCount = 10, ProficiencyLevel = 1 }
        );
        await db.SaveChangesAsync();

        var suggested = await svc.SuggestBestOperatorAsync(machine.Id);

        Assert.Equal(expert.Id, suggested);
    }

    [Fact]
    public async Task SuggestBestOperator_PrefersPreferredFlag()
    {
        var (db, svc) = CreateLearningService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        var preferred = new User { Email = "pref@test.com", FullName = "Pref", PasswordHash = "x" };
        var expert = new User { Email = "exp@test.com", FullName = "Exp", PasswordHash = "x" };
        db.Users.AddRange(preferred, expert);
        await db.SaveChangesAsync();

        db.OperatorSetupProfiles.AddRange(
            new OperatorSetupProfile { UserId = preferred.Id, MachineId = machine.Id, AverageSetupMinutes = 40, SampleCount = 5, ProficiencyLevel = 3, IsPreferred = true },
            new OperatorSetupProfile { UserId = expert.Id, MachineId = machine.Id, AverageSetupMinutes = 25, SampleCount = 10, ProficiencyLevel = 5 }
        );
        await db.SaveChangesAsync();

        var suggested = await svc.SuggestBestOperatorAsync(machine.Id);

        Assert.Equal(preferred.Id, suggested); // Preferred beats higher proficiency
    }

    [Fact]
    public async Task SuggestBestOperator_ReturnsNull_WhenNoProfiles()
    {
        var (db, svc) = CreateLearningService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        var suggested = await svc.SuggestBestOperatorAsync(machine.Id);

        Assert.Null(suggested);
    }

    [Fact]
    public async Task GetMachineProfiles_ReturnsOrderedByProficiency()
    {
        var (db, svc) = CreateLearningService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        var users = new[] {
            new User { Email = "a@test.com", FullName = "A", PasswordHash = "x" },
            new User { Email = "b@test.com", FullName = "B", PasswordHash = "x" }
        };
        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        db.OperatorSetupProfiles.AddRange(
            new OperatorSetupProfile { UserId = users[0].Id, MachineId = machine.Id, ProficiencyLevel = 2, AverageSetupMinutes = 50, SampleCount = 3 },
            new OperatorSetupProfile { UserId = users[1].Id, MachineId = machine.Id, ProficiencyLevel = 5, AverageSetupMinutes = 25, SampleCount = 10 }
        );
        await db.SaveChangesAsync();

        var profiles = await svc.GetMachineProfilesAsync(machine.Id);

        Assert.Equal(2, profiles.Count);
        Assert.Equal(5, profiles[0].ProficiencyLevel); // Higher proficiency first
    }
}
