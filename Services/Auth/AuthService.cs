using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Vectrik.Data;

namespace Vectrik.Services.Auth;

public class AuthService : IAuthService
{
    private readonly PlatformDbContext _platformDb;
    private readonly IServiceProvider _serviceProvider;

    public AuthService(PlatformDbContext platformDb, IServiceProvider serviceProvider)
    {
        _platformDb = platformDb;
        _serviceProvider = serviceProvider;
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new AuthResult { Success = false, ErrorMessage = "Username and password are required." };

        var normalizedUsername = username.Trim().ToLowerInvariant();

        // Step 1: Check platform users (super admin)
        var platformUser = await _platformDb.PlatformUsers
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername);

        if (platformUser != null && VerifyPassword(password, platformUser.PasswordHash))
        {
            return new AuthResult
            {
                Success = true,
                Username = platformUser.Username,
                Role = platformUser.Role,
                IsPlatformUser = true,
                RedirectUrl = "/Platform/Tenants"
            };
        }

        // Step 2: Scan tenant databases
        var tenants = await _platformDb.Tenants
            .Where(t => t.IsActive)
            .ToListAsync();

        foreach (var tenant in tenants)
        {
            var dbPath = Path.Combine("data", "tenants", $"{tenant.Code}.db");
            if (!File.Exists(dbPath)) continue;

            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            using var tenantDb = new TenantDbContext(options);

            var user = await tenantDb.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername && u.IsActive);

            if (user != null && VerifyPassword(password, user.PasswordHash))
            {
                user.LastLoginDate = DateTime.UtcNow;
                await tenantDb.SaveChangesAsync();

                return new AuthResult
                {
                    Success = true,
                    TenantCode = tenant.Code,
                    CompanyName = tenant.CompanyName,
                    Username = user.Username,
                    FullName = user.FullName,
                    Role = user.Role,
                    UserId = user.Id,
                    IsPlatformUser = false,
                    RedirectUrl = "/dashboard"
                };
            }
        }

        return new AuthResult { Success = false, ErrorMessage = "Invalid username or password." };
    }

    public ClaimsPrincipal? GetClaimsPrincipal(AuthResult result)
    {
        if (!result.Success) return null;

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, result.Username ?? string.Empty),
            new(ClaimTypes.Role, result.Role ?? string.Empty)
        };

        if (result.IsPlatformUser)
        {
            claims.Add(new Claim("IsPlatform", "true"));
        }
        else
        {
            claims.Add(new Claim("TenantCode", result.TenantCode ?? string.Empty));
            claims.Add(new Claim("CompanyName", result.CompanyName ?? string.Empty));
            claims.Add(new Claim("UserId", result.UserId?.ToString() ?? string.Empty));
            claims.Add(new Claim("FullName", result.FullName ?? string.Empty));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        var combined = new byte[48];
        Buffer.BlockCopy(salt, 0, combined, 0, 16);
        Buffer.BlockCopy(hash, 0, combined, 16, 32);
        return Convert.ToBase64String(combined);
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var combined = Convert.FromBase64String(storedHash);
            if (combined.Length != 48) return false;

            var salt = new byte[16];
            Buffer.BlockCopy(combined, 0, salt, 0, 16);

            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);

            return CryptographicOperations.FixedTimeEquals(hash, combined.AsSpan(16, 32));
        }
        catch
        {
            return false;
        }
    }
}
