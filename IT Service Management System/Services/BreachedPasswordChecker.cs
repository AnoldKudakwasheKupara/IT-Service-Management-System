using System.Security.Cryptography;
using System.Text;

namespace IT_Service_Management_System.Services
{
    /// <summary>
    /// Screens candidate passwords against the Have I Been Pwned "Pwned Passwords" corpus
    /// using the k-anonymity range API — only the first 5 characters of the SHA-1 hash ever
    /// leave the server, so the full password (and even its full hash) is never disclosed.
    ///
    /// Fails OPEN: if the API is unreachable (offline dev box, outage, timeout) the password
    /// is allowed, so breach screening can never lock legitimate users out of setting a password.
    /// This aligns with NIST SP 800-63B §5.1.1.2 (screen secrets against known-compromised lists).
    /// </summary>
    public class BreachedPasswordChecker
    {
        private readonly HttpClient _http;
        private readonly ILogger<BreachedPasswordChecker> _logger;

        public BreachedPasswordChecker(HttpClient http, ILogger<BreachedPasswordChecker> logger)
        {
            _http = http;
            _logger = logger;
        }

        /// <summary>True when the password appears in a known breach corpus. False on any error (fail-open).</summary>
        public async Task<bool> IsBreachedAsync(string password, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(password)) return false;

            try
            {
                // SHA-1 the password (this hash never leaves the process in full).
                var sha1 = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
                var prefix = sha1[..5];
                var suffix = sha1[5..];

                // Only the 5-char prefix is sent. "Add-Padding" hides the real result-set size.
                using var req = new HttpRequestMessage(HttpMethod.Get,
                    $"https://api.pwnedpasswords.com/range/{prefix}");
                req.Headers.Add("Add-Padding", "true");

                using var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) return false;

                var body = await resp.Content.ReadAsStringAsync(ct);

                // Each line is "HASHSUFFIX:count". A count > 0 means the password is breached.
                foreach (var line in body.Split('\n'))
                {
                    var sep = line.IndexOf(':');
                    if (sep <= 0) continue;

                    if (line.AsSpan(0, sep).Trim().Equals(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        var countText = line[(sep + 1)..].Trim();
                        return int.TryParse(countText, out var count) && count > 0;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                // Fail-open: never block a password change because the breach service is down.
                _logger.LogWarning(ex, "Breached-password check failed; allowing the password (fail-open).");
                return false;
            }
        }
    }
}
