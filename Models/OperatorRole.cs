using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

/// <summary>
/// Defines an operator role that can be assigned to users and required by production stages.
/// One user can hold multiple roles; one stage requires one role.
/// </summary>
public class OperatorRole
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    // Navigation
    public virtual ICollection<UserOperatorRole> UserRoles { get; set; } = new List<UserOperatorRole>();
}
