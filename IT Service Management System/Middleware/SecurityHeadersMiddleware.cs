namespace IT_Service_Management_System.Middleware
{
    /// <summary>
    /// Adds the baseline security response headers recommended by the OWASP Secure Headers
    /// Project to every response: clickjacking, MIME-sniffing, referrer-leakage, and a
    /// starter Content-Security-Policy that locks down the dangerous sinks (framing, object
    /// embedding, base-uri, form targets) while staying permissive enough for the app's
    /// current inline scripts/styles and CDN assets.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

        // Starter CSP. Scripts/styles/fonts/images stay broad (https:, inline, data:) so the
        // current CDN-hosted assets and inline handlers keep working, but the genuinely
        // dangerous directives are locked down:
        //   frame-ancestors 'none'  → cannot be framed (clickjacking) — supersedes X-Frame-Options
        //   object-src 'none'       → no <object>/<embed>/Flash sinks
        //   base-uri 'self'         → attacker cannot rewrite the document base
        //   form-action 'self'      → forms can only post back to this origin
        // Tighten script-src/style-src to explicit hosts or nonces once inline usage is removed.
        private const string Csp =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https:; " +
            "style-src 'self' 'unsafe-inline' https:; " +
            "img-src 'self' data: https:; " +
            "font-src 'self' data: https:; " +
            "connect-src 'self' https: wss:; " +
            "frame-ancestors 'none'; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'";

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            // Set on the way out, before the response body is written.
            context.Response.OnStarting(() =>
            {
                headers["X-Frame-Options"] = "DENY";
                headers["X-Content-Type-Options"] = "nosniff";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["X-Permitted-Cross-Domain-Policies"] = "none";
                if (!headers.ContainsKey("Content-Security-Policy"))
                    headers["Content-Security-Policy"] = Csp;
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
