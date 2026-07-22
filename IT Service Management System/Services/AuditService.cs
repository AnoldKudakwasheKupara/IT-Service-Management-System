using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;

namespace IT_Service_Management_System.Services
{
    /// <summary>
    /// Writes audit-log entries. Request-bound data (user, IP, user-agent) is captured synchronously,
    /// then the geo lookup and database write run on the background queue so they never block the
    /// originating request.
    /// </summary>
    public class AuditService
    {
        private readonly IHttpContextAccessor _httpContext;
        private readonly IBackgroundTaskQueue _queue;
        private readonly ILogger<AuditService> _logger;

        public AuditService(
            IHttpContextAccessor httpContext,
            IBackgroundTaskQueue queue,
            ILogger<AuditService> logger)
        {
            _httpContext = httpContext;
            _queue = queue;
            _logger = logger;
        }

        public Task LogAsync(string action, string entity, int? entityId = null, string details = "")
        {
            try
            {
                // Capture everything that needs the live HttpContext now; the rest runs in the background.
                var http = _httpContext.HttpContext;
                var userId = http?.Session.GetInt32("UserId")?.ToString();
                var userName = http?.Session.GetString("UserName");
                var userRole = http?.Session.GetString("UserRole");
                var ip = GetRealIpAddress();
                var device = GetDeviceInfo(http?.Request.Headers.UserAgent);
                var timestamp = DateTime.Now;

                var userLabel = string.IsNullOrEmpty(userName) ? "Anonymous" : $"{userName} ({userRole})";

                _queue.Enqueue(async (sp, ct) =>
                {
                    var db = sp.GetRequiredService<ApplicationDbContext>();
                    var geo = sp.GetRequiredService<GeoLocationService>();

                    var location = await geo.ResolveAsync(ip);

                    db.AuditLogs.Add(new AuditLog
                    {
                        UserId = userId ?? "System",
                        UserName = userLabel,
                        Action = action,
                        Entity = entity,
                        EntityId = entityId,
                        Details = details,
                        Timestamp = timestamp,
                        IpAddress = ip,
                        Location = location,
                        Device = device
                    });

                    await db.SaveChangesAsync(ct);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue audit log for action {Action} on {Entity}", action, entity);
            }

            return Task.CompletedTask;
        }

        private string GetDeviceInfo(string? userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
                return "Unknown Device";

            string browser = userAgent.Contains("Edg") ? "Edge"
                : userAgent.Contains("Chrome") ? "Chrome"
                : userAgent.Contains("Firefox") ? "Firefox"
                : userAgent.Contains("Safari") ? "Safari" : "Unknown Browser";

            string os = userAgent.Contains("Windows") ? "Windows"
                : userAgent.Contains("Android") ? "Android"
                : userAgent.Contains("iPhone") || userAgent.Contains("iPad") ? "iOS"
                : userAgent.Contains("Mac") ? "MacOS"
                : userAgent.Contains("Linux") ? "Linux" : "Unknown OS";

            return $"{browser} on {os}";
        }

        private string GetRealIpAddress()
        {
            var ip = _httpContext.HttpContext?.Connection?.RemoteIpAddress?.ToString();
            if (ip == "::1") ip = "127.0.0.1";
            return ip ?? "Unknown";
        }
    }
}
