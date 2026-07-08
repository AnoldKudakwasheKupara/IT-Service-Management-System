using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Hubs;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using IT_Service_Management_System.Services.Efm;
using Microsoft.AspNetCore.DataProtection;
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
    // Populate the nav notification-bell count for every rendered page.
    options.Filters.Add<NotificationBadgeFilter>();
});

// Allow the anti-forgery token to be supplied via a request header (used by AJAX calls).
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

builder.Services.AddSignalR();

builder.Services.AddMemoryCache();

// Distributed cache + session store. Uses Redis when a connection string is configured
// (enables horizontal scaling + shared Data Protection keys); otherwise an in-memory store
// (single instance). Set ConnectionStrings:Redis or Redis:Configuration to enable.
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? builder.Configuration["Redis:Configuration"];

if (!string.IsNullOrWhiteSpace(redisConnection))
{
    try
    {
        var multiplexer = StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnection);
        builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(multiplexer);
        builder.Services.AddStackExchangeRedisCache(o =>
            o.ConnectionMultiplexerFactory = () =>
                Task.FromResult<StackExchange.Redis.IConnectionMultiplexer>(multiplexer));
        builder.Services.AddDataProtection()
            .PersistKeysToStackExchangeRedis(multiplexer, "ITSM:DataProtection:Keys")
            .SetApplicationName("ITSM");
        Console.WriteLine("[startup] Distributed cache + Data Protection backed by Redis.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[startup] Redis unavailable ({ex.Message}); falling back to in-memory cache.");
        builder.Services.AddDistributedMemoryCache();
    }
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// Email transport: SendGrid when EmailSettings:SendGridApiKey is set, otherwise SMTP (MailKit).
// The chosen sender is wrapped in a retry decorator; all sends run on the background queue.
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<SendGridEmailSender>();
var useSendGrid = !string.IsNullOrWhiteSpace(builder.Configuration["EmailSettings:SendGridApiKey"]);
builder.Services.AddScoped<IEmailSender>(sp =>
{
    IEmailSender inner = useSendGrid
        ? sp.GetRequiredService<SendGridEmailSender>()
        : sp.GetRequiredService<EmailService>();
    return new RetryingEmailSender(inner, sp.GetRequiredService<ILogger<RetryingEmailSender>>());
});

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

// Breached-password screening via the Have I Been Pwned range API (typed client with a short
// timeout so a slow/unreachable service never stalls a password change — the checker fails open).
builder.Services.AddHttpClient<BreachedPasswordChecker>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(3);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("AxisITSM-PasswordCheck");
});

builder.Services.AddScoped<GeoLocationService>();

builder.Services.AddScoped<AlertService>();

builder.Services.AddScoped<BackupService>();

// Background work queue + hosted processor (audit writes, geo lookups, email sends run off-request).
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddScoped<EmailDispatcher>();

// EFM document storage (pluggable). LocalDisk by default; set EFM:StorageProvider
// to "AzureBlob" or "AwsS3" once those providers are implemented + configured.
builder.Services.AddScoped<LocalDiskDocumentStorage>();
builder.Services.AddScoped<AzureBlobDocumentStorage>();
builder.Services.AddScoped<AwsS3DocumentStorage>();
builder.Services.AddScoped<IDocumentStorage>(sp =>
{
    var provider = (sp.GetRequiredService<IConfiguration>()["EFM:StorageProvider"] ?? "LocalDisk")
        .Trim().ToLowerInvariant();
    return provider switch
    {
        "azureblob" or "azure" => sp.GetRequiredService<AzureBlobDocumentStorage>(),
        "awss3" or "s3" => sp.GetRequiredService<AwsS3DocumentStorage>(),
        _ => sp.GetRequiredService<LocalDiskDocumentStorage>()
    };
});
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<DocumentApprovalService>();

// IMS / ISO (ISO 9001:2015 & ISO/IEC 27001:2022) services.
builder.Services.AddScoped<IT_Service_Management_System.Services.Ims.ImsNotificationService>();
builder.Services.AddScoped<IT_Service_Management_System.Services.Ims.IsoDocumentService>();

// Defensive, idempotent demo-data top-up seeder (gated by Demo:Seed, default ON).
builder.Services.AddScoped<IT_Service_Management_System.Services.DemoDataSeeder>();
// OCR engine (pluggable). PlainText baseline by default; set EFM:Ocr:Provider = "tesseract"
// to OCR images + scanned PDFs (requires a tessdata folder — see EFM:Ocr:TessDataPath).
if (string.Equals(builder.Configuration["EFM:Ocr:Provider"], "tesseract", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<IOcrService, TesseractOcrService>();
else
    builder.Services.AddScoped<IOcrService, PlainTextOcrService>();
builder.Services.AddScoped<DocumentMaintenanceService>();
builder.Services.AddHostedService<DocumentMaintenanceHostedService>();

// Malware scanning on uploads. Heuristic (content + magic-byte) by default; set
// Security:Av:Provider = "clamav" (+ Security:Av:Host/Port) to scan via a ClamAV daemon.
if (string.Equals(builder.Configuration["Security:Av:Provider"], "clamav", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<IT_Service_Management_System.Services.Security.IMalwareScanner,
        IT_Service_Management_System.Services.Security.ClamAvMalwareScanner>();
else
    builder.Services.AddScoped<IT_Service_Management_System.Services.Security.IMalwareScanner,
        IT_Service_Management_System.Services.Security.HeuristicMalwareScanner>();

// Real-time notifications (SignalR) + configurable SLA engine.
builder.Services.AddScoped<IT_Service_Management_System.Services.Realtime.IRealtimeNotifier,
    IT_Service_Management_System.Services.Realtime.RealtimeNotifier>();
builder.Services.AddScoped<IT_Service_Management_System.Services.Itsm.ISlaService,
    IT_Service_Management_System.Services.Itsm.SlaService>();

// QuestPDF community licence (free for this use); required before any PDF is generated.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Baseline security response headers (clickjacking, MIME-sniffing, referrer, CSP) on every response.
app.UseMiddleware<IT_Service_Management_System.Middleware.SecurityHeadersMiddleware>();

app.UseRouting();

// Concise structured request logging (method, path, status, elapsed).
app.UseSerilogRequestLogging();

app.UseSession();

app.UseMiddleware<IT_Service_Management_System.Middleware.DatabaseFailureAlertMiddleware>();

app.UseAuthorization();

app.MapStaticAssets();

app.MapHub<ChatHub>("/chathub");
app.MapHub<IT_Service_Management_System.Hubs.NotificationHub>("/hubs/notifications");

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

    // In production, run migrations as a deliberate deploy step instead (set Database:MigrateOnStartup=false).
    if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
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

    // Demo-data top-up seeding (idempotent; never crashes startup). Default ON; disable via Demo:Seed=false.
    if (app.Configuration.GetValue("Demo:Seed", true))
    {
        try { await scope.ServiceProvider.GetRequiredService<IT_Service_Management_System.Services.DemoDataSeeder>().SeedAsync(); }
        catch (Exception ex) { Log.Warning(ex, "Demo data seeding failed"); }
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