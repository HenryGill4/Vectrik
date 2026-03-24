using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public interface IQualityService
{
    // Inspection Plans
    Task<List<InspectionPlan>> GetPlansForPartAsync(int partId);
    Task<InspectionPlan?> GetPlanByIdAsync(int id);
    Task<InspectionPlan> CreatePlanAsync(InspectionPlan plan);
    Task UpdatePlanAsync(InspectionPlan plan);
    Task DeletePlanAsync(int id);

    // Inspections
    Task<QCInspection> CreateInspectionFromPlanAsync(int jobId, int planId, string inspectorUserId);
    Task<QCInspection> SaveInspectionAsync(QCInspection inspection);
    Task<List<QCInspection>> GetInspectionsForJobAsync(int jobId);
    Task<List<QCInspection>> GetRecentInspectionsAsync(int count = 20);
    Task<QCInspection?> GetInspectionByIdAsync(int id);

    // NCR
    Task<NonConformanceReport> CreateNcrAsync(NonConformanceReport ncr);
    Task UpdateNcrAsync(NonConformanceReport ncr);
    Task<List<NonConformanceReport>> GetOpenNcrsAsync();
    Task<List<NonConformanceReport>> GetAllNcrsAsync();
    Task<NonConformanceReport?> GetNcrByIdAsync(int id);

    // CAPA
    Task<CorrectiveAction> CreateCapaAsync(CorrectiveAction capa);
    Task UpdateCapaAsync(CorrectiveAction capa);
    Task<List<CorrectiveAction>> GetAllCapasAsync();
    Task<CorrectiveAction?> GetCapaByIdAsync(int id);

    // Dashboard KPIs
    Task<QualityDashboardData> GetDashboardDataAsync();
}

public record QualityDashboardData(
    int TotalInspections,
    int PassedInspections,
    decimal FirstPassYieldPercent,
    int OpenNcrCount,
    int OpenCapaCount,
    int PartsInInspection,
    List<NonConformanceReport> RecentNcrs,
    List<CorrectiveAction> OverduaCapas);
