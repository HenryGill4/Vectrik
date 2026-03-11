using System.Security.Claims;

namespace Opcentrix_V3.Services.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password);
    ClaimsPrincipal? GetClaimsPrincipal(AuthResult result);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}

public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TenantCode { get; set; }
    public string? CompanyName { get; set; }
    public string? Username { get; set; }
    public string? FullName { get; set; }
    public string? Role { get; set; }
    public int? UserId { get; set; }
    public bool IsPlatformUser { get; set; }
    public string? RedirectUrl { get; set; }
}
