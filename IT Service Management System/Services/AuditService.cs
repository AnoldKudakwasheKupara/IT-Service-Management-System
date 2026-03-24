using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;

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

                var log = new AuditLog
                {
                    UserId = userId ?? "System",
                    UserName = string.IsNullOrEmpty(userName)
                        ? "Anonymous"
                        : $"{userName} ({userRole})", // ✅ MORE DETAIL

                    Action = action,
                    Entity = entity,
                    EntityId = entityId,
                    Details = details,

                    Timestamp = DateTime.Now,

                    IpAddress = GetRealIpAddress() // ✅ FIXED BELOW
                };

                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Audit Error: " + ex.Message);
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