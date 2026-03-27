using Vectrik.Services.Platform;

namespace Vectrik.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantCode = context.User.FindFirst("TenantCode")?.Value;
            var companyName = context.User.FindFirst("CompanyName")?.Value;
            var isSuperAdmin = context.User.FindFirst("IsPlatform")?.Value == "true";

            if (tenantContext is TenantContext tc)
            {
                tc.TenantCode = tenantCode ?? string.Empty;
                tc.CompanyName = companyName ?? string.Empty;
                tc.IsSuperAdmin = isSuperAdmin;
            }
        }

        await _next(context);
    }
}
