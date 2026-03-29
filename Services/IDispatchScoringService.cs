using Vectrik.Models;

namespace Vectrik.Services;

public interface IDispatchScoringService
{
    /// <summary>Scores a single dispatch based on due date, changeover, throughput, and maintenance.</summary>
    Task<DispatchScore> ScoreDispatchAsync(SetupDispatch dispatch);

    /// <summary>Scores and ranks a list of dispatches in descending order.</summary>
    Task<List<(SetupDispatch Dispatch, DispatchScore Score)>> ScoreAndRankAsync(List<SetupDispatch> dispatches);
}

public record DispatchScore(
    int FinalScore,
    int DueDateScore,
    int ChangeoverScore,
    int ThroughputScore,
    int MaintenanceModifier,
    decimal DueDateWeight,
    decimal ChangeoverWeight,
    decimal ThroughputWeight,
    string PriorityReason,
    string ScoreBreakdownJson);
