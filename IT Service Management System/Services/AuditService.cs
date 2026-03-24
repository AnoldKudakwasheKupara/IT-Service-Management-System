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
            var user = _httpContext.HttpContext.User;

            var log = new AuditLog
            {
                UserId = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                UserName = user?.Identity?.Name,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                Details = details,
                IpAddress = _httpContext.HttpContext.Connection.RemoteIpAddress?.ToString()
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}