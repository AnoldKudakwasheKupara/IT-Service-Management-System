using System.Text.Json;
using IT_Service_Management_System.Models;

namespace IT_Service_Management_System.Helpers
{
    /// <summary>Verifies and consumes one-time MFA recovery codes stored (hashed) on the user.</summary>
    public static class MfaRecovery
    {
        /// <summary>
        /// If <paramref name="code"/> matches one of the user's unused recovery codes, removes it
        /// (single-use) and returns true. The caller must persist the change.
        /// </summary>
        public static bool TryConsume(User user, string? code)
        {
            if (string.IsNullOrEmpty(user.MfaRecoveryCodes) || string.IsNullOrWhiteSpace(code)) return false;

            List<string>? hashes;
            try { hashes = JsonSerializer.Deserialize<List<string>>(user.MfaRecoveryCodes); }
            catch { return false; }
            if (hashes == null || hashes.Count == 0) return false;

            var normalized = Normalize(code);
            var idx = hashes.FindIndex(h => PasswordHasher.VerifyPassword(normalized, h));
            if (idx < 0) return false;

            hashes.RemoveAt(idx);
            user.MfaRecoveryCodes = JsonSerializer.Serialize(hashes);
            return true;
        }

        // Recovery codes are issued as "XXXX-XXXX" uppercase; accept them with or without the dash/spaces.
        private static string Normalize(string code)
        {
            var c = code.Trim().ToUpperInvariant().Replace(" ", "");
            if (c.Length == 8 && !c.Contains('-')) c = c[..4] + "-" + c[4..];
            return c;
        }
    }
}
