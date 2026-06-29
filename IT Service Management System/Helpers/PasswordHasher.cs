using System.Security.Cryptography;

namespace IT_Service_Management_System.Helpers
{
    /// <summary>
    /// Salted PBKDF2 (SHA-256) password hashing. Stored format:
    /// PBKDF2$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;
    /// </summary>
    public static class PasswordHasher
    {
        private const string Prefix = "PBKDF2";
        private const int Iterations = 100_000;
        private const int SaltSize = 16;   // 128-bit
        private const int KeySize = 32;    // 256-bit

        public static string HashPassword(string password)
        {
            ArgumentException.ThrowIfNullOrEmpty(password);

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        /// <summary>True when the stored value is a PBKDF2 hash produced by this class.</summary>
        public static bool IsHashed(string? stored) =>
            !string.IsNullOrEmpty(stored) && stored.StartsWith(Prefix + "$", StringComparison.Ordinal);

        /// <summary>
        /// Verifies a password against a stored value. Falls back to a plaintext
        /// comparison for legacy (un-hashed) values so existing accounts keep working;
        /// callers should re-hash on a successful legacy match.
        /// </summary>
        public static bool VerifyPassword(string password, string? stored)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
                return false;

            if (!IsHashed(stored))
            {
                // Legacy plaintext value — constant-time compare.
                return CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(password),
                    System.Text.Encoding.UTF8.GetBytes(stored));
            }

            var parts = stored.Split('$', 4);
            if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
                return false;

            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}
