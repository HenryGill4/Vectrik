using Microsoft.AspNetCore.Authentication;
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

// Catch unobserved task exceptions to prevent worker process crashes.
// In Blazor Server, circuit disconnections and cancellation tokens can produce
// TaskCanceledException on fire-and-forget paths (timer callbacks, SignalR).
TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    if (e.Exception.InnerExceptions.All(ex =>
        ex is TaskCanceledException or OperationCanceledException or ObjectDisposedException
        or Microsoft.JSInterop.JSDisconnectedException))
    {
        e.SetObserved(); // Suppress — expected during circuit teardown
    }
};

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpContextAccessor();
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
builder.Services.AddScoped<ICertifiedLayoutService, CertifiedLayoutService>();
builder.Services.AddScoped<IDataSeedingService, DataSeedingService>();
builder.Services.AddScoped<SetupReadinessService>();
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
builder.Services.AddScoped<ISmartPricingService, SmartPricingService>();
builder.Services.AddScoped<IQuoteAnalyticsService, QuoteAnalyticsService>();
builder.Services.AddScoped<ICustomerPricingService, CustomerPricingService>();

// Shop Floor & Scheduling (Stage 4)
builder.Services.AddScoped<IShiftManagementService, ShiftManagementService>();
builder.Services.AddScoped<IBuildAdvisorService, BuildAdvisorService>();
builder.Services.AddScoped<ISchedulingWeightsService, SchedulingWeightsService>();
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

// Setup Dispatch System
builder.Services.AddScoped<ISetupDispatchService, SetupDispatchService>();
builder.Services.AddSingleton<IDispatchNotifier, DispatchNotifier>();
builder.Services.AddScoped<IChangeoverDispatchService, ChangeoverDispatchService>();
builder.Services.AddScoped<IPlateLayoutDispatchService, PlateLayoutDispatchService>();
builder.Services.AddScoped<IPrintStartDispatchService, PrintStartDispatchService>();
builder.Services.AddScoped<IPrintCompletionService, PrintCompletionService>();
builder.Services.AddScoped<IDispatchScoringService, DispatchScoringService>();
builder.Services.AddScoped<IDispatchGenerationService, DispatchGenerationService>();
builder.Services.AddScoped<IMaintenanceDispatchService, MaintenanceDispatchService>();
builder.Services.AddScoped<IDispatchLearningService, DispatchLearningService>();
builder.Services.AddScoped<ICapacityPlanningService, CapacityPlanningService>();
builder.Services.AddHostedService<DispatchGenerationBackgroundService>();

// Email
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// Machine providers + SignalR notifier
builder.Services.AddScoped<MachineProviderFactory>();
builder.Services.AddSingleton<IMachineStateNotifier, MachineStateNotifier>();
builder.Services.AddHostedService<MachineSyncService>();

var app = builder.Build();

// Ensure platform DB is created and seeded
// Wrapped in try-catch to handle concurrent startup (Azure may start multiple workers)
try
{
    using var scope = app.Services.CreateScope();
    var platformDb = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    platformDb.Database.Migrate();

    // Ensure platform admin exists
    var adminPassword = Environment.GetEnvironmentVariable("VECTRIK_ADMIN_PASSWORD") ?? "Vectrik2026!";
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VECTRIK_ADMIN_PASSWORD")))
        app.Logger.LogWarning("VECTRIK_ADMIN_PASSWORD not set — using default. Set this env var in production.");
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    if (!platformDb.PlatformUsers.Any(u => u.Username == "henry"))
    {
        platformDb.PlatformUsers.Add(new Vectrik.Models.Platform.PlatformUser
        {
            Username = "henry",
            PasswordHash = authService.HashPassword(adminPassword),
            Role = "SuperAdmin"
        });
        platformDb.SaveChanges();
    }

    // Always ensure demo tenant exists and is seeded
    var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
    if (!platformDb.Tenants.Any(t => t.Code == "demo"))
    {
        Task.Run(async () =>
            await tenantService.CreateTenantAsync("demo", "Polite Society Industries", "System",
                logoUrl: "/uploads/logos/psi-shield.svg", primaryColor: "#a1a1aa")
        ).GetAwaiter().GetResult();
    }

    // Ensure all active tenants have seeded data (handles deleted/recreated tenant DBs)
    foreach (var tenant in platformDb.Tenants.Where(t => t.IsActive).ToList())
    {
        var isDemo = tenant.Code == "demo";
        Task.Run(async () =>
            await tenantService.SeedTenantDatabaseAsync(tenant.Code, isDemoTenant: isDemo)
        ).GetAwaiter().GetResult();
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
            "module.pdm", "module.maintenance", "module.instructions",
            "module.costing", "module.shipping", "sls",
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
catch (Exception ex)
{
    app.Logger.LogError(ex, "Database seeding failed. App will continue but data may be incomplete.");
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
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .AllowAnonymous();

// Password change endpoint — handles cookie re-signing (can't do this from interactive Blazor)
app.MapPost("/api/account/change-password", async (HttpContext httpContext) =>
{
    if (httpContext.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();

    var form = await httpContext.Request.ReadFormAsync();
    var currentPassword = form["currentPassword"].ToString();
    var newPassword = form["newPassword"].ToString();

    var tenantCode = httpContext.User.FindFirst("TenantCode")?.Value;
    var userIdStr = httpContext.User.FindFirst("UserId")?.Value;
    if (string.IsNullOrEmpty(tenantCode) || !int.TryParse(userIdStr, out var userId))
        return Results.BadRequest(new { error = "Unable to determine your account." });

    var dataRoot = Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } h
        ? Path.Combine(h, "data") : "data";
    var dbPath = Path.Combine(dataRoot, "tenants", $"{tenantCode}.db");
    if (!File.Exists(dbPath))
        return Results.BadRequest(new { error = "Tenant database not found." });

    var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlite($"Data Source={dbPath}")
        .Options;

    using var db = new TenantDbContext(options);
    var user = await db.Users.FindAsync(userId);
    if (user == null)
        return Results.BadRequest(new { error = "User not found." });

    var auth = httpContext.RequestServices.GetRequiredService<IAuthService>();
    if (!auth.VerifyPassword(currentPassword, user.PasswordHash))
        return Results.BadRequest(new { error = "Current password is incorrect." });

    if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        return Results.BadRequest(new { error = "New password must be at least 8 characters." });
    if (newPassword == currentPassword)
        return Results.BadRequest(new { error = "New password must be different from current password." });

    user.PasswordHash = auth.HashPassword(newPassword);
    user.MustChangePassword = false;
    user.LastModifiedDate = DateTime.UtcNow;
    user.LastModifiedBy = user.Username;
    await db.SaveChangesAsync();

    // Re-sign cookie with updated claims
    var authResult = new Vectrik.Services.Auth.AuthResult
    {
        Success = true, TenantCode = tenantCode,
        CompanyName = httpContext.User.FindFirst("CompanyName")?.Value,
        Username = user.Username, FullName = user.FullName,
        Role = user.Role, UserId = user.Id,
        IsPlatformUser = false, MustChangePassword = false
    };
    var principal = auth.GetClaimsPrincipal(authResult);
    if (principal != null)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12) });
    }
    return Results.Ok(new { success = true });
}).RequireAuthorization();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHub<MachineStateHub>("/hubs/machine-state");
app.MapHub<DispatchHub>("/hubs/dispatch");

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
