using Microsoft.EntityFrameworkCore;
using Vectrik.Data;

namespace Vectrik.Services;

public class SetupReadinessService
{
    public async Task<List<ReadinessCheck>> GetChecksAsync(TenantDbContext db)
    {
        var checks = new List<ReadinessCheck>();

        // 1. Team: more than 1 active user
        var userCount = await db.Users.CountAsync(u => u.IsActive);
        checks.Add(new ReadinessCheck
        {
            Label = "Team Members",
            Description = userCount > 1 ? $"{userCount} active users" : "Only the default admin — add your team",
            Passed = userCount > 1,
            Link = "/admin/users",
            Icon = "person"
        });

        // 2. Shifts: at least one machine-shift assignment
        var hasShiftAssignments = await db.MachineShiftAssignments.AnyAsync();
        var shiftCount = await db.OperatingShifts.CountAsync(s => s.IsActive);
        checks.Add(new ReadinessCheck
        {
            Label = "Shift Schedules",
            Description = hasShiftAssignments ? $"{shiftCount} shifts with machine assignments" : "No machines assigned to shifts",
            Passed = hasShiftAssignments,
            Link = "/admin/shifts",
            Icon = "clock"
        });

        // 3. Machines: at least one active
        var machineCount = await db.Machines.CountAsync(m => m.IsActive);
        checks.Add(new ReadinessCheck
        {
            Label = "Machines",
            Description = machineCount > 0 ? $"{machineCount} active machines" : "No machines configured",
            Passed = machineCount > 0,
            Link = "/machines",
            Icon = "machine"
        });

        // 4. Materials
        var materialCount = await db.Materials.CountAsync();
        checks.Add(new ReadinessCheck
        {
            Label = "Materials",
            Description = materialCount > 0 ? $"{materialCount} materials defined" : "No materials configured",
            Passed = materialCount > 0,
            Link = "/admin/materials",
            Icon = "material"
        });

        // 5. Manufacturing Approaches
        var approachCount = await db.ManufacturingApproaches.CountAsync(a => a.IsActive);
        checks.Add(new ReadinessCheck
        {
            Label = "Manufacturing Approaches",
            Description = approachCount > 0 ? $"{approachCount} active approaches" : "No manufacturing approaches defined",
            Passed = approachCount > 0,
            Link = "/admin/manufacturing-approaches",
            Icon = "route"
        });

        // 6. Operation Costs
        var costCount = await db.StageCostProfiles.CountAsync();
        var stageCount = await db.ProductionStages.CountAsync(s => s.IsActive);
        checks.Add(new ReadinessCheck
        {
            Label = "Operation Costs",
            Description = costCount > 0 ? $"{costCount}/{stageCount} stages costed" : "No stage costs configured — jobs will show $0",
            Passed = costCount > 0,
            Link = "/admin/operation-costs",
            Icon = "cost"
        });

        // 7. Numbering
        var hasNumbering = await db.SystemSettings.AnyAsync(s => s.Category == "Numbering");
        checks.Add(new ReadinessCheck
        {
            Label = "Numbering Sequences",
            Description = hasNumbering ? "Number formats configured" : "Using default numbering — customize for your shop",
            Passed = hasNumbering,
            Link = "/admin/numbering",
            Icon = "number"
        });

        // 8. Branding
        var companyName = await db.SystemSettings
            .Where(s => s.Key == "company.name")
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
        var hasBranding = !string.IsNullOrWhiteSpace(companyName) && companyName != "Your Company";
        checks.Add(new ReadinessCheck
        {
            Label = "Branding",
            Description = hasBranding ? $"Company: {companyName}" : "Company name and branding not set",
            Passed = hasBranding,
            Link = "/admin/branding",
            Icon = "brand"
        });

        return checks;
    }

    public int CalculateScore(List<ReadinessCheck> checks)
    {
        if (checks.Count == 0) return 0;
        return (int)Math.Round(100.0 * checks.Count(c => c.Passed) / checks.Count);
    }
}

public class ReadinessCheck
{
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Passed { get; set; }
    public string Link { get; set; } = "";
    public string Icon { get; set; } = "";
}
