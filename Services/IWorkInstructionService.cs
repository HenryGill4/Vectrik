using Microsoft.AspNetCore.Components.Forms;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public interface IWorkInstructionService
{
    Task<List<WorkInstruction>> GetAllAsync();
    Task<WorkInstruction?> GetByIdAsync(int id);
    Task<WorkInstruction?> GetByPartAndStageAsync(int partId, int stageId);
    Task<WorkInstruction> CreateAsync(WorkInstruction instruction);
    Task UpdateAsync(WorkInstruction instruction, string? changeNotes = null);
    Task DeleteAsync(int id);

    // Steps
    Task<WorkInstructionStep> AddStepAsync(int instructionId, WorkInstructionStep step);
    Task UpdateStepAsync(WorkInstructionStep step);
    Task DeleteStepAsync(int stepId);
    Task ReorderStepsAsync(int instructionId, List<int> orderedStepIds);

    // Media
    Task<string> UploadMediaAsync(int stepId, IBrowserFile file, string tenantCode);
    Task DeleteMediaAsync(int mediaId);

    // Feedback
    Task SubmitFeedbackAsync(OperatorFeedback feedback);
    Task<List<OperatorFeedback>> GetPendingFeedbackAsync();
    Task UpdateFeedbackStatusAsync(int feedbackId, FeedbackStatus status);

    // Revisions
    Task<List<WorkInstructionRevision>> GetRevisionsAsync(int instructionId);
}
