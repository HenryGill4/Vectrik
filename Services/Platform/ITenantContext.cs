namespace Vectrik.Services.Platform;

public interface ITenantContext
{
    string TenantCode { get; }
    string CompanyName { get; }
    bool IsSuperAdmin { get; }
}
