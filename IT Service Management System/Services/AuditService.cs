using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using System.Text.Json;

namespace IT_Service_Management_System.Services
{
    public class AuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContext;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContext)
        {
            _context = context;
            _httpContext = httpContext;
        }

        public async Task LogAsync(string action, string entity, int? entityId = null, string details = "")
        {
            try
            {
                var http = _httpContext.HttpContext;

                var userId = http?.Session.GetInt32("UserId")?.ToString();
                var userName = http?.Session.GetString("UserName");
                var userRole = http?.Session.GetString("UserRole");

                var ip = GetRealIpAddress();

                var location = await GetLocationFromIp(ip);
                var device = GetDeviceInfo(http?.Request.Headers["User-Agent"]);

                var log = new AuditLog
                {
                    UserId = userId ?? "System",
                    UserName = string.IsNullOrEmpty(userName)
                        ? "Anonymous"
                        : $"{userName} ({userRole})",

                    Action = action,
                    Entity = entity,
                    EntityId = entityId,
                    Details = details,

                    Timestamp = DateTime.Now,
                    IpAddress = ip,
                    Location = location,
                    Device = device
                };

                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Audit Error: " + ex.Message);
            }
        }

        private string GetDeviceInfo(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
                return "Unknown Device";

            string browser = "Unknown Browser";
            string os = "Unknown OS";

            // Browser
            if (userAgent.Contains("Chrome")) browser = "Chrome";
            else if (userAgent.Contains("Firefox")) browser = "Firefox";
            else if (userAgent.Contains("Safari")) browser = "Safari";
            else if (userAgent.Contains("Edge")) browser = "Edge";

            // OS
            if (userAgent.Contains("Windows")) os = "Windows";
            else if (userAgent.Contains("Mac")) os = "MacOS";
            else if (userAgent.Contains("Linux")) os = "Linux";
            else if (userAgent.Contains("Android")) os = "Android";
            else if (userAgent.Contains("iPhone")) os = "iOS";

            return $"{browser} on {os}";
        }
        private async Task<string> GetLocationFromIp(string ip)
        {
            try
            {
                if (string.IsNullOrEmpty(ip) || ip == "127.0.0.1")
                    return "Localhost";

                using var client = new HttpClient();
                var response = await client.GetStringAsync($"http://ip-api.com/json/{ip}");

                var json = JsonDocument.Parse(response);

                var city = json.RootElement.GetProperty("city").GetString();
                var country = json.RootElement.GetProperty("country").GetString();

                return $"{city}, {country}";
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetRealIpAddress()
        {
            var context = _httpContext.HttpContext;

            var ip = context?.Connection?.RemoteIpAddress?.ToString();

            // Handle IPv6 localhost
            if (ip == "::1")
                ip = "127.0.0.1";

            return ip;
        }
    }
}