using System.Security.Cryptography;
using System.Text;

namespace IT_Service_Management_System.Helpers
{
    /// <summary>
    /// RFC 6238 TOTP (time-based one-time password) for authenticator apps — no external packages.
    /// 30-second step, 6 digits, HMAC-SHA1, which is what Google/Microsoft Authenticator, Authy, etc. use.
    /// </summary>
    public static class TotpAuthenticator
    {
        private const int Digits = 6;
        private const int PeriodSeconds = 30;
        private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Generates a new random Base32 secret (160-bit) for enrollment.</summary>
        public static string GenerateSecret()
        {
            var bytes = RandomNumberGenerator.GetBytes(20);
            return Base32Encode(bytes);
        }

        /// <summary>The otpauth:// URI that an authenticator app scans as a QR code.</summary>
        public static string BuildOtpAuthUri(string issuer, string account, string base32Secret)
        {
            var iss = Uri.EscapeDataString(issuer);
            var acc = Uri.EscapeDataString(account);
            return $"otpauth://totp/{iss}:{acc}?secret={base32Secret}&issuer={iss}&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}";
        }

        /// <summary>Validates a code against the secret, tolerating ±<paramref name="window"/> time steps for clock drift.</summary>
        public static bool Verify(string? base32Secret, string? code, int window = 1)
        {
            if (string.IsNullOrWhiteSpace(base32Secret) || string.IsNullOrWhiteSpace(code)) return false;
            code = code.Trim().Replace(" ", "");
            if (code.Length != Digits || !code.All(char.IsDigit)) return false;

            byte[] key;
            try { key = Base32Decode(base32Secret); } catch { return false; }

            var counter = (long)(DateTime.UtcNow - Epoch).TotalSeconds / PeriodSeconds;
            for (var i = -window; i <= window; i++)
            {
                if (Compute(key, counter + i) == code) return true;
            }
            return false;
        }

        private static string Compute(byte[] key, long counter)
        {
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

            using var hmac = new HMACSHA1(key);
            var hash = hmac.ComputeHash(counterBytes);

            var offset = hash[^1] & 0x0f;
            var binary = ((hash[offset] & 0x7f) << 24)
                       | ((hash[offset + 1] & 0xff) << 16)
                       | ((hash[offset + 2] & 0xff) << 8)
                       | (hash[offset + 3] & 0xff);
            var otp = binary % (int)Math.Pow(10, Digits);
            return otp.ToString().PadLeft(Digits, '0');
        }

        /// <summary>A short, human-friendly one-time recovery code, e.g. "K3F9-Q7W2".</summary>
        public static string GenerateRecoveryCode()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no ambiguous chars
            var chars = new char[8];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            return new string(chars, 0, 4) + "-" + new string(chars, 4, 4);
        }

        // ── Base32 (RFC 4648, no padding) ───────────────────────────────────────────
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        public static string Base32Encode(byte[] data)
        {
            var sb = new StringBuilder((data.Length * 8 + 4) / 5);
            int buffer = 0, bitsLeft = 0;
            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    bitsLeft -= 5;
                    sb.Append(Base32Alphabet[(buffer >> bitsLeft) & 31]);
                }
            }
            if (bitsLeft > 0)
                sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 31]);
            return sb.ToString();
        }

        public static byte[] Base32Decode(string input)
        {
            input = input.Trim().TrimEnd('=').ToUpperInvariant().Replace(" ", "");
            var output = new List<byte>(input.Length * 5 / 8);
            int buffer = 0, bitsLeft = 0;
            foreach (var c in input)
            {
                var val = Base32Alphabet.IndexOf(c);
                if (val < 0) throw new FormatException("Invalid Base32 character.");
                buffer = (buffer << 5) | val;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    bitsLeft -= 8;
                    output.Add((byte)((buffer >> bitsLeft) & 0xff));
                }
            }
            return output.ToArray();
        }
    }
}
