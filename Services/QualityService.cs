using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class QualityService : IQualityService
{
    private readonly TenantDbContext _db;
    private readonly INumberSequenceService _numberSeq;

    public QualityService(TenantDbContext db, INumberSequenceService numberSeq)
    {
        _db = db;
        _numberSeq = numberSeq;
    }

    // ── Inspection Plans ──

    public async Task<List<InspectionPlan>> GetPlansForPartAsync(int partId) =>
        await _db.InspectionPlans
            .Include(p => p.Characteristics.OrderBy(c => c.DisplayOrder))
            .Where(p => p.PartId == partId)
            .OrderByDescending(p => p.IsDefault)
            .ToListAsync();

    public async Task<InspectionPlan?> GetPlanByIdAsync(int id) =>
        await _db.InspectionPlans
            .Include(p => p.Characteristics.OrderBy(c => c.DisplayOrder))
            .Include(p => p.Part)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<InspectionPlan> CreatePlanAsync(InspectionPlan plan)
    {
        _db.InspectionPlans.Add(plan);
        await _db.SaveChangesAsync();
        return plan;
    }

    public async Task UpdatePlanAsync(InspectionPlan plan)
    {
        _db.InspectionPlans.Update(plan);
        await _db.SaveChangesAsync();
    }

    public async Task DeletePlanAsync(int id)
    {
        var plan = await _db.InspectionPlans.FindAsync(id);
        if (plan != null)
        {
            _db.InspectionPlans.Remove(plan);
            await _db.SaveChangesAsync();
        }
    }

    // ── Inspections ──

    public async Task<QCInspection> CreateInspectionFromPlanAsync(int jobId, int planId, string inspectorUserId)
    {
        var plan = await _db.InspectionPlans
            .Include(p => p.Characteristics)
            .FirstOrDefaultAsync(p => p.Id == planId)
            ?? throw new InvalidOperationException("Inspection plan not found.");

        var job = await _db.Jobs.FindAsync(jobId)
            ?? throw new InvalidOperationException("Job not found.");

        var inspection = new QCInspection
        {
            JobId = jobId,
            PartId = job.PartId,
            InspectionPlanId = planId,
            InspectorUserId = int.TryParse(inspectorUserId, out var uid) ? uid : 0,
            OverallResult = InspectionResult.Pending
        };

        foreach (var c in plan.Characteristics.OrderBy(x => x.DisplayOrder))
        {
            inspection.Measurements.Add(new InspectionMeasurement
            {
                CharacteristicName = c.Name,
                DrawingCallout = c.DrawingCallout,
                NominalValue = c.NominalValue,
                TolerancePlus = c.TolerancePlus,
                ToleranceMinus = c.ToleranceMinus
            });
        }

        _db.QCInspections.Add(inspection);
        await _db.SaveChangesAsync();
        return inspection;
    }

    public async Task<QCInspection> SaveInspectionAsync(QCInspection inspection)
    {
        foreach (var m in inspection.Measurements)
        {
            m.Deviation = m.ActualValue - m.NominalValue;
            m.IsInSpec = m.ActualValue >= (m.NominalValue - m.ToleranceMinus)
                      && m.ActualValue <= (m.NominalValue + m.TolerancePlus);
        }

        bool allInSpec = inspection.Measurements.All(m => m.IsInSpec);
        inspection.OverallPass = allInSpec;
        inspection.OverallResult = allInSpec ? InspectionResult.Pass : InspectionResult.Fail;

        _db.QCInspections.Update(inspection);
        await _db.SaveChangesAsync();

        // Record SPC data points
        foreach (var m in inspection.Measurements)
        {
            _db.SpcDataPoints.Add(new SpcDataPoint
            {
                PartId = inspection.PartId ?? 0,
                CharacteristicName = m.CharacteristicName,
                MeasuredValue = m.ActualValue,
                NominalValue = m.NominalValue,
                TolerancePlus = m.TolerancePlus,
                ToleranceMinus = m.ToleranceMinus,
                QcInspectionId = inspection.Id,
                JobId = inspection.JobId
            });
        }
        await _db.SaveChangesAsync();

        return inspection;
    }

    public async Task<List<QCInspection>> GetInspectionsForJobAsync(int jobId) =>
        await _db.QCInspections
            .Include(i => i.Measurements)
            .Include(i => i.Part)
            .Where(i => i.JobId == jobId)
            .OrderByDescending(i => i.InspectionDate)
            .ToListAsync();

    public async Task<List<QCInspection>> GetRecentInspectionsAsync(int count = 20) =>
        await _db.QCInspections
            .Include(i => i.Part)
            .OrderByDescending(i => i.InspectionDate)
            .Take(count)
            .ToListAsync();

    public async Task<QCInspection?> GetInspectionByIdAsync(int id) =>
        await _db.QCInspections
            .Include(i => i.Measurements)
            .Include(i => i.Part)
            .Include(i => i.InspectionPlan)
            .FirstOrDefaultAsync(i => i.Id == id);

    // ── NCR ──

    public async Task<NonConformanceReport> CreateNcrAsync(NonConformanceReport ncr)
    {
        ncr.NcrNumber = await _numberSeq.NextAsync("NCR");
        _db.NonConformanceReports.Add(ncr);
        await _db.SaveChangesAsync();
        return ncr;
    }

    public async Task UpdateNcrAsync(NonConformanceReport ncr)
    {
        if (ncr.Status == NcrStatus.Closed && ncr.ClosedAt == null)
            ncr.ClosedAt = DateTime.UtcNow;

        _db.NonConformanceReports.Update(ncr);
        await _db.SaveChangesAsync();
    }

    public async Task<List<NonConformanceReport>> GetOpenNcrsAsync() =>
        await _db.NonConformanceReports
            .Include(n => n.Job)
            .Include(n => n.Part)
            .Include(n => n.CorrectiveAction)
            .Where(n => n.Status != NcrStatus.Closed)
            .OrderByDescending(n => n.ReportedAt)
            .ToListAsync();

    public async Task<List<NonConformanceReport>> GetAllNcrsAsync() =>
        await _db.NonConformanceReports
            .Include(n => n.Job)
            .Include(n => n.Part)
            .Include(n => n.CorrectiveAction)
            .OrderByDescending(n => n.ReportedAt)
            .ToListAsync();

    public async Task<NonConformanceReport?> GetNcrByIdAsync(int id) =>
        await _db.NonConformanceReports
            .Include(n => n.Job)
            .Include(n => n.Part)
            .Include(n => n.CorrectiveAction)
            .FirstOrDefaultAsync(n => n.Id == id);

    // ── CAPA ──

    public async Task<CorrectiveAction> CreateCapaAsync(CorrectiveAction capa)
    {
        capa.CapaNumber = await _numberSeq.NextAsync("CAPA");
        _db.CorrectiveActions.Add(capa);
        await _db.SaveChangesAsync();
        return capa;
    }

    public async Task UpdateCapaAsync(CorrectiveAction capa)
    {
        if (capa.Status == CapaStatus.Closed && capa.CompletedAt == null)
            capa.CompletedAt = DateTime.UtcNow;

        _db.CorrectiveActions.Update(capa);
        await _db.SaveChangesAsync();
    }

    public async Task<List<CorrectiveAction>> GetAllCapasAsync() =>
        await _db.CorrectiveActions
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

    public async Task<CorrectiveAction?> GetCapaByIdAsync(int id) =>
        await _db.CorrectiveActions.FindAsync(id);

    // ── Dashboard KPIs ──

    public async Task<QualityDashboardData> GetDashboardDataAsync()
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        var inspections = await _db.QCInspections
            .Where(i => i.InspectionDate >= thirtyDaysAgo)
            .ToListAsync();

        int total = inspections.Count;
        int passed = inspections.Count(i => i.OverallPass);
        decimal fpy = total > 0 ? Math.Round((decimal)passed / total * 100, 1) : 0;

        int openNcrs = await _db.NonConformanceReports.CountAsync(n => n.Status != NcrStatus.Closed);
        int openCapas = await _db.CorrectiveActions.CountAsync(c => c.Status != CapaStatus.Closed);
        int inInspection = await _db.QCInspections.CountAsync(i => i.OverallResult == InspectionResult.Pending);

        var recentNcrs = await _db.NonConformanceReports
            .Include(n => n.Part)
            .Include(n => n.Job)
            .OrderByDescending(n => n.ReportedAt)
            .Take(10)
            .ToListAsync();

        var overdueCapas = await _db.CorrectiveActions
            .Where(c => c.Status != CapaStatus.Closed && c.DueDate < DateTime.UtcNow)
            .OrderBy(c => c.DueDate)
            .ToListAsync();

        return new QualityDashboardData(total, passed, fpy, openNcrs, openCapas, inInspection, recentNcrs, overdueCapas);
    }
}
