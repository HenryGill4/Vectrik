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
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// SignalR
builder.Services.AddSignalR();

// Platform services
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IAuthService, AuthService>();

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
builder.Services.AddScoped<IDataSeedingService, DataSeedingService>();

// Machine providers + SignalR notifier
builder.Services.AddScoped<MachineProviderFactory>();
builder.Services.AddSingleton<IMachineStateNotifier, MachineStateNotifier>();
builder.Services.AddHostedService<MachineSyncService>();

var app = builder.Build();

// Ensure platform DB is created
using (var scope = app.Services.CreateScope())
{
    var platformDb = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    platformDb.Database.EnsureCreated();
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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHub<MachineStateHub>("/hubs/machine-state");

app.Run();
