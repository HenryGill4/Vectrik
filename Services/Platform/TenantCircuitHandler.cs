using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Vectrik.Services.Platform;

/// <summary>
/// Populates the scoped TenantContext from auth claims when a Blazor Server
/// interactive circuit is established. This bridges the gap where
/// TenantMiddleware (HTTP-only) does not run for SignalR circuit connections.
/// Without this, TenantDbContextFactory falls back to an in-memory database
/// and pages lose their data after SSR prerender.
/// </summary>
public class TenantCircuitHandler : CircuitHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly AuthenticationStateProvider _authStateProvider;

    public TenantCircuitHandler(
        ITenantContext tenantContext,
        AuthenticationStateProvider authStateProvider)
    {
        _tenantContext = tenantContext;
        _authStateProvider = authStateProvider;
    }

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated == true && _tenantContext is TenantContext tc)
        {
            tc.TenantCode = user.FindFirst("TenantCode")?.Value ?? string.Empty;
            tc.CompanyName = user.FindFirst("CompanyName")?.Value ?? string.Empty;
            tc.IsSuperAdmin = user.FindFirst("IsPlatform")?.Value == "true";
        }
    }
}
