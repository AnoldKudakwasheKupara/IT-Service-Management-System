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
        private readonly EmailDispatcher _email;
        private readonly AuditService _auditService;
        private readonly SessionService _sessions;
        private readonly ConfigurationService _configService;
        private readonly AlertService _alerts;
        private readonly GeoLocationService _geo;
        private readonly ILogger<AccountController> _logger;
        private readonly IMemoryCache _cache;

        // Coarse per-IP backstop against scripted brute force (per-user lockout is the primary,
        // configurable mechanism). Generous so the configurable account lockout governs UX.
        private const int MaxIpAttempts = 15;
        private const int MaxMfaAttempts = 5;
        private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

        public AccountController(
            ApplicationDbContext context,
            EmailDispatcher email,
            AuditService auditService,
            SessionService sessions,
            ConfigurationService configService,
            AlertService alerts,
            GeoLocationService geo,
            ILogger<AccountController> logger,
            IMemoryCache cache)
        {
            _context = context;
            _email = email;
            _auditService = auditService;
            _sessions = sessions;
            _configService = configService;
            _alerts = alerts;
            _geo = geo;
            _logger = logger;
            _cache = cache;
        }

        // ── rate limiting ────────────────────────────────────────────────────────────

        private string ClientKey(string scope) =>
            $"{scope}:{HttpContext.Connection.RemoteIpAddress}";

        private bool IsRateLimited(string key) =>
            _cache.TryGetValue(key, out int count) && count >= MaxIpAttempts;

        private void RegisterAttempt(string key)
        {
            var count = _cache.TryGetValue(key, out int c) ? c : 0;
            _cache.Set(key, count + 1, AttemptWindow);
        }

        private void ResetAttempts(string key) => _cache.Remove(key);

        // ── login ────────────────────────────────────────────────────────────────────

        public IActionResult Login(bool expired = false, bool mfaFailed = false)
        {
            if (expired)
                ViewBag.Error = "Your session ended. Please sign in again.";
            else if (mfaFailed)
                ViewBag.Error = "Too many incorrect verification codes. Please sign in again.";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var throttleKey = ClientKey("login");
            var config = _configService.Get();

            if (IsRateLimited(throttleKey))
            {
                await _auditService.LogAsync("Login Blocked", "User", null,
                    $"Rate-limited login attempt for {email}");
                ViewBag.Error = "Too many failed attempts. Please try again in a few minutes.";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            // Account-locked check (per-user, configurable).
            if (user != null && user.IsLockedOut)
            {
                RegisterAttempt(throttleKey);
                await _auditService.LogAsync("Login Blocked", "User", user.Id,
                    $"Login attempt on locked account {user.Email}");
                ViewBag.Error = $"This account is locked due to repeated failed sign-ins. Try again after {user.LockoutEnd:h:mm tt}.";
                return View();
            }

            if (user == null || !user.IsActive || !PasswordHasher.VerifyPassword(password, user.PasswordHash))
            {
                RegisterAttempt(throttleKey);

                // Track per-user failed attempts and lock if the threshold is reached.
                if (user != null)
                {
                    user.FailedLoginCount++;

                    bool justLocked = false;
                    if (config.LockoutMaxFailedAttempts > 0 &&
                        user.FailedLoginCount >= config.LockoutMaxFailedAttempts)
                    {
                        user.LockoutEnd = DateTime.Now.AddMinutes(config.LockoutDurationMinutes);
                        user.FailedLoginCount = 0;
                        justLocked = true;
                    }

                    await _context.SaveChangesAsync();

                    await _auditService.LogAsync(justLocked ? "Account Locked" : "Login Failed",
                        "User", user.Id,
                        justLocked
                            ? $"Account {user.Email} locked after repeated failed sign-ins"
                            : $"Failed login ({user.FailedLoginCount}) for {user.Email}");

                    // Notify the user (locked → always; otherwise a one-time heads-up partway to lockout).
                    if (justLocked)
                    {
                        var resetLink = Url.Action("ForgotPassword", "Account", null, Request.Scheme)!;
                        await TrySendEmailAsync(user.Email, user.FirstName,
                            "Your account has been locked — Axis IT Operations",
                            EmailTemplates.AccountLocked(user.FirstName, user.LockoutEnd?.ToString("h:mm tt") ?? "", resetLink));

                        // Admin alert: account locked after multiple failed logins.
                        await _alerts.MultipleFailedLoginsAsync(user.Email, _sessions.CurrentIp(), _sessions.CurrentDevice());
                    }
                    else if (config.LockoutMaxFailedAttempts > 2 &&
                             user.FailedLoginCount == Math.Max(2, config.LockoutMaxFailedAttempts - 2))
                    {
                        await TrySendEmailAsync(user.Email, user.FirstName,
                            "Unusual sign-in attempts on your account — Axis IT Operations",
                            EmailTemplates.FailedLoginAttempt(user.FirstName, user.FailedLoginCount, _sessions.CurrentIp()));
                    }
                }
                else
                {
                    await _auditService.LogAsync("Login Failed", "User", null,
                        $"Failed login attempt for {email}");
                }

                ViewBag.Error = "Invalid credentials or account not activated.";
                return View();
            }

            ResetAttempts(throttleKey);

            // Transparently upgrade legacy plaintext passwords.
            if (!PasswordHasher.IsHashed(user.PasswordHash))
                user.PasswordHash = PasswordHasher.HashPassword(password);

            // Successful auth — reset lockout counters, stamp last login.
            user.FailedLoginCount = 0;
            user.LockoutEnd = null;
            if (string.IsNullOrEmpty(user.SecurityStamp))
                user.SecurityStamp = Guid.NewGuid().ToString("N");

            // MFA gate (email OTP) — challenge before completing the login.
            if (MfaApplies(user, config))
            {
                var code = GenerateOtp();
                user.MfaOtpCodeHash = PasswordHasher.HashPassword(code);
                user.MfaOtpExpiry = DateTime.Now.AddMinutes(config.MfaOtpValidityMinutes);
                await _context.SaveChangesAsync();

                HttpContext.Session.SetInt32(MfaPendingKey, user.Id);

                await TrySendEmailAsync(user.Email, user.FirstName,
                    "Your sign-in verification code — Axis IT Operations",
                    EmailTemplates.MfaCode(user.FirstName, code, config.MfaOtpValidityMinutes));

                await _auditService.LogAsync("MFA Challenge", "User", user.Id,
                    $"OTP issued to {user.Email}");

                return RedirectToAction(nameof(VerifyMfa));
            }

            user.LastLoginAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return await FinalizeLoginAsync(user);
        }

        // ── MFA challenge (email OTP) ──────────────────────────────────────────────────

        private const string MfaPendingKey = "MfaPendingUserId";

        [HttpGet]
        public async Task<IActionResult> VerifyMfa()
        {
            var pendingId = HttpContext.Session.GetInt32(MfaPendingKey);
            if (pendingId == null) return RedirectToAction(nameof(Login));

            var user = await _context.Users.FindAsync(pendingId.Value);
            if (user == null) { HttpContext.Session.Remove(MfaPendingKey); return RedirectToAction(nameof(Login)); }

            ViewBag.MaskedEmail = MaskEmail(user.Email);
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyMfa(string code)
        {
            var pendingId = HttpContext.Session.GetInt32(MfaPendingKey);
            if (pendingId == null) return RedirectToAction(nameof(Login));

            var user = await _context.Users.FindAsync(pendingId.Value);
            if (user == null) { HttpContext.Session.Remove(MfaPendingKey); return RedirectToAction(nameof(Login)); }

            ViewBag.MaskedEmail = MaskEmail(user.Email);

            if (user.MfaOtpExpiry == null || user.MfaOtpExpiry < DateTime.Now)
            {
                ViewBag.Error = "That code has expired. Please request a new one.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(code) || !PasswordHasher.VerifyPassword(code.Trim(), user.MfaOtpCodeHash))
            {
                // Cap failed OTP attempts to defeat brute force of the 6-digit code.
                var attemptKey = $"mfa-attempts:{user.Id}";
                var attempts = (_cache.TryGetValue(attemptKey, out int a) ? a : 0) + 1;
                _cache.Set(attemptKey, attempts, TimeSpan.FromMinutes(15));

                if (attempts >= MaxMfaAttempts)
                {
                    // Invalidate the code and end the challenge — user must sign in again.
                    user.MfaOtpCodeHash = null;
                    user.MfaOtpExpiry = null;
                    await _context.SaveChangesAsync();
                    _cache.Remove(attemptKey);
                    HttpContext.Session.Remove(MfaPendingKey);
                    await _auditService.LogAsync("MFA Locked", "User", user.Id,
                        $"Too many invalid OTP attempts for {user.Email}");
                    TempData["Success"] = null;
                    ViewBag.Error = null;
                    return RedirectToAction(nameof(Login), new { mfaFailed = true });
                }

                await _auditService.LogAsync("MFA Failed", "User", user.Id,
                    $"Invalid OTP ({attempts}/{MaxMfaAttempts}) for {user.Email}");
                ViewBag.Error = $"Incorrect code. {MaxMfaAttempts - attempts} attempt(s) left.";
                return View();
            }

            // OTP verified — clear it and complete the login.
            user.MfaOtpCodeHash = null;
            user.MfaOtpExpiry = null;
            user.LastLoginAt = DateTime.Now;
            await _context.SaveChangesAsync();

            _cache.Remove($"mfa-attempts:{user.Id}");
            HttpContext.Session.Remove(MfaPendingKey);
            await _auditService.LogAsync("MFA Verified", "User", user.Id, $"OTP verified for {user.Email}");

            return await FinalizeLoginAsync(user);
        }

        [HttpPost]
        public async Task<IActionResult> ResendMfa()
        {
            var pendingId = HttpContext.Session.GetInt32(MfaPendingKey);
            if (pendingId == null) return RedirectToAction(nameof(Login));

            var user = await _context.Users.FindAsync(pendingId.Value);
            if (user == null) { HttpContext.Session.Remove(MfaPendingKey); return RedirectToAction(nameof(Login)); }

            // Throttle resends to prevent OTP flooding / window extension.
            var resendKey = $"mfa-resend:{user.Id}";
            if (_cache.TryGetValue(resendKey, out _))
            {
                TempData["Success"] = "A code was just sent. Please wait a moment before requesting another.";
                return RedirectToAction(nameof(VerifyMfa));
            }
            _cache.Set(resendKey, true, TimeSpan.FromSeconds(60));

            var config = _configService.Get();
            var newCode = GenerateOtp();
            user.MfaOtpCodeHash = PasswordHasher.HashPassword(newCode);
            user.MfaOtpExpiry = DateTime.Now.AddMinutes(config.MfaOtpValidityMinutes);
            await _context.SaveChangesAsync();

            await TrySendEmailAsync(user.Email, user.FirstName,
                "Your sign-in verification code — Axis IT Operations",
                EmailTemplates.MfaCode(user.FirstName, newCode, config.MfaOtpValidityMinutes));

            TempData["Success"] = "A new code has been sent to your email.";
            return RedirectToAction(nameof(VerifyMfa));
        }

        // Completes a verified login: opens a tracked session and sends a new-device alert if needed.
        private async Task<IActionResult> FinalizeLoginAsync(User user)
        {
            bool knownDevice = await _sessions.IsKnownDeviceAsync(user.Id);

            // Capture the locations this user has signed in from before (for suspicious-location detection).
            var priorLocations = await _context.UserSessions
                .Where(s => s.UserId == user.Id && s.Location != null)
                .Select(s => s.Location!)
                .Distinct()
                .ToListAsync();

            await _sessions.StartSessionAsync(user);
            await _auditService.LogAsync("Login", "User", user.Id, $"User {user.Email} logged in");

            if (!knownDevice)
            {
                await _auditService.LogAsync("New Device Login", "User", user.Id,
                    $"{user.Email} signed in from a new device ({_sessions.CurrentDevice()})");
                await TrySendEmailAsync(user.Email, user.FirstName,
                    "New sign-in to your account — Axis IT Operations",
                    EmailTemplates.NewDeviceLogin(user.FirstName, _sessions.CurrentDevice(),
                        _sessions.CurrentIp(), DateTime.Now.ToString("MMM dd, yyyy h:mm tt")));
            }

            // Suspicious location: a resolvable, previously-unseen location for this user.
            var ip = _sessions.CurrentIp();
            var location = await _geo.ResolveAsync(ip);
            if (location != "Localhost" && location != "Unknown" &&
                priorLocations.Any() && !priorLocations.Contains(location))
            {
                await _alerts.SuspiciousLocationAsync(user.Email, location, ip, _sessions.CurrentDevice());
            }

            return RedirectToAction("Index", "Home");
        }

        private bool MfaApplies(User user, Models.AppConfiguration config)
        {
            if (!config.MfaEnabled) return false;
            bool isAdmin = user.Role == UserRole.Admin || user.Role == UserRole.SystemsAdmin;
            return user.MfaEnabled || (config.MfaRequiredForAdmins && isAdmin);
        }

        private static string GenerateOtp() =>
            System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        private static string MaskEmail(string email)
        {
            var at = email.IndexOf('@');
            if (at <= 1) return email;
            return email[0] + new string('•', Math.Min(6, at - 1)) + email[(at - 1)..];
        }

        // ── logout ───────────────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId != null)
            {
                await _sessions.RevokeCurrentAsync("User logged out");
                await _auditService.LogAsync("Logout", "User", userId, "User logged out");
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // Ends every active session for the current user (e.g. after suspected compromise).
        [HttpPost]
        public async Task<IActionResult> LogoutAllDevices()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            // Rotate the security stamp so all existing sessions (including this one) are invalidated.
            var user = await _context.Users.FindAsync(userId.Value);
            if (user != null)
            {
                user.SecurityStamp = Guid.NewGuid().ToString("N");
                await _context.SaveChangesAsync();
                await _sessions.RevokeAllAsync(user.Id, "User logged out from all devices");
                await _auditService.LogAsync("Logout All Devices", "User", user.Id,
                    $"{user.Email} logged out from all devices");
            }

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

            if (!IsValidPassword(password, out var policyError))
            {
                ViewBag.Error = policyError;
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
            user.PasswordChangedAt = DateTime.Now;
            user.FailedLoginCount = 0;
            user.LockoutEnd = null;
            // Rotate the security stamp so any existing sessions are invalidated on next request.
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await _context.SaveChangesAsync();

            // Revoke any tracked sessions for this user (password just changed).
            await _sessions.RevokeAllAsync(user.Id, "Password changed");

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

        private bool IsValidPassword(string? password, out string error)
        {
            var policy = _configService.Get();
            var problems = new List<string>();

            if (string.IsNullOrEmpty(password) || password.Length < policy.PasswordMinLength)
                problems.Add($"at least {policy.PasswordMinLength} characters");
            if (policy.PasswordRequireUppercase && !(password?.Any(char.IsUpper) ?? false))
                problems.Add("an uppercase letter");
            if (policy.PasswordRequireLowercase && !(password?.Any(char.IsLower) ?? false))
                problems.Add("a lowercase letter");
            if (policy.PasswordRequireDigit && !(password?.Any(char.IsDigit) ?? false))
                problems.Add("a number");
            if (policy.PasswordRequireSpecial && !(password?.Any(ch => !char.IsLetterOrDigit(ch)) ?? false))
                problems.Add("a special character");

            error = problems.Count == 0
                ? string.Empty
                : "Password must contain " + string.Join(", ", problems) + ".";
            return problems.Count == 0;
        }

        // Queues the email on the background worker (its own scope), so a slow/failing SMTP
        // server never blocks or breaks the request. Returns immediately.
        private Task TrySendEmailAsync(string toEmail, string toName, string subject, string body)
        {
            _email.Queue(toEmail, toName, subject, body);
            return Task.CompletedTask;
        }

        protected UserRole? GetCurrentUserRole()
        {
            var role = HttpContext.Session.GetString("UserRole");
            return Enum.TryParse<UserRole>(role, out var userRole) ? userRole : null;
        }
    }
}
