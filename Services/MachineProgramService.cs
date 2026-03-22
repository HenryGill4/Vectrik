using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Services.Platform;

namespace Opcentrix_V3.Services;

public class MachineProgramService : IMachineProgramService
{
    private readonly TenantDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IWebHostEnvironment _env;

    public MachineProgramService(TenantDbContext db, ITenantContext tenant, IWebHostEnvironment env)
    {
        _db = db;
        _tenant = tenant;
        _env = env;
    }

    // ── CRUD ─────────────────────────────────────────────────

    public async Task<MachineProgram?> GetByIdAsync(int id)
    {
        return await _db.MachinePrograms
            .Include(p => p.Part)
            .Include(p => p.Machine)
            .Include(p => p.ProcessStage)
                .ThenInclude(ps => ps!.ProductionStage)
            .Include(p => p.Files)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<MachineProgram>> GetAllAsync()
    {
        return await _db.MachinePrograms
            .Include(p => p.Part)
            .Include(p => p.Machine)
            .Include(p => p.Files)
            .OrderBy(p => p.ProgramNumber)
            .ToListAsync();
    }

    public async Task<List<MachineProgram>> GetProgramsForPartAsync(int partId)
    {
        return await _db.MachinePrograms
            .Include(p => p.Machine)
            .Include(p => p.ProcessStage)
                .ThenInclude(ps => ps!.ProductionStage)
            .Include(p => p.Files)
            .Where(p => p.PartId == partId)
            .OrderBy(p => p.ProgramNumber)
            .ToListAsync();
    }

    public async Task<List<MachineProgram>> GetProgramsForMachineAsync(int machineId)
    {
        return await _db.MachinePrograms
            .Include(p => p.Part)
            .Include(p => p.ProcessStage)
                .ThenInclude(ps => ps!.ProductionStage)
            .Include(p => p.Files)
            .Where(p => p.MachineId == machineId)
            .OrderBy(p => p.ProgramNumber)
            .ToListAsync();
    }

    public async Task<MachineProgram?> GetProgramForStageAsync(int processStageId)
    {
        return await _db.MachinePrograms
            .Include(p => p.Part)
            .Include(p => p.Machine)
            .Include(p => p.Files)
            .FirstOrDefaultAsync(p => p.ProcessStageId == processStageId && p.Status == ProgramStatus.Active);
    }

    public async Task<MachineProgram> CreateAsync(MachineProgram program, string createdBy)
    {
        ArgumentNullException.ThrowIfNull(program);

        program.CreatedBy = createdBy;
        program.LastModifiedBy = createdBy;
        program.CreatedDate = DateTime.UtcNow;
        program.LastModifiedDate = DateTime.UtcNow;
        program.Version = 1;

        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();
        return program;
    }

    public async Task UpdateAsync(MachineProgram program, string modifiedBy)
    {
        ArgumentNullException.ThrowIfNull(program);

        program.LastModifiedBy = modifiedBy;
        program.LastModifiedDate = DateTime.UtcNow;

        _db.MachinePrograms.Update(program);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var program = await _db.MachinePrograms
            .Include(p => p.Files)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (program == null) return;

        // Delete physical files
        foreach (var file in program.Files)
        {
            DeletePhysicalFile(file.FilePath);
        }

        _db.MachinePrograms.Remove(program);
        await _db.SaveChangesAsync();
    }

    // ── Versioning ───────────────────────────────────────────

    public async Task<MachineProgram> CreateNewVersionAsync(int programId, string createdBy)
    {
        var source = await _db.MachinePrograms
            .Include(p => p.Files)
            .FirstOrDefaultAsync(p => p.Id == programId)
            ?? throw new InvalidOperationException($"Program {programId} not found.");

        // Mark old version as superseded
        source.Status = ProgramStatus.Superseded;
        source.LastModifiedBy = createdBy;
        source.LastModifiedDate = DateTime.UtcNow;

        // Create new version
        var newVersion = new MachineProgram
        {
            PartId = source.PartId,
            MachineId = source.MachineId,
            MachineType = source.MachineType,
            ProcessStageId = source.ProcessStageId,
            ProgramNumber = source.ProgramNumber,
            Name = source.Name,
            Description = source.Description,
            Version = source.Version + 1,
            Status = ProgramStatus.Draft,
            SetupTimeMinutes = source.SetupTimeMinutes,
            RunTimeMinutes = source.RunTimeMinutes,
            CycleTimeMinutes = source.CycleTimeMinutes,
            ToolingRequired = source.ToolingRequired,
            FixtureRequired = source.FixtureRequired,
            Parameters = source.Parameters,
            Notes = source.Notes,
            IsActive = true,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };

        _db.MachinePrograms.Add(newVersion);
        await _db.SaveChangesAsync();
        return newVersion;
    }

    // ── File Management ──────────────────────────────────────

    public async Task<MachineProgramFile> UploadFileAsync(int programId, string fileName, Stream fileStream, long fileSize, string uploadedBy, string? description = null)
    {
        var relativePath = GetUploadPath(programId, fileName);
        var fullPath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/'));

        var dir = Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // Compute hash while writing
        string? hash;
        using (var fs = new FileStream(fullPath, FileMode.Create))
        {
            using var sha256 = SHA256.Create();
            using var hashStream = new CryptoStream(fs, sha256, CryptoStreamMode.Write);
            await fileStream.CopyToAsync(hashStream);
            await hashStream.FlushFinalBlockAsync();
            hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
        }

        var ext = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(ext)) ext = "Other";

        var file = new MachineProgramFile
        {
            MachineProgramId = programId,
            FileName = fileName,
            FilePath = relativePath,
            FileType = ext,
            FileSizeBytes = fileSize,
            FileHash = hash,
            Description = description,
            UploadedBy = uploadedBy,
            UploadedDate = DateTime.UtcNow
        };

        _db.MachineProgramFiles.Add(file);
        await _db.SaveChangesAsync();
        return file;
    }

    public async Task<List<MachineProgramFile>> GetFilesAsync(int programId)
    {
        return await _db.MachineProgramFiles
            .Where(f => f.MachineProgramId == programId)
            .OrderByDescending(f => f.IsPrimary)
            .ThenByDescending(f => f.UploadedDate)
            .ToListAsync();
    }

    public async Task<MachineProgramFile?> GetFileByIdAsync(int fileId)
    {
        return await _db.MachineProgramFiles.FindAsync(fileId);
    }

    public async Task DeleteFileAsync(int fileId)
    {
        var file = await _db.MachineProgramFiles.FindAsync(fileId);
        if (file == null) return;

        DeletePhysicalFile(file.FilePath);

        _db.MachineProgramFiles.Remove(file);
        await _db.SaveChangesAsync();
    }

    public async Task SetPrimaryFileAsync(int programId, int fileId)
    {
        var files = await _db.MachineProgramFiles.Where(f => f.MachineProgramId == programId).ToListAsync();
        foreach (var f in files)
        {
            f.IsPrimary = f.Id == fileId;
        }
        await _db.SaveChangesAsync();
    }

    // ── Cloning ──────────────────────────────────────────────

    public async Task<MachineProgram> CloneProgramAsync(int sourceProgramId, int? targetPartId, int? targetMachineId, string createdBy)
    {
        var source = await _db.MachinePrograms
            .FirstOrDefaultAsync(p => p.Id == sourceProgramId)
            ?? throw new InvalidOperationException($"Program {sourceProgramId} not found.");

        var clone = new MachineProgram
        {
            PartId = targetPartId ?? source.PartId,
            MachineId = targetMachineId ?? source.MachineId,
            MachineType = source.MachineType,
            ProcessStageId = null, // Must be manually re-linked
            ProgramNumber = $"{source.ProgramNumber}-COPY",
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            Version = 1,
            Status = ProgramStatus.Draft,
            SetupTimeMinutes = source.SetupTimeMinutes,
            RunTimeMinutes = source.RunTimeMinutes,
            CycleTimeMinutes = source.CycleTimeMinutes,
            ToolingRequired = source.ToolingRequired,
            FixtureRequired = source.FixtureRequired,
            Parameters = source.Parameters,
            Notes = source.Notes,
            IsActive = true,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };

        _db.MachinePrograms.Add(clone);
        await _db.SaveChangesAsync();
        return clone;
    }

    // ── Parameter Templates ──────────────────────────────────

    public string GetDefaultParametersForMachineType(string machineType)
    {
        return machineType.ToUpperInvariant() switch
        {
            "CNC" or "CNC MILL" or "CNC LATHE" => """
                {
                  "spindleSpeedRpm": 0,
                  "feedRateMmPerMin": 0,
                  "depthOfCutMm": 0,
                  "coolantType": "Flood",
                  "toolChangeCount": 0,
                  "materialRemovalRateCm3PerMin": 0
                }
                """,
            "EDM" or "WIRE EDM" => """
                {
                  "wireTypeMm": "Brass 0.25mm",
                  "gapVoltageMv": 0,
                  "dischargeCurrent": 0,
                  "flushPressureBar": 0,
                  "wireConsumedMeters": 0,
                  "cutSpeedMmPerMin": 0
                }
                """,
            "LASER" or "LASER ENGRAVING" or "LASER CUTTING" => """
                {
                  "laserPowerWatts": 0,
                  "scanSpeedMmPerSec": 0,
                  "pulseFrequencyHz": 0,
                  "focalLengthMm": 0,
                  "assistGas": "Nitrogen",
                  "passCount": 1
                }
                """,
            "SLS" or "ADDITIVE" or "3D PRINTING" => """
                {
                  "layerThicknessMm": 0.1,
                  "laserPowerWatts": 0,
                  "scanSpeedMmPerSec": 0,
                  "hatchSpacingMm": 0,
                  "buildChamberTempC": 0,
                  "printerModel": ""
                }
                """,
            "TURNING" or "CNC TURNING" => """
                {
                  "spindleSpeedRpm": 0,
                  "feedRateMmPerRev": 0,
                  "depthOfCutMm": 0,
                  "toolNoseRadiusMm": 0,
                  "coolantType": "Flood",
                  "chuckType": "3-Jaw"
                }
                """,
            _ => """
                {
                  "param1": "",
                  "param2": "",
                  "notes": ""
                }
                """
        };
    }

    // ── Helpers ───────────────────────────────────────────────

    private string GetUploadPath(int programId, string fileName)
    {
        var safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Path.GetFileName(fileName)}";
        return $"/uploads/programs/{_tenant.TenantCode}/{programId}/{safeName}";
    }

    private void DeletePhysicalFile(string relativePath)
    {
        var fullPath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/'));
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
