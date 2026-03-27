using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

/// <summary>
/// Junction table linking users to operator roles. One user can hold multiple roles.
/// </summary>
public class UserOperatorRole
{
    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public int OperatorRoleId { get; set; }
    public virtual OperatorRole OperatorRole { get; set; } = null!;

    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string AssignedBy { get; set; } = string.Empty;
}
