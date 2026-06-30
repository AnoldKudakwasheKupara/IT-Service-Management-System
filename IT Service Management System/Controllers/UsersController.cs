using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailDispatcher _email;
        private readonly AuditService _auditService;
        private readonly AlertService _alerts;
        private readonly ILogger<UsersController> _logger;

        public UsersController(ApplicationDbContext context, EmailDispatcher email, AuditService auditService, AlertService alerts, ILogger<UsersController> logger)
        {
            _context = context;
            _email = email;
            _auditService = auditService;
            _alerts = alerts;
            _logger = logger;
        }

        private static bool IsPrivileged(Ticket.UserRole role) =>
            role == Ticket.UserRole.Admin || role == Ticket.UserRole.SystemsAdmin;

        // Queues the email on the background worker so SMTP latency never blocks the request.
        private Task TrySendEmailAsync(string toEmail, string toName, string subject, string body)
        {
            _email.Queue(toEmail, toName, subject, body);
            return Task.CompletedTask;
        }

        // 🔒 AUTH + ROLE CHECK
        private IActionResult? CheckAccess()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Admin" && role != "SystemsAdmin")
                return RedirectToAction("AccessDenied", "Home"); // Forbid() throws under session auth

            return null;
        }

        // 🔹 LIST USERS
        public async Task<IActionResult> Index(int page = 1, string? q = null)
        {
            var access = CheckAccess();
            if (access != null)
                return access;

            // Summary counts over the full (unfiltered) set so the stat cards
            // reflect ALL users regardless of the current search or page.
            var baseQuery = _context.Users.AsQueryable();
            ViewBag.TotalUsers = await baseQuery.CountAsync();
            ViewBag.AdminUsers = await baseQuery.CountAsync(u => u.Role == Ticket.UserRole.Admin);
            ViewBag.RegularUsers = await baseQuery.CountAsync(u => u.Role == Ticket.UserRole.Employee);
            ViewBag.NewUsers = await baseQuery.CountAsync(u => u.CreatedAt >= DateTime.Now.AddDays(-30));

            // Declared as IQueryable<User> so the Includes don't lock us into IIncludableQueryable
            // (which can't be reassigned by .Where below).
            IQueryable<User> query = _context.Users
                .Include(u => u.Department)
                    .ThenInclude(d => d!.Hod)
                .Include(u => u.Supervisor);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(u =>
                    u.FirstName.Contains(q) || u.LastName.Contains(q) || u.Email.Contains(q));

            query = query
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName);

            var (items, paging) = await query.PageAsync(page);
            ViewBag.Paging = paging;
            ViewBag.Search = q;

            return View(items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var access = CheckAccess();
            if (access != null)
                return access;

            var user = await _context.Users
                .Include(u => u.Department)
                    .ThenInclude(d => d!.Hod)
                .Include(u => u.Supervisor)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound();

            // Assets currently assigned to this user.
            ViewBag.AssignedAssets = await _context.Assets
                .Where(a => a.UserId == id)
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            // Asset-history events involving this user (issued to / returned by, etc.).
            ViewBag.AssetHistory = await _context.AssetHistories
                .Include(h => h.Asset)
                .Where(h => h.UserId == id)
                .OrderByDescending(h => h.Date)
                .ToListAsync();

            return View(user);
        }

        public IActionResult Create()
        {
            var access = CheckAccess();
            if (access != null)
                return access;

            ViewBag.Departments = _context.Departments
                .OrderBy(d => d.Name)
                .ToList();

            ViewBag.Supervisors = _context.Users
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToList();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user)
        {
            var access = CheckAccess();
            if (access != null)
                return access;

            if (!ModelState.IsValid)
            {
                ViewBag.Departments = _context.Departments
                    .OrderBy(d => d.Name)
                    .ToList();

                ViewBag.Supervisors = _context.Users
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ToList();

                return View(user);
            }

            var existingUser = await _context.Users
                .AnyAsync(u => u.Email == user.Email);

            if (existingUser)
            {
                ModelState.AddModelError("Email", "This email is already registered.");

                ViewBag.Departments = _context.Departments
                    .OrderBy(d => d.Name)
                    .ToList();

                ViewBag.Supervisors = _context.Users
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ToList();

                return View(user);
            }

            user.CreatedAt = DateTime.Now;
            user.IsActive = false;
            user.ResetToken = Guid.NewGuid().ToString("N");
            user.TokenExpiry = DateTime.Now.AddHours(24);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Created", "User", user.Id, $"User {user.Email} created");

            // Admin alert: a new privileged account was created.
            if (IsPrivileged(user.Role))
            {
                var createdBy = HttpContext.Session.GetString("UserName") ?? "Unknown";
                await _alerts.NewAdminAccountAsync(user.Email, user.Role.ToString(), createdBy);
            }

            var activationLink = Url.Action("SetPassword", "Account",
                new { token = user.ResetToken }, Request.Scheme)!;

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            await TrySendEmailAsync(
                user.Email, user.FirstName,
                "Welcome — Activate your account — Axis IT Operations",
                EmailTemplates.WelcomeActivation(user.FirstName, activationLink, baseUrl));

            TempData["Success"] = $"Account created. An activation email has been sent to {user.Email}.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound();

            ViewBag.Departments = await _context.Departments
                .OrderBy(d => d.Name)
                .ToListAsync();

            ViewBag.Supervisors = await _context.Users
                .Where(u => u.Id != id)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToListAsync();

            return View(user);
        }

        // 🔹 UPDATE USER
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(User user)
        {
            var access = CheckAccess();
            if (access != null) return access;

            if (!ModelState.IsValid)
            {
                ViewBag.Departments = await _context.Departments
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                ViewBag.Supervisors = await _context.Users
                    .Where(u => u.Id != user.Id)
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ToListAsync();

                return View(user);
            }

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            if (existingUser == null)
                return NotFound();

            var oldEmail = existingUser.Email;
            var oldRole = existingUser.Role;

            // Update fields
            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.Email = user.Email;
            existingUser.Role = user.Role;

            existingUser.DepartmentId = user.DepartmentId;
            existingUser.SupervisorId = user.SupervisorId;

            // Only update password if supplied
            if (!string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                existingUser.PasswordHash = PasswordHasher.HashPassword(user.PasswordHash);
                existingUser.PasswordChangedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Updated",
                "User",
                user.Id,
                $"User {user.Email} updated");

            // Notify on email change (both old and new addresses).
            bool emailChanged = !string.Equals(oldEmail, existingUser.Email, StringComparison.OrdinalIgnoreCase);
            if (emailChanged)
            {
                var html = EmailTemplates.EmailChanged(existingUser.FirstName, oldEmail, existingUser.Email);
                await TrySendEmailAsync(oldEmail, existingUser.FirstName, "Your account email was changed — Axis IT Operations", html);
                await TrySendEmailAsync(existingUser.Email, existingUser.FirstName, "Your account email was changed — Axis IT Operations", html);
            }

            // Audit a role change for the security/compliance trail.
            if (oldRole != existingUser.Role)
            {
                await _auditService.LogAsync("Permission Changed", "User", existingUser.Id,
                    $"Role changed from {oldRole} to {existingUser.Role} for {existingUser.Email}");

                // Admin alert when a non-privileged account is elevated to a privileged role.
                if (!IsPrivileged(oldRole) && IsPrivileged(existingUser.Role))
                {
                    var changedBy = HttpContext.Session.GetString("UserName") ?? "Unknown";
                    await _alerts.PrivilegeEscalationAsync(existingUser.Email,
                        oldRole.ToString(), existingUser.Role.ToString(), changedBy);
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // 🔹 DELETE CONFIRMATION
        public async Task<IActionResult> Delete(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var user = await _context.Users.FindAsync(id);

            if (user == null) return NotFound();

            return View(user);
        }

        [HttpGet]
        [IT_Service_Management_System.Filters.AllowAnyRole]
        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return NotFound();

            var currentToken = HttpContext.Session.GetString(SessionService.SessionTokenKey);
            ViewBag.ActiveSessions = await _context.UserSessions
                .Where(s => s.UserId == userId && s.RevokedAt == null)
                .OrderByDescending(s => s.LastSeenAt)
                .ToListAsync();
            ViewBag.CurrentSessionToken = currentToken;

            return View(user);
        }

        [HttpPost]
        [IT_Service_Management_System.Filters.AllowAnyRole]
        public async Task<IActionResult> Profile(User model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            var oldEmail = user.Email;

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Email = model.Email;

            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync("Profile Updated", "User", user.Id, "User updated profile");

            HttpContext.Session.SetString("UserName", user.FirstName);

            bool emailChanged = !string.Equals(oldEmail, user.Email, StringComparison.OrdinalIgnoreCase);
            if (emailChanged)
            {
                var html = EmailTemplates.EmailChanged(user.FirstName, oldEmail, user.Email);
                await TrySendEmailAsync(oldEmail, user.FirstName, "Your account email was changed — Axis IT Operations", html);
                await TrySendEmailAsync(user.Email, user.FirstName, "Your account email was changed — Axis IT Operations", html);
            }

            // Reload sidebars/session list for the re-rendered view.
            ViewBag.ActiveSessions = await _context.UserSessions
                .Where(s => s.UserId == user.Id && s.RevokedAt == null)
                .OrderByDescending(s => s.LastSeenAt).ToListAsync();
            ViewBag.CurrentSessionToken = HttpContext.Session.GetString(SessionService.SessionTokenKey);
            ViewBag.Success = "Profile updated successfully";

            return View(user);
        }

        // 🔹 SELF-SERVICE MFA TOGGLE (email OTP)
        [HttpPost]
        [IT_Service_Management_System.Filters.AllowAnyRole]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMfa(bool enable)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            bool was = user.MfaEnabled;
            user.MfaEnabled = enable;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(enable ? "MFA Enabled" : "MFA Disabled", "User", user.Id,
                $"{user.Email} {(enable ? "enabled" : "disabled")} email OTP MFA");

            if (was && !enable)
            {
                await TrySendEmailAsync(user.Email, user.FirstName,
                    "Two-factor authentication disabled — Axis IT Operations",
                    EmailTemplates.MfaDisabled(user.FirstName));
            }

            TempData["Success"] = enable
                ? "Two-factor authentication is now ON. You'll get a code by email when signing in."
                : "Two-factor authentication is now OFF.";
            return RedirectToAction(nameof(Profile));
        }

        // 🔹 DELETE USER
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var user = await _context.Users.FindAsync(id);

            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync("Deleted", "User", id, $"User ID {id} deleted");

            TempData["Success"] = $"User {user.Email} deleted.";
            return RedirectToAction("Index");
        }

        // 🔹 ADMIN-TRIGGERED PASSWORD RESET (emails a reset link)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.ResetToken = Guid.NewGuid().ToString("N");
            user.TokenExpiry = DateTime.Now.AddHours(24);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Reset Password", "User", user.Id,
                $"Admin-triggered password reset link sent to {user.Email}");

            var resetLink = Url.Action("SetPassword", "Account",
                new { token = user.ResetToken }, Request.Scheme)!;

            await TrySendEmailAsync(
                user.Email, user.FirstName,
                "Reset your password — Axis IT Operations",
                EmailTemplates.PasswordReset(user.FirstName, resetLink));

            TempData["Success"] = $"A password reset link has been sent to {user.Email}.";
            return RedirectToAction("Details", new { id });
        }

        // 🔹 RESEND ACTIVATION EMAIL (for inactive accounts)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendInvitation(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.IsActive)
            {
                TempData["Error"] = "This account is already active. Use Reset Password instead.";
                return RedirectToAction("Details", new { id });
            }

            user.ResetToken = Guid.NewGuid().ToString("N");
            user.TokenExpiry = DateTime.Now.AddHours(24);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Resend Invitation", "User", user.Id,
                $"Activation email resent to {user.Email}");

            var activationLink = Url.Action("SetPassword", "Account",
                new { token = user.ResetToken }, Request.Scheme)!;
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            await TrySendEmailAsync(
                user.Email, user.FirstName,
                "Your activation link has been refreshed — Axis IT Operations",
                EmailTemplates.ResendActivation(user.FirstName, activationLink));

            TempData["Success"] = $"A new activation link has been sent to {user.Email}.";
            return RedirectToAction("Details", new { id });
        }
    }
}