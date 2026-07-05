using System.Text.Json;
using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Self-service two-factor enrollment: any signed-in user chooses their preferred second factor
    /// (authenticator app / email OTP), enrolls, and manages recovery codes. Sign-in verification of
    /// these methods lives in <see cref="AccountController"/>.
    /// </summary>
    // No [RoleAuthorize] → any authenticated user (the global session filter still requires login).
    public class MfaController : Controller
    {
        private const string Issuer = "Axis IT Operations";
        private const string PendingSecretKey = "PendingTotpSecret";

        private readonly ApplicationDbContext _db;
        public MfaController(ApplicationDbContext db) => _db = db;

        private int? Uid => HttpContext.Session.GetInt32("UserId");

        private async Task<User?> CurrentUserAsync() =>
            Uid is int id ? await _db.Users.FirstOrDefaultAsync(u => u.Id == id) : null;

        public async Task<IActionResult> Index()
        {
            var user = await CurrentUserAsync();
            if (user == null) return RedirectToAction("Login", "Account");
            ViewBag.RecoveryRemaining = CountRecoveryCodes(user.MfaRecoveryCodes);
            return View(user);
        }

        // ── Authenticator-app (TOTP) enrollment ────────────────────────────────────────
        public async Task<IActionResult> SetupAuthenticator()
        {
            var user = await CurrentUserAsync();
            if (user == null) return RedirectToAction("Login", "Account");

            // A fresh pending secret is generated and held in session until the user confirms a code.
            var secret = TotpAuthenticator.GenerateSecret();
            HttpContext.Session.SetString(PendingSecretKey, secret);

            ViewBag.Secret = secret;
            ViewBag.OtpAuthUri = TotpAuthenticator.BuildOtpAuthUri(Issuer, user.Email, secret);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableAuthenticator(string code)
        {
            var user = await CurrentUserAsync();
            if (user == null) return RedirectToAction("Login", "Account");

            var secret = HttpContext.Session.GetString(PendingSecretKey);
            if (string.IsNullOrEmpty(secret))
            { TempData["Error"] = "Your setup session expired. Please start again."; return RedirectToAction(nameof(SetupAuthenticator)); }

            if (!TotpAuthenticator.Verify(secret, code))
            {
                ViewBag.Secret = secret;
                ViewBag.OtpAuthUri = TotpAuthenticator.BuildOtpAuthUri(Issuer, user.Email, secret);
                ViewBag.Error = "That code didn't match. Make sure your device clock is correct and try the current code.";
                return View(nameof(SetupAuthenticator));
            }

            user.TotpSecret = secret;
            user.MfaMethod = MfaMethod.Authenticator;
            user.MfaEnabled = true;
            var plainCodes = SetRecoveryCodes(user);
            await _db.SaveChangesAsync();
            HttpContext.Session.Remove(PendingSecretKey);

            TempData["RecoveryCodes"] = JsonSerializer.Serialize(plainCodes);
            TempData["Success"] = "Authenticator app enabled. Save your recovery codes below.";
            return RedirectToAction(nameof(RecoveryCodes));
        }

        // ── Email-OTP enrollment ────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableEmail()
        {
            var user = await CurrentUserAsync();
            if (user == null) return RedirectToAction("Login", "Account");

            user.MfaMethod = MfaMethod.Email;
            user.MfaEnabled = true;
            user.TotpSecret = null;                 // switching away from authenticator
            var plainCodes = SetRecoveryCodes(user);
            await _db.SaveChangesAsync();

            TempData["RecoveryCodes"] = JsonSerializer.Serialize(plainCodes);
            TempData["Success"] = "Email verification enabled. Save your recovery codes below.";
            return RedirectToAction(nameof(RecoveryCodes));
        }

        // ── disable / recovery codes ────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disable()
        {
            var user = await CurrentUserAsync();
            if (user == null) return RedirectToAction("Login", "Account");

            user.MfaEnabled = false;
            user.MfaMethod = MfaMethod.None;
            user.TotpSecret = null;
            user.MfaRecoveryCodes = null;
            user.MfaOtpCodeHash = null;
            user.MfaOtpExpiry = null;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Two-factor authentication disabled for your account.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateRecoveryCodes()
        {
            var user = await CurrentUserAsync();
            if (user == null) return RedirectToAction("Login", "Account");
            if (!user.MfaEnabled) { TempData["Error"] = "Enable two-factor first."; return RedirectToAction(nameof(Index)); }

            var plainCodes = SetRecoveryCodes(user);
            await _db.SaveChangesAsync();
            TempData["RecoveryCodes"] = JsonSerializer.Serialize(plainCodes);
            TempData["Success"] = "New recovery codes generated. Your old codes no longer work.";
            return RedirectToAction(nameof(RecoveryCodes));
        }

        public IActionResult RecoveryCodes()
        {
            if (TempData["RecoveryCodes"] is not string json) return RedirectToAction(nameof(Index));
            TempData.Keep("RecoveryCodes");     // survive a refresh; cleared once they leave
            var codes = JsonSerializer.Deserialize<List<string>>(json) ?? new();
            return View(codes);
        }

        // ── helpers ─────────────────────────────────────────────────────────────────────
        private static List<string> SetRecoveryCodes(User user)
        {
            var plain = Enumerable.Range(0, 10).Select(_ => TotpAuthenticator.GenerateRecoveryCode()).ToList();
            var hashed = plain.Select(PasswordHasher.HashPassword).ToList();
            user.MfaRecoveryCodes = JsonSerializer.Serialize(hashed);
            return plain;
        }

        private static int CountRecoveryCodes(string? json)
        {
            if (string.IsNullOrEmpty(json)) return 0;
            try { return JsonSerializer.Deserialize<List<string>>(json)?.Count ?? 0; }
            catch { return 0; }
        }
    }
}
