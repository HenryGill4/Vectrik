using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class Part
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string PartNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required, MaxLength(100)]
    public string Material { get; set; } = "Ti-6Al-4V Grade 5";

    [Required, MaxLength(100)]
    public string ManufacturingApproach { get; set; } = "SLS-Based";

    // PDM Fields
    [MaxLength(50)]
    public string? CustomerPartNumber { get; set; }

    [MaxLength(50)]
    public string? DrawingNumber { get; set; }

    [MaxLength(20)]
    public string? Revision { get; set; }

    public DateTime? RevisionDate { get; set; }

    [Range(0, 10000)]
    public double? EstimatedWeightKg { get; set; }

    [MaxLength(200)]
    public string? RawMaterialSpec { get; set; }

    // DLMS / Customization
    public string? CustomFieldValues { get; set; }

    public ItarClassification ItarClassification { get; set; } = ItarClassification.None;

    public bool IsDefensePart { get; set; }

    // SLS Stacking
    public bool AllowStacking { get; set; }

    [Range(0.1, 500)]
    public double? SingleStackDurationHours { get; set; }

    [Range(0.1, 500)]
    public double? DoubleStackDurationHours { get; set; }

    [Range(0.1, 500)]
    public double? TripleStackDurationHours { get; set; }

    [Range(1, 10)]
    public int MaxStackCount { get; set; } = 1;

    [Required, Range(1, 100)]
    public int PartsPerBuildSingle { get; set; } = 1;

    [Range(1, 100)]
    public int? PartsPerBuildDouble { get; set; }

    [Range(1, 100)]
    public int? PartsPerBuildTriple { get; set; }

    public bool EnableDoubleStack { get; set; }
    public bool EnableTripleStack { get; set; }

    [Range(0.1, 500)]
    public double? StageEstimateSingle { get; set; }

    // Batch Stage Durations
    [Range(0.1, 500)]
    public double? SlsBuildDurationHours { get; set; }

    [Range(1, 100)]
    public int? SlsPartsPerBuild { get; set; }

    [Range(0.1, 100)]
    public double? DepowderingDurationHours { get; set; }

    [Range(1, 100)]
    public int? DepowderingPartsPerBatch { get; set; }

    [Range(0.1, 100)]
    public double? HeatTreatmentDurationHours { get; set; }

    [Range(1, 100)]
    public int? HeatTreatmentPartsPerBatch { get; set; }

    [Range(0.1, 100)]
    public double? WireEdmDurationHours { get; set; }

    [Range(1, 100)]
    public int? WireEdmPartsPerSession { get; set; }

    // Stage Config
    [Required, MaxLength(1000)]
    public string RequiredStages { get; set; } = "[]";

    // Status + Audit
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual ICollection<PartStageRequirement> StageRequirements { get; set; } = new List<PartStageRequirement>();
    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();
    public virtual ICollection<PartDrawing> Drawings { get; set; } = new List<PartDrawing>();
    public virtual ICollection<PartRevisionHistory> RevisionHistory { get; set; } = new List<PartRevisionHistory>();
    public virtual ICollection<PartNote> Notes { get; set; } = new List<PartNote>();
    public virtual ICollection<InspectionPlan> InspectionPlans { get; set; } = new List<InspectionPlan>();

    // NotMapped computed properties
    [NotMapped]
    public double? SlsPerPartHours => SlsBuildDurationHours.HasValue && SlsPartsPerBuild.HasValue && SlsPartsPerBuild > 0
        ? SlsBuildDurationHours.Value / SlsPartsPerBuild.Value
        : null;

    [NotMapped]
    public double? DepowderingPerPartHours => DepowderingDurationHours.HasValue && DepowderingPartsPerBatch.HasValue && DepowderingPartsPerBatch > 0
        ? DepowderingDurationHours.Value / DepowderingPartsPerBatch.Value
        : null;

    [NotMapped]
    public double? HeatTreatmentPerPartHours => HeatTreatmentDurationHours.HasValue && HeatTreatmentPartsPerBatch.HasValue && HeatTreatmentPartsPerBatch > 0
        ? HeatTreatmentDurationHours.Value / HeatTreatmentPartsPerBatch.Value
        : null;

    [NotMapped]
    public double? WireEdmPerPartHours => WireEdmDurationHours.HasValue && WireEdmPartsPerSession.HasValue && WireEdmPartsPerSession > 0
        ? WireEdmDurationHours.Value / WireEdmPartsPerSession.Value
        : null;

    [NotMapped]
    public bool HasStackingConfiguration => AllowStacking && SingleStackDurationHours.HasValue;

    [NotMapped]
    public double? EffectiveSingleDuration => SingleStackDurationHours ?? StageEstimateSingle;

    [NotMapped]
    public bool HasValidDoubleStack => EnableDoubleStack && DoubleStackDurationHours.HasValue && PartsPerBuildDouble.HasValue;

    [NotMapped]
    public bool HasValidTripleStack => EnableTripleStack && TripleStackDurationHours.HasValue && PartsPerBuildTriple.HasValue;

    [NotMapped]
    public List<int> AvailableStackLevels
    {
        get
        {
            var levels = new List<int> { 1 };
            if (HasValidDoubleStack) levels.Add(2);
            if (HasValidTripleStack) levels.Add(3);
            return levels;
        }
    }

    public double? GetStackDuration(int level) => level switch
    {
        1 => EffectiveSingleDuration,
        2 => HasValidDoubleStack ? DoubleStackDurationHours : null,
        3 => HasValidTripleStack ? TripleStackDurationHours : null,
        _ => null
    };

    public int? GetPartsPerBuild(int level) => level switch
    {
        1 => PartsPerBuildSingle,
        2 => PartsPerBuildDouble,
        3 => PartsPerBuildTriple,
        _ => null
    };

    public List<string> ValidateStackingConfiguration()
    {
        var errors = new List<string>();
        if (!AllowStacking) return errors;

        if (!SingleStackDurationHours.HasValue)
            errors.Add("Single stack duration is required when stacking is enabled.");

        if (EnableDoubleStack)
        {
            if (!DoubleStackDurationHours.HasValue)
                errors.Add("Double stack duration is required when double stacking is enabled.");
            if (!PartsPerBuildDouble.HasValue)
                errors.Add("Parts per build (double) is required when double stacking is enabled.");
        }

        if (EnableTripleStack)
        {
            if (!TripleStackDurationHours.HasValue)
                errors.Add("Triple stack duration is required when triple stacking is enabled.");
            if (!PartsPerBuildTriple.HasValue)
                errors.Add("Parts per build (triple) is required when triple stacking is enabled.");
        }

        return errors;
    }

    public int GetRecommendedStackLevel(int quantity)
    {
        if (!AllowStacking || quantity <= 0) return 1;

        var bestLevel = 1;
        var bestEfficiency = double.MaxValue;

        foreach (var level in AvailableStackLevels)
        {
            var duration = GetStackDuration(level);
            var partsPerBuild = GetPartsPerBuild(level);
            if (!duration.HasValue || !partsPerBuild.HasValue || partsPerBuild.Value == 0) continue;

            var builds = Math.Ceiling((double)quantity / partsPerBuild.Value);
            var totalHours = builds * duration.Value;
            var hoursPerPart = totalHours / quantity;

            if (hoursPerPart < bestEfficiency)
            {
                bestEfficiency = hoursPerPart;
                bestLevel = level;
            }
        }

        return bestLevel;
    }

    public double? CalculateStackEfficiency(int level, int quantity)
    {
        var duration = GetStackDuration(level);
        var partsPerBuild = GetPartsPerBuild(level);
        if (!duration.HasValue || !partsPerBuild.HasValue || partsPerBuild.Value == 0 || quantity <= 0) return null;

        var builds = Math.Ceiling((double)quantity / partsPerBuild.Value);
        return (builds * duration.Value) / quantity;
    }
}
