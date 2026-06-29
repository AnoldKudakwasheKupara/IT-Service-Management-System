using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly AuditService _auditService;
        private readonly ILogger<AccountController> _logger;
        private readonly IMemoryCache _cache;

        private const int MaxAttempts = 5;
        private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

        public AccountController(
            ApplicationDbContext context,
            EmailService emailService,
            AuditService auditService,
            ILogger<AccountController> logger,
            IMemoryCache cache)
        {
            _context = context;
            _emailService = emailService;
            _auditService = auditService;
            _logger = logger;
            _cache = cache;
        }

        // ── rate limiting ────────────────────────────────────────────────────────────

        private string ClientKey(string scope) =>
            $"{scope}:{HttpContext.Connection.RemoteIpAddress}";

        private bool IsRateLimited(string key) =>
            _cache.TryGetValue(key, out int count) && count >= MaxAttempts;

        private void RegisterAttempt(string key)
        {
            var count = _cache.TryGetValue(key, out int c) ? c : 0;
            _cache.Set(key, count + 1, AttemptWindow);
        }

        private void ResetAttempts(string key) => _cache.Remove(key);

        // ── login ────────────────────────────────────────────────────────────────────

        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var throttleKey = ClientKey("login");

            if (IsRateLimited(throttleKey))
            {
                await _auditService.LogAsync("Login Blocked", "User", null,
                    $"Rate-limited login attempt for {email}");
                ViewBag.Error = "Too many failed attempts. Please try again in a few minutes.";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || !user.IsActive || !PasswordHasher.VerifyPassword(password, user.PasswordHash))
            {
                RegisterAttempt(throttleKey);
                await _auditService.LogAsync("Login Failed", "User", null,
                    $"Failed login attempt for {email}");
                ViewBag.Error = "Invalid credentials or account not activated.";
                return View();
            }

            ResetAttempts(throttleKey);

            // Transparently upgrade legacy plaintext passwords.
            if (!PasswordHasher.IsHashed(user.PasswordHash))
            {
                user.PasswordHash = PasswordHasher.HashPassword(password);
                await _context.SaveChangesAsync();
            }

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.FirstName);
            HttpContext.Session.SetString("UserRole", user.Role.ToString());

            await _auditService.LogAsync("Login", "User", user.Id, $"User {user.Email} logged in");

            return RedirectToAction("Index", "Home");
        }

        // ── logout ───────────────────────────────────────────────────────────────────

        public async Task<IActionResult> Logout()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId != null)
                await _auditService.LogAsync("Logout", "User", userId, "User logged out");

            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // ── set / reset password ─────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> SetPassword(string? token)
        {
            if (string.IsNullOrEmpty(token))
            {
                ViewBag.TokenError = "No reset token was provided. Please use the link from your email.";
                return View();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.ResetToken == token);

            if (user == null)
            {
                ViewBag.TokenError = "This link is invalid or has already been used. Please request a new one.";
                return View();
            }

            if (user.TokenExpiry == null || user.TokenExpiry < DateTime.Now)
            {
                ViewBag.TokenError = "This link has expired. Please request a new password reset link.";
                return View();
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SetPassword(string token, string password, string confirmPassword)
        {
            if (string.IsNullOrEmpty(token))
            {
                ViewBag.TokenError = "Invalid token. Please use the link from your email.";
                return View();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.ResetToken == token);

            if (user == null)
            {
                ViewBag.TokenError = "This link is invalid or has already been used.";
                return View();
            }

            if (user.TokenExpiry == null || user.TokenExpiry < DateTime.Now)
            {
                ViewBag.TokenError = "This link has expired. Please request a new password reset link.";
                return View();
            }

            if (!IsValidPassword(password))
            {
                ViewBag.Error = "Password does not meet the requirements. It must be at least 8 characters and include uppercase, lowercase, and a special character.";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                return View();
            }

            bool isNewAccount = !user.IsActive;

            user.PasswordHash = PasswordHasher.HashPassword(password);
            user.IsActive = true;
            user.ResetToken = null;
            user.TokenExpiry = null;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Set Password", "User", user.Id,
                isNewAccount ? "Account activated — first password set" : "Password reset via email link");

            // Send confirmation email. TrySendEmailAsync swallows/logs errors, so awaiting it
            // guarantees delivery is attempted without ever breaking the redirect.
            var loginUrl = Url.Action("Login", "Account", null, Request.Scheme)!;
            await TrySendEmailAsync(
                user.Email, user.FirstName,
                isNewAccount ? "Your account is now active — Axis IT Operations" : "Your password has been changed — Axis IT Operations",
                EmailTemplates.PasswordChanged(user.FirstName, loginUrl, isNewAccount));

            TempData["Success"] = isNewAccount
                ? "Account activated successfully. You can now log in."
                : "Your password has been reset. You can now log in.";

            return RedirectToAction("Login", "Account");
        }

        // ── forgot password ──────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var throttleKey = ClientKey("forgot");

            if (IsRateLimited(throttleKey))
            {
                ViewBag.Message = "If that email exists in our system, a reset link has been sent.";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            // Always show the same message regardless of whether the email exists (prevents enumeration).
            if (user == null)
            {
                RegisterAttempt(throttleKey);
                ViewBag.Message = "If that email exists in our system, a reset link has been sent.";
                return View();
            }

            user.ResetToken = Guid.NewGuid().ToString("N");
            user.TokenExpiry = DateTime.Now.Add(TokenLifetime);   // 24 hours
            await _context.SaveChangesAsync();

            RegisterAttempt(throttleKey);
            await _auditService.LogAsync("Forgot Password", "User", user.Id, "Password reset requested");

            var resetLink = Url.Action("SetPassword", "Account",
                new { token = user.ResetToken }, Request.Scheme)!;

            await TrySendEmailAsync(
                user.Email, user.FirstName,
                "Reset your password — Axis IT Operations",
                EmailTemplates.PasswordReset(user.FirstName, resetLink));

            ViewBag.Message = "If that email exists in our system, a reset link has been sent.";
            return View();
        }

        // ── helpers ──────────────────────────────────────────────────────────────────

        private static bool IsValidPassword(string? password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return false;

            return password.Any(char.IsUpper)
                && password.Any(char.IsLower)
                && password.Any(ch => !char.IsLetterOrDigit(ch));
        }

        private async Task TrySendEmailAsync(string toEmail, string toName, string subject, string body)
        {
            try
            {
                await _emailService.SendEmailAsync(toEmail, toName, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email} — subject: {Subject}", toEmail, subject);
            }
        }

        protected UserRole? GetCurrentUserRole()
        {
            var role = HttpContext.Session.GetString("UserRole");
            return Enum.TryParse<UserRole>(role, out var userRole) ? userRole : null;
        }
    }
}
