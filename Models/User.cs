using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Role { get; set; } = "Operator";

    [MaxLength(100)]
    public string? Department { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? AssignedStageIds { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginDate { get; set; }
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    public virtual UserSettings? Settings { get; set; }
    public virtual ICollection<UserOperatorRole> OperatorRoles { get; set; } = new List<UserOperatorRole>();
}
