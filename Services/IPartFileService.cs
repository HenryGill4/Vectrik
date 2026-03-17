using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IPartFileService
{
    Task<PartDrawing> UploadDrawingAsync(int partId, string fileName, Stream fileStream, long fileSize, string uploadedBy, string? description = null, string? revision = null);
    Task<List<PartDrawing>> GetDrawingsAsync(int partId);
    Task<PartDrawing?> GetDrawingByIdAsync(int id);
    Task DeleteDrawingAsync(int id);
    Task SetPrimaryDrawingAsync(int partId, int drawingId);
    Task<string> GetUploadPathAsync(int partId, string fileName);
}
