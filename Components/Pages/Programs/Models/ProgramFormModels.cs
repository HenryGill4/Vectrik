namespace Opcentrix_V3.Components.Pages.Programs.Models;

/// <summary>
/// Represents a part entry in the create/edit program form's unified parts list.
/// Used for both BuildPlate and Standard program types.
/// </summary>
public record CreateBuildPartEntry(
    int PartId,
    string PartNumber,
    string PartName,
    int Quantity,
    bool IsPrimary = false);
