namespace IT_Service_Management_System.Services.Auditing
{
    /// <summary>
    /// Everything about "who did this, from where" that only the live request can answer.
    /// Captured synchronously and carried into background work, because by the time an entry is
    /// written the HttpContext is long gone.
    /// </summary>
    public record AuditRequestInfo(
        string UserId,
        string UserName,
        string? UserRole,
        string IpAddress,
        string Device,
        string? CorrelationId,
        string? RequestPath,
        string? HttpMethod)
    {
        /// <summary>Context for work that runs with no request behind it (background jobs, startup).</summary>
        public static AuditRequestInfo SystemContext(string? correlationId = null) => new(
            UserId: "System",
            UserName: "System",
            UserRole: null,
            IpAddress: "127.0.0.1",
            Device: "Server",
            CorrelationId: correlationId ?? Guid.NewGuid().ToString("N"),
            RequestPath: null,
            HttpMethod: null);
    }

    /// <summary>Builds an <see cref="AuditRequestInfo"/> from the current HttpContext.</summary>
    public class AuditContextProvider
    {
        private const string ActorKey = "Audit.Actor";

        private readonly IHttpContextAccessor _httpContext;

        public AuditContextProvider(IHttpContextAccessor httpContext) => _httpContext = httpContext;

        /// <summary>
        /// Names the acting user for a request that has no session yet.
        ///
        /// Signing in, verifying an OTP and resetting a password all write to the user's own row
        /// before the session exists. Without this those changes would be attributed to
        /// "Anonymous", which is technically true of the request and useless in an audit trail.
        /// Call it only once the identity is established — after the password has been verified,
        /// never from the credentials the caller merely claimed.
        /// </summary>
        public static void SetActor(HttpContext? http, int userId, string userName, string? role)
        {
            if (http == null) return;
            http.Items[ActorKey] = (userId.ToString(), userName, role);
        }

        public AuditRequestInfo Capture()
        {
            var http = _httpContext.HttpContext;
            if (http == null) return AuditRequestInfo.SystemContext();

            var userId = http.Session.IsAvailable ? http.Session.GetInt32("UserId")?.ToString() : null;
            // Prefer the full name: the trail identifies people, it does not greet them.
            var userName = http.Session.IsAvailable
                ? http.Session.GetString("UserFullName") ?? http.Session.GetString("UserName")
                : null;
            var userRole = http.Session.IsAvailable ? http.Session.GetString("UserRole") : null;

            // The session wins where there is one; the explicit actor covers the pre-session steps.
            if (string.IsNullOrEmpty(userId) &&
                http.Items.TryGetValue(ActorKey, out var actor) &&
                actor is ValueTuple<string, string, string?> resolved)
            {
                (userId, userName, userRole) = resolved;
            }

            return new AuditRequestInfo(
                UserId: userId ?? "Anonymous",
                UserName: string.IsNullOrEmpty(userName) ? "Anonymous" : userName,
                UserRole: userRole,
                IpAddress: ResolveIp(http),
                Device: DescribeDevice(http.Request.Headers.UserAgent),
                CorrelationId: http.TraceIdentifier,
                RequestPath: http.Request.Path.HasValue ? http.Request.Path.Value : null,
                HttpMethod: http.Request.Method);
        }

        private static string ResolveIp(HttpContext http)
        {
            // A proxy-supplied header is only trusted when forwarded headers are configured;
            // fall back to the socket address, normalising IPv6 loopback for readability.
            var ip = http.Connection.RemoteIpAddress?.ToString();
            if (ip == "::1") ip = "127.0.0.1";
            return string.IsNullOrEmpty(ip) ? "Unknown" : ip;
        }

        public static string DescribeDevice(string? userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "Unknown Device";

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
    }
}
