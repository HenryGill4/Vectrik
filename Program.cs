using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Components;
using Opcentrix_V3.Data;
using Opcentrix_V3.Hubs;
using Opcentrix_V3.Middleware;
using Opcentrix_V3.Services;
using Opcentrix_V3.Services.Auth;
using Opcentrix_V3.Services.MachineProviders;
using Opcentrix_V3.Services.Platform;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Platform DB
var platformDbPath = Path.Combine("data", "platform.db");
var platformDir = Path.GetDirectoryName(platformDbPath);
if (!string.IsNullOrEmpty(platformDir) && !Directory.Exists(platformDir))
    Directory.CreateDirectory(platformDir);

builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseSqlite($"Data Source={platformDbPath}"));

// Multi-tenant
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<TenantDbContextFactory>();
builder.Services.AddScoped(sp => sp.GetRequiredService<TenantDbContextFactory>().CreateDbContext());

// Populate TenantContext from auth claims when a Blazor interactive circuit opens
// (TenantMiddleware only runs on HTTP requests, not SignalR circuit connections)
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler,
    Opcentrix_V3.Services.Platform.TenantCircuitHandler>();

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(options =>
{
    // Require authentication on all endpoints by default.
    // Pages that should be public must use @attribute [AllowAnonymous].
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddCascadingAuthenticationState();

// SignalR
builder.Services.AddSignalR();

// Platform services
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ToastService>();

// Tenant services
builder.Services.AddScoped<IPartService, PartService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IWorkOrderService, WorkOrderService>();
builder.Services.AddScoped<IQuoteService, QuoteService>();
builder.Services.AddScoped<IBuildService, BuildService>();
builder.Services.AddScoped<IBuildPlanningService, BuildPlanningService>();
builder.Services.AddScoped<IStageService, StageService>();
builder.Services.AddScoped<ISerialNumberService, SerialNumberService>();
builder.Services.AddScoped<IPartTrackerService, PartTrackerService>();
builder.Services.AddScoped<ILearningService, LearningService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<IMachineService, MachineService>();
builder.Services.AddScoped<IDataSeedingService, DataSeedingService>();

// Customization Foundation (Stage 0.5)
builder.Services.AddScoped<ITenantFeatureService, TenantFeatureService>();
builder.Services.AddScoped<ICustomFieldService, CustomFieldService>();
builder.Services.AddScoped<INumberSequenceService, NumberSequenceService>();
builder.Services.AddScoped<IWorkflowEngine, WorkflowEngine>();
builder.Services.AddScoped<IDocumentTemplateService, DocumentTemplateService>();

// Parts / PDM (Stage 1)
builder.Services.AddScoped<IPartFileService, PartFileService>();

// Estimating & Quoting (Stage 2)
builder.Services.AddScoped<IPricingEngineService, PricingEngineService>();

// Shop Floor & Scheduling (Stage 4)
builder.Services.AddScoped<ISchedulingService, SchedulingService>();
builder.Services.AddScoped<IOeeService, OeeService>();

// Inventory Control (Module 06)
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IMaterialPlanningService, MaterialPlanningService>();

// Quality Systems (Module 05)
builder.Services.AddScoped<IQualityService, QualityService>();
builder.Services.AddScoped<ISpcService, SpcService>();

// Machine providers + SignalR notifier
builder.Services.AddScoped<MachineProviderFactory>();
builder.Services.AddSingleton<IMachineStateNotifier, MachineStateNotifier>();
builder.Services.AddHostedService<MachineSyncService>();

var app = builder.Build();

// Ensure platform DB is created and seeded
using (var scope = app.Services.CreateScope())
{
    var platformDb = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    platformDb.Database.Migrate();

    // Seed super admin if none exists
    if (!platformDb.PlatformUsers.Any())
    {
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        platformDb.PlatformUsers.Add(new Opcentrix_V3.Models.Platform.PlatformUser
        {
            Username = "superadmin",
            PasswordHash = authService.HashPassword("admin123"),
            Role = "SuperAdmin"
        });
        platformDb.SaveChanges();
    }

    // Seed demo tenant if none exists
    if (!platformDb.Tenants.Any())
    {
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
        tenantService.CreateTenantAsync("demo", "Demo Manufacturing", "System").GetAwaiter().GetResult();
    }

    // Seed default feature flags for demo tenant
    if (!platformDb.TenantFeatureFlags.Any(f => f.TenantCode == "demo"))
    {
        var coreFlags = new[]
        {
            "module.quoting", "module.workorders", "module.instructions",
            "module.shopfloor", "module.quality", "module.inventory",
            "module.analytics", "module.pdm", "module.costing",
            "module.tools", "module.maintenance", "module.purchasing",
            "module.timeclock", "module.documents", "module.shipping",
            "module.crm", "module.compliance", "module.training",
            "advanced.spc", "advanced.workflows", "advanced.custom_fields",
            "dlms", "dlms.iuid", "dlms.wawf", "dlms.gfm", "dlms.cdrl"
        };
        var enabledByDefault = new HashSet<string>
        {
            "module.quoting", "module.workorders", "module.shopfloor",
            "module.quality", "module.inventory", "module.analytics",
            "module.pdm", "module.maintenance", "advanced.workflows",
            "advanced.custom_fields"
        };
        foreach (var key in coreFlags)
        {
            platformDb.TenantFeatureFlags.Add(new Opcentrix_V3.Models.Platform.TenantFeatureFlag
            {
                TenantCode = "demo",
                FeatureKey = key,
                IsEnabled = enabledByDefault.Contains(key),
                EnabledAt = enabledByDefault.Contains(key) ? DateTime.UtcNow : null
            });
        }
        platformDb.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();

app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHub<MachineStateHub>("/hubs/machine-state");

app.Run();
