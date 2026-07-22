using System.Security.Cryptography;
using System.Text;

namespace IT_Service_Management_System.Helpers
{
    /// <summary>
    /// One-way hashing for high-entropy, single-use secrets such as password-reset and
    /// account-activation tokens. Only the hash is persisted; the raw token lives only in
    /// the email link, so a database leak never exposes usable tokens.
    ///
    /// A plain SHA-256 is appropriate here (unlike passwords, which use PBKDF2): the token
    /// is a 122-bit random GUID, so it is not brute-forceable and needs no salt or stretching.
    /// </summary>
    public static class TokenHasher
    {
        /// <summary>Returns the uppercase-hex SHA-256 of the token (64 chars).</summary>
        public static string Hash(string token)
        {
            ArgumentException.ThrowIfNullOrEmpty(token);
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }
    }
}
