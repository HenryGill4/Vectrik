using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Services.Platform;

namespace Opcentrix_V3.Services;

public class PartFileService : IPartFileService
{
    private readonly TenantDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IWebHostEnvironment _env;

    public PartFileService(TenantDbContext db, ITenantContext tenant, IWebHostEnvironment env)
    {
        _db = db;
        _tenant = tenant;
        _env = env;
    }

    public async Task<PartDrawing> UploadDrawingAsync(int partId, string fileName, Stream fileStream, long fileSize, string uploadedBy, string? description = null, string? revision = null)
    {
        var relativePath = await GetUploadPathAsync(partId, fileName);
        var fullPath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/'));

        var dir = Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using (var fs = new FileStream(fullPath, FileMode.Create))
        {
            await fileStream.CopyToAsync(fs);
        }

        var ext = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();

        var drawing = new PartDrawing
        {
            PartId = partId,
            FileName = fileName,
            FilePath = relativePath,
            FileType = ext,
            FileSizeBytes = fileSize,
            Description = description,
            Revision = revision,
            UploadedBy = uploadedBy,
            UploadedDate = DateTime.UtcNow
        };

        _db.PartDrawings.Add(drawing);
        await _db.SaveChangesAsync();
        return drawing;
    }

    public async Task<List<PartDrawing>> GetDrawingsAsync(int partId)
    {
        return await _db.PartDrawings
            .Where(d => d.PartId == partId)
            .OrderByDescending(d => d.IsPrimary)
            .ThenByDescending(d => d.UploadedDate)
            .ToListAsync();
    }

    public async Task<PartDrawing?> GetDrawingByIdAsync(int id)
    {
        return await _db.PartDrawings.FindAsync(id);
    }

    public async Task DeleteDrawingAsync(int id)
    {
        var drawing = await _db.PartDrawings.FindAsync(id);
        if (drawing == null) return;

        var fullPath = Path.Combine(_env.WebRootPath, drawing.FilePath.TrimStart('/'));
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        _db.PartDrawings.Remove(drawing);
        await _db.SaveChangesAsync();
    }

    public async Task SetPrimaryDrawingAsync(int partId, int drawingId)
    {
        var drawings = await _db.PartDrawings.Where(d => d.PartId == partId).ToListAsync();
        foreach (var d in drawings)
        {
            d.IsPrimary = d.Id == drawingId;
        }
        await _db.SaveChangesAsync();
    }

    public Task<string> GetUploadPathAsync(int partId, string fileName)
    {
        var safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Path.GetFileName(fileName)}";
        var path = $"/uploads/drawings/{_tenant.TenantCode}/{partId}/{safeName}";
        return Task.FromResult(path);
    }
}
