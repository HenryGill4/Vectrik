using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class WorkInstructionService : IWorkInstructionService
{
    private readonly TenantDbContext _db;
    private readonly IWebHostEnvironment _env;

    private static readonly HashSet<string> AllowedImageExts = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly HashSet<string> AllowedVideoExts = [".mp4", ".webm"];
    private static readonly HashSet<string> AllowedDocExts = [".pdf"];
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB

    public WorkInstructionService(TenantDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // ── Instructions CRUD ──

    public async Task<List<WorkInstruction>> GetAllAsync() =>
        await _db.WorkInstructions
            .Include(w => w.Part)
            .Include(w => w.ProductionStage)
            .Include(w => w.Steps.OrderBy(s => s.StepOrder))
            .OrderBy(w => w.Part.PartNumber)
            .ThenBy(w => w.ProductionStage.Name)
            .ToListAsync();

    public async Task<WorkInstruction?> GetByIdAsync(int id) =>
        await _db.WorkInstructions
            .Include(w => w.Part)
            .Include(w => w.ProductionStage)
            .Include(w => w.Steps.OrderBy(s => s.StepOrder))
                .ThenInclude(s => s.Media.OrderBy(m => m.DisplayOrder))
            .Include(w => w.Steps)
                .ThenInclude(s => s.Feedback)
            .FirstOrDefaultAsync(w => w.Id == id);

    public async Task<WorkInstruction?> GetByPartAndStageAsync(int partId, int stageId) =>
        await _db.WorkInstructions
            .Include(w => w.Steps.OrderBy(s => s.StepOrder))
                .ThenInclude(s => s.Media.OrderBy(m => m.DisplayOrder))
            .FirstOrDefaultAsync(w => w.PartId == partId && w.ProductionStageId == stageId && w.IsActive);

    public async Task<WorkInstruction> CreateAsync(WorkInstruction instruction)
    {
        instruction.CreatedAt = DateTime.UtcNow;
        instruction.UpdatedAt = DateTime.UtcNow;
        instruction.RevisionNumber = 1;

        _db.WorkInstructions.Add(instruction);
        await _db.SaveChangesAsync();
        return instruction;
    }

    public async Task UpdateAsync(WorkInstruction instruction, string? changeNotes = null)
    {
        var existing = await _db.WorkInstructions
            .Include(w => w.Steps)
                .ThenInclude(s => s.Media)
            .FirstOrDefaultAsync(w => w.Id == instruction.Id);

        if (existing == null)
            throw new InvalidOperationException($"Work instruction {instruction.Id} not found.");

        // Snapshot current state as revision before applying changes
        var snapshot = JsonSerializer.Serialize(existing.Steps.Select(s => new
        {
            s.Id,
            s.StepOrder,
            s.Title,
            s.Body,
            s.WarningText,
            s.TipText,
            s.RequiresOperatorSignoff,
            Media = s.Media.Select(m => new { m.FileName, m.FileUrl, m.MediaType }).ToList()
        }));

        var revision = new WorkInstructionRevision
        {
            WorkInstructionId = existing.Id,
            RevisionNumber = existing.RevisionNumber,
            SnapshotJson = snapshot,
            ChangeNotes = changeNotes,
            CreatedByUserId = instruction.CreatedByUserId,
            CreatedAt = DateTime.UtcNow
        };
        _db.WorkInstructionRevisions.Add(revision);

        // Apply updates
        existing.Title = instruction.Title;
        existing.Description = instruction.Description;
        existing.IsActive = instruction.IsActive;
        existing.RevisionNumber++;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var instruction = await _db.WorkInstructions.FindAsync(id);
        if (instruction != null)
        {
            _db.WorkInstructions.Remove(instruction);
            await _db.SaveChangesAsync();
        }
    }

    // ── Steps ──

    public async Task<WorkInstructionStep> AddStepAsync(int instructionId, WorkInstructionStep step)
    {
        var maxOrder = await _db.WorkInstructionSteps
            .Where(s => s.WorkInstructionId == instructionId)
            .MaxAsync(s => (int?)s.StepOrder) ?? 0;

        step.WorkInstructionId = instructionId;
        step.StepOrder = maxOrder + 1;

        _db.WorkInstructionSteps.Add(step);
        await _db.SaveChangesAsync();
        return step;
    }

    public async Task UpdateStepAsync(WorkInstructionStep step)
    {
        var existing = await _db.WorkInstructionSteps.FindAsync(step.Id);
        if (existing == null)
            throw new InvalidOperationException($"Step {step.Id} not found.");

        existing.Title = step.Title;
        existing.Body = step.Body;
        existing.WarningText = step.WarningText;
        existing.TipText = step.TipText;
        existing.RequiresOperatorSignoff = step.RequiresOperatorSignoff;

        await _db.SaveChangesAsync();
    }

    public async Task DeleteStepAsync(int stepId)
    {
        var step = await _db.WorkInstructionSteps.FindAsync(stepId);
        if (step != null)
        {
            _db.WorkInstructionSteps.Remove(step);
            await _db.SaveChangesAsync();

            // Re-order remaining steps
            var remaining = await _db.WorkInstructionSteps
                .Where(s => s.WorkInstructionId == step.WorkInstructionId)
                .OrderBy(s => s.StepOrder)
                .ToListAsync();

            for (int i = 0; i < remaining.Count; i++)
                remaining[i].StepOrder = i + 1;

            await _db.SaveChangesAsync();
        }
    }

    public async Task ReorderStepsAsync(int instructionId, List<int> orderedStepIds)
    {
        var steps = await _db.WorkInstructionSteps
            .Where(s => s.WorkInstructionId == instructionId)
            .ToListAsync();

        for (int i = 0; i < orderedStepIds.Count; i++)
        {
            var step = steps.FirstOrDefault(s => s.Id == orderedStepIds[i]);
            if (step != null)
                step.StepOrder = i + 1;
        }

        await _db.SaveChangesAsync();
    }

    // ── Media ──

    public async Task<string> UploadMediaAsync(int stepId, IBrowserFile file, string tenantCode)
    {
        var step = await _db.WorkInstructionSteps.FindAsync(stepId)
            ?? throw new InvalidOperationException($"Step {stepId} not found.");

        if (file.Size > MaxFileSizeBytes)
            throw new InvalidOperationException($"File exceeds maximum size of {MaxFileSizeBytes / (1024 * 1024)}MB.");

        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        var mediaType = GetMediaType(ext);

        // Save file
        var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "instructions", tenantCode);
        Directory.CreateDirectory(uploadDir);

        var safeFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadDir, safeFileName);
        var relativeUrl = $"/uploads/instructions/{tenantCode}/{safeFileName}";

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.OpenReadStream(MaxFileSizeBytes).CopyToAsync(stream);

        var maxOrder = await _db.WorkInstructionMedia
            .Where(m => m.WorkInstructionStepId == stepId)
            .MaxAsync(m => (int?)m.DisplayOrder) ?? 0;

        var media = new WorkInstructionMedia
        {
            WorkInstructionStepId = stepId,
            MediaType = mediaType,
            FileName = file.Name,
            FileUrl = relativeUrl,
            DisplayOrder = maxOrder + 1,
            FileSizeBytes = file.Size,
            UploadedAt = DateTime.UtcNow
        };

        _db.WorkInstructionMedia.Add(media);
        await _db.SaveChangesAsync();

        return relativeUrl;
    }

    public async Task<string> UploadMediaFromStreamAsync(int stepId, string fileName, string contentType, Stream dataStream, long fileSize, string tenantCode)
    {
        var step = await _db.WorkInstructionSteps.FindAsync(stepId)
            ?? throw new InvalidOperationException($"Step {stepId} not found.");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var mediaType = GetMediaType(ext);

        var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "instructions", tenantCode);
        Directory.CreateDirectory(uploadDir);

        var safeFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadDir, safeFileName);
        var relativeUrl = $"/uploads/instructions/{tenantCode}/{safeFileName}";

        await using var stream = new FileStream(filePath, FileMode.Create);
        await dataStream.CopyToAsync(stream);

        var maxOrder = await _db.WorkInstructionMedia
            .Where(m => m.WorkInstructionStepId == stepId)
            .MaxAsync(m => (int?)m.DisplayOrder) ?? 0;

        var media = new WorkInstructionMedia
        {
            WorkInstructionStepId = stepId,
            MediaType = mediaType,
            FileName = fileName,
            FileUrl = relativeUrl,
            DisplayOrder = maxOrder + 1,
            FileSizeBytes = fileSize,
            UploadedAt = DateTime.UtcNow
        };

        _db.WorkInstructionMedia.Add(media);
        await _db.SaveChangesAsync();
        return relativeUrl;
    }

    public async Task<string> UploadMediaFromBytesAsync(int stepId, string fileName, string contentType, byte[] data, string tenantCode)
    {
        using var ms = new MemoryStream(data);
        return await UploadMediaFromStreamAsync(stepId, fileName, contentType, ms, data.Length, tenantCode);
    }

    public async Task DeleteMediaAsync(int mediaId)
    {
        var media = await _db.WorkInstructionMedia.FindAsync(mediaId);
        if (media != null)
        {
            // Delete physical file
            var fullPath = Path.Combine(_env.WebRootPath, media.FileUrl.TrimStart('/'));
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            _db.WorkInstructionMedia.Remove(media);
            await _db.SaveChangesAsync();
        }
    }

    // ── Feedback ──

    public async Task SubmitFeedbackAsync(OperatorFeedback feedback)
    {
        feedback.SubmittedAt = DateTime.UtcNow;
        feedback.Status = FeedbackStatus.New;

        _db.OperatorFeedback.Add(feedback);
        await _db.SaveChangesAsync();
    }

    public async Task<List<OperatorFeedback>> GetPendingFeedbackAsync() =>
        await _db.OperatorFeedback
            .Include(f => f.Step)
                .ThenInclude(s => s.WorkInstruction)
                    .ThenInclude(w => w.Part)
            .Include(f => f.Step)
                .ThenInclude(s => s.WorkInstruction)
                    .ThenInclude(w => w.ProductionStage)
            .Where(f => f.Status == FeedbackStatus.New || f.Status == FeedbackStatus.Acknowledged)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync();

    public async Task UpdateFeedbackStatusAsync(int feedbackId, FeedbackStatus status)
    {
        var feedback = await _db.OperatorFeedback.FindAsync(feedbackId);
        if (feedback != null)
        {
            feedback.Status = status;
            if (status == FeedbackStatus.Resolved || status == FeedbackStatus.WontFix)
                feedback.ReviewedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    // ── Revisions ──

    public async Task<List<WorkInstructionRevision>> GetRevisionsAsync(int instructionId) =>
        await _db.WorkInstructionRevisions
            .Where(r => r.WorkInstructionId == instructionId)
            .OrderByDescending(r => r.RevisionNumber)
            .ToListAsync();

    // ── Helpers ──

    private static MediaType GetMediaType(string ext)
    {
        if (AllowedImageExts.Contains(ext)) return MediaType.Image;
        if (AllowedVideoExts.Contains(ext)) return MediaType.Video;
        if (AllowedDocExts.Contains(ext)) return MediaType.PDF;
        throw new InvalidOperationException($"File type '{ext}' is not allowed. Supported: images (jpg/png/gif/webp), video (mp4/webm), PDF.");
    }
}
