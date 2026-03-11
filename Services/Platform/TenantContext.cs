namespace Opcentrix_V3.Services.Platform;

public class TenantContext : ITenantContext
{
    public string TenantCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public bool IsSuperAdmin { get; set; }
}
