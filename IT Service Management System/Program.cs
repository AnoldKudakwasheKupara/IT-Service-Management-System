using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Hubs;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging — console + daily rolling file (logs/). Overridable from appsettings.
builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/itsm-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

builder.Services.AddControllersWithViews(options =>
{
    // Require an authenticated session for every action unless [AllowAnonymous].
    options.Filters.Add<SessionAuthorizationFilter>();
    // Enforce [RoleAuthorize] role restrictions (runs after the login check).
    options.Filters.Add<RoleAuthorizationFilter>();
    // Validate the anti-forgery token on every unsafe (POST/PUT/DELETE) request.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Allow the anti-forgery token to be supplied via a request header (used by AJAX calls).
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

builder.Services.AddSignalR();

builder.Services.AddMemoryCache();

builder.Services.AddScoped<EmailService>();

// Read the configurable idle timeout from the DB (falls back to 30 min if unavailable,
// e.g. on a brand-new database before the table exists). Applied at startup.
int sessionIdleMinutes = 30;
try
{
    var probeOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
        .Options;
    using var probe = new ApplicationDbContext(probeOptions);
    var cfg = probe.AppConfigurations.AsNoTracking().FirstOrDefault();
    if (cfg != null && cfg.SessionIdleTimeoutMinutes > 0)
        sessionIdleMinutes = cfg.SessionIdleTimeoutMinutes;
}
catch
{
    // Configuration table not present yet — use the default.
}

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(sessionIdleMinutes);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<AuditService>();

builder.Services.AddScoped<ConfigurationService>();

builder.Services.AddScoped<SessionService>();

builder.Services.AddHttpClient();

builder.Services.AddScoped<GeoLocationService>();

builder.Services.AddScoped<AlertService>();

builder.Services.AddScoped<BackupService>();

// Background work queue + hosted processor (audit writes, geo lookups, email sends run off-request).
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddScoped<EmailDispatcher>();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

// Concise structured request logging (method, path, status, elapsed).
app.UseSerilogRequestLogging();

app.UseSession();

app.UseMiddleware<IT_Service_Management_System.Middleware.DatabaseFailureAlertMiddleware>();

app.UseAuthorization();

app.MapStaticAssets();

app.MapHub<ChatHub>("/chathub");

// Liveness/readiness endpoint for load balancers and uptime monitors.
app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

try
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    context.Database.Migrate();

    if (!context.Users.Any())
    {
        context.Users.Add(new User
        {
            FirstName = "Admin",
            LastName = "User",
            Email = "admin@test.com",
            PasswordHash = PasswordHasher.HashPassword("Admin@123"),
            IsActive = true,
            Role = Ticket.UserRole.Admin,
            CreatedAt = DateTime.Now
        });

        context.SaveChanges();
    }

    Log.Information("Database migrated and seeded; application starting.");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Startup database migration/seed failed.");
    throw;
}

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}