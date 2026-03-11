using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models.Platform;

public class PlatformUser
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Role { get; set; } = "SuperAdmin";
}
