using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Vectrik.Components;
using Vectrik.Data;
using Vectrik.Hubs;
using Vectrik.Middleware;
using Vectrik.Services;
using Vectrik.Services.Auth;
using Vectrik.Services.MachineProviders;
using Vectrik.Services.Platform;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Platform DB — use HOME path on Azure for writable storage, fallback to local "data" dir
var dataRoot = Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } home
    ? Path.Combine(home, "data")
    : "data";
var platformDbPath = Path.Combine(dataRoot, "platform.db");
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
    Vectrik.Services.Platform.TenantCircuitHandler>();

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
builder.Services.AddScoped<IUserSettingsService, UserSettingsService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<TabManagerService>();

// Tenant services
builder.Services.AddScoped<IPartService, PartService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IWorkOrderService, WorkOrderService>();
builder.Services.AddScoped<IQuoteService, QuoteService>();
builder.Services.AddScoped<IStageService, StageService>();
builder.Services.AddScoped<ISerialNumberService, SerialNumberService>();
builder.Services.AddScoped<IPartTrackerService, PartTrackerService>();
builder.Services.AddScoped<ILearningService, LearningService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<IManufacturingApproachService, ManufacturingApproachService>();
builder.Services.AddScoped<IMachineService, MachineService>();
builder.Services.AddScoped<IOperatorRoleService, OperatorRoleService>();
builder.Services.AddScoped<IExternalOperationService, ExternalOperationService>();
builder.Services.AddScoped<IBuildTemplateService, BuildTemplateService>();
builder.Services.AddScoped<IDataSeedingService, DataSeedingService>();
builder.Services.AddScoped<IDevIssueService, DevIssueService>();
builder.Services.AddScoped<IManufacturingProcessService, ManufacturingProcessService>();
builder.Services.AddScoped<IBatchService, BatchService>();
builder.Services.AddScoped<IMachineProgramService, MachineProgramService>();
builder.Services.AddScoped<IStageCostService, StageCostService>();
builder.Services.AddScoped<IPartPricingService, PartPricingService>();

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
builder.Services.AddScoped<IShiftManagementService, ShiftManagementService>();
builder.Services.AddScoped<IBuildAdvisorService, BuildAdvisorService>();
builder.Services.AddScoped<ISchedulingService, SchedulingService>();
builder.Services.AddScoped<IProgramSchedulingService, ProgramSchedulingService>();
builder.Services.AddScoped<IProgramPlanningService, ProgramPlanningService>();
builder.Services.AddScoped<IDownstreamProgramService, DownstreamProgramService>();
builder.Services.AddScoped<ISchedulingDiagnosticsService, SchedulingDiagnosticsService>();
builder.Services.AddScoped<IOeeService, OeeService>();

// Inventory Control (Module 06)
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IMaterialPlanningService, MaterialPlanningService>();

// Quality Systems (Module 05)
builder.Services.AddScoped<IQualityService, QualityService>();
builder.Services.AddScoped<ISpcService, SpcService>();

// Visual Work Instructions (Module 03)
builder.Services.AddScoped<IWorkInstructionService, WorkInstructionService>();

// Shipping
builder.Services.AddScoped<IShippingService, ShippingService>();

// Machine providers + SignalR notifier
builder.Services.AddScoped<MachineProviderFactory>();
builder.Services.AddSingleton<IMachineStateNotifier, MachineStateNotifier>();
builder.Services.AddHostedService<MachineSyncService>();

var app = builder.Build();

// Ensure platform DB is created and seeded
// Wrapped in try-catch to handle concurrent startup (Azure may start multiple workers)
using (var scope = app.Services.CreateScope())
{
    var platformDb = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    platformDb.Database.Migrate();

    try
    {
        // Ensure platform admin exists
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        if (!platformDb.PlatformUsers.Any(u => u.Username == "henry"))
        {
            platformDb.PlatformUsers.Add(new Vectrik.Models.Platform.PlatformUser
            {
                Username = "henry",
                PasswordHash = authService.HashPassword("Vectrik2026!"),
                Role = "SuperAdmin"
            });
            platformDb.SaveChanges();
        }

        // Seed demo tenant if none exists
        if (!platformDb.Tenants.Any())
        {
            var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
            tenantService.CreateTenantAsync("demo", "Polite Society Industries", "System",
                logoUrl: "/uploads/logos/psi-shield.svg", primaryColor: "#a1a1aa").GetAwaiter().GetResult();
        }
        else
        {
            // Ensure existing tenants have seeded data (handles deleted/recreated tenant DBs)
            var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
            foreach (var tenant in platformDb.Tenants.Where(t => t.IsActive).ToList())
            {
                tenantService.SeedTenantDatabaseAsync(tenant.Code).GetAwaiter().GetResult();
            }
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
                "sls", "advanced.spc", "advanced.workflows", "advanced.custom_fields",
                "dlms", "dlms.iuid", "dlms.wawf", "dlms.gfm", "dlms.cdrl"
            };
            var enabledByDefault = new HashSet<string>
            {
                "module.quoting", "module.workorders", "module.shopfloor",
                "module.quality", "module.inventory", "module.analytics",
                "module.pdm", "module.maintenance", "sls",
                "advanced.spc", "advanced.workflows", "advanced.custom_fields"
            };
            foreach (var key in coreFlags)
            {
                platformDb.TenantFeatureFlags.Add(new Vectrik.Models.Platform.TenantFeatureFlag
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
    catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        when (ex.InnerException?.Message.Contains("UNIQUE constraint") == true)
    {
        // Another process already seeded — safe to ignore
        app.Logger.LogWarning("Seed data already exists (concurrent startup). Continuing.");
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

// Health check endpoint for Azure App Service monitoring
app.MapGet("/healthz", () =>
{
    var debugDataRoot = Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } debugHome
        ? Path.Combine(debugHome, "data") : "data";
    var tenantsDir = Path.Combine(debugDataRoot, "tenants");
    var tenantFiles = Directory.Exists(tenantsDir)
        ? Directory.GetFiles(tenantsDir, "*.db").Select(Path.GetFileName).ToList()
        : new List<string?>();

    var results = new List<object>();
    foreach (var file in tenantFiles)
    {
        var dbPath = Path.Combine(tenantsDir, file!);
        var users = new List<string>();
        try
        {
            var opts = new DbContextOptionsBuilder<TenantDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            using var tdb = new TenantDbContext(opts);
            users = tdb.Users.Select(u => $"{u.Username} (active={u.IsActive}, role={u.Role})").ToList();
        }
        catch (Exception ex) { users.Add($"ERROR: {ex.Message}"); }
        results.Add(new { file, dbPath, users });
    }
    return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow, dataRoot = debugDataRoot, tenants = results });
}).AllowAnonymous();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHub<MachineStateHub>("/hubs/machine-state");

// Logo upload endpoint
var uploadsDir = Path.Combine(app.Environment.WebRootPath, "uploads", "logos");
if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

app.MapPost("/api/uploads/logo", async (HttpRequest request) =>
{
    if (!request.HasFormContentType) return Results.BadRequest("Expected multipart form data.");

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    var tenantCode = form["tenantCode"].ToString();

    if (file == null || file.Length == 0) return Results.BadRequest("No file uploaded.");
    if (file.Length > 2 * 1024 * 1024) return Results.BadRequest("File too large (max 2MB).");
    if (string.IsNullOrWhiteSpace(tenantCode)) return Results.BadRequest("Tenant code required.");

    var allowedTypes = new[] { "image/png", "image/jpeg", "image/svg+xml", "image/webp" };
    if (!allowedTypes.Contains(file.ContentType))
        return Results.BadRequest($"Invalid file type. Allowed: png, jpg, svg, webp.");

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (string.IsNullOrEmpty(ext)) ext = ".png";
    var fileName = $"{tenantCode}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{ext}";
    var filePath = Path.Combine(uploadsDir, fileName);

    await using var stream = new FileStream(filePath, FileMode.Create);
    await file.CopyToAsync(stream);

    return Results.Ok(new { url = $"/uploads/logos/{fileName}" });
}).RequireAuthorization(policy => policy.RequireRole("SuperAdmin")).DisableAntiforgery();

app.Run();
