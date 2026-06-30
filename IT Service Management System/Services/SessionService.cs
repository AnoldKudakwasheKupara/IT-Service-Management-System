using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services
{
    /// <summary>
    /// Manages persistent <see cref="UserSession"/> records and the ASP.NET session state that
    /// links to them. Enables active-session listing, concurrent-session detection, and
    /// revocation ("log out everywhere", invalidate-after-password-change).
    /// </summary>
    public class SessionService
    {
        public const string SessionTokenKey = "SessionToken";
        public const string SecurityStampKey = "SecurityStamp";

        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _http;
        private readonly GeoLocationService _geo;

        public SessionService(ApplicationDbContext context, IHttpContextAccessor http, GeoLocationService geo)
        {
            _context = context;
            _http = http;
            _geo = geo;
        }

        /// <summary>Creates a session record and populates the ASP.NET session for a freshly authenticated user.</summary>
        public async Task<UserSession> StartSessionAsync(User user)
        {
            var http = _http.HttpContext!;
            var token = Guid.NewGuid().ToString("N");

            // Ensure the user has a security stamp.
            if (string.IsNullOrEmpty(user.SecurityStamp))
                user.SecurityStamp = Guid.NewGuid().ToString("N");

            var ip = GetIp();
            var session = new UserSession
            {
                UserId = user.Id,
                SessionToken = token,
                IpAddress = ip,
                Device = GetDevice(),
                Location = await _geo.ResolveAsync(ip),
                CreatedAt = DateTime.Now,
                LastSeenAt = DateTime.Now
            };

            _context.UserSessions.Add(session);
            await _context.SaveChangesAsync();

            http.Session.SetInt32("UserId", user.Id);
            http.Session.SetString("UserName", user.FirstName);
            http.Session.SetString("UserRole", user.Role.ToString());
            http.Session.SetString(SessionTokenKey, token);
            http.Session.SetString(SecurityStampKey, user.SecurityStamp);

            return session;
        }

        /// <summary>
        /// Validates the current request's session against the database. Returns false when the
        /// session record is missing or revoked, or the security stamp no longer matches.
        /// Also bumps LastSeenAt (throttled).
        /// </summary>
        public async Task<bool> ValidateCurrentAsync()
        {
            var http = _http.HttpContext!;
            var userId = http.Session.GetInt32("UserId");
            var token = http.Session.GetString(SessionTokenKey);
            var stamp = http.Session.GetString(SecurityStampKey);

            if (userId == null || string.IsNullOrEmpty(token))
                return false;

            var session = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.SessionToken == token);

            if (session == null || session.RevokedAt != null)
                return false;

            // Security-stamp check: rotated on password change / revoke-all.
            var currentStamp = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.SecurityStamp)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(currentStamp) && currentStamp != stamp)
                return false;

            // Throttled heartbeat to avoid a write on every request.
            if (session.LastSeenAt < DateTime.Now.AddMinutes(-1))
            {
                session.LastSeenAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return true;
        }

        /// <summary>Revokes the session tied to the current request (logout).</summary>
        public async Task RevokeCurrentAsync(string reason)
        {
            var token = _http.HttpContext?.Session.GetString(SessionTokenKey);
            if (string.IsNullOrEmpty(token))
                return;

            var session = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.SessionToken == token && s.RevokedAt == null);

            if (session != null)
            {
                session.RevokedAt = DateTime.Now;
                session.RevokedReason = reason;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>Revokes all active sessions for a user, optionally keeping one token alive.</summary>
        public async Task RevokeAllAsync(int userId, string reason, string? exceptToken = null)
        {
            var sessions = await _context.UserSessions
                .Where(s => s.UserId == userId && s.RevokedAt == null)
                .ToListAsync();

            foreach (var s in sessions)
            {
                if (exceptToken != null && s.SessionToken == exceptToken)
                    continue;
                s.RevokedAt = DateTime.Now;
                s.RevokedReason = reason;
            }

            await _context.SaveChangesAsync();
        }

        public string? CurrentToken() => _http.HttpContext?.Session.GetString(SessionTokenKey);

        public string CurrentIp() => GetIp();
        public string CurrentDevice() => GetDevice();

        /// <summary>True when this user has previously had a session from the current device + IP.</summary>
        public async Task<bool> IsKnownDeviceAsync(int userId)
        {
            var ip = GetIp();
            var device = GetDevice();
            return await _context.UserSessions
                .AnyAsync(s => s.UserId == userId && s.IpAddress == ip && s.Device == device);
        }

        // ── helpers ──────────────────────────────────────────────────────────────
        private string GetIp()
        {
            var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
            if (ip == "::1") ip = "127.0.0.1";
            return ip ?? "Unknown";
        }

        private string GetDevice()
        {
            var ua = _http.HttpContext?.Request.Headers.UserAgent.ToString();
            if (string.IsNullOrEmpty(ua)) return "Unknown Device";

            string browser = ua.Contains("Edg") ? "Edge"
                : ua.Contains("Chrome") ? "Chrome"
                : ua.Contains("Firefox") ? "Firefox"
                : ua.Contains("Safari") ? "Safari" : "Unknown Browser";

            string os = ua.Contains("Windows") ? "Windows"
                : ua.Contains("Android") ? "Android"
                : ua.Contains("iPhone") || ua.Contains("iPad") ? "iOS"
                : ua.Contains("Mac") ? "macOS"
                : ua.Contains("Linux") ? "Linux" : "Unknown OS";

            return $"{browser} on {os}";
        }
    }
}
