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
        private readonly EmailService _emailService;
        private readonly AuditService _auditService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(ApplicationDbContext context, EmailService emailService, AuditService auditService, ILogger<UsersController> logger)
        {
            _context = context;
            _emailService = emailService;
            _auditService = auditService;
            _logger = logger;
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

        // 🔒 AUTH + ROLE CHECK
        private IActionResult? CheckAccess()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Admin" && role != "SystemsAdmin")
                return Forbid();

            return null;
        }

        // 🔹 LIST USERS
        public async Task<IActionResult> Index()
        {
            var access = CheckAccess();
            if (access != null)
                return access;

            var users = await _context.Users
                .Include(u => u.Department)
                    .ThenInclude(d => d!.Hod)
                .Include(u => u.Supervisor)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToListAsync();

            return View(users);
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
            }

            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Updated",
                "User",
                user.Id,
                $"User {user.Email} updated");

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

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Email = model.Email;

            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync("Profile Updated", "User", user.Id, "User updated profile");

            HttpContext.Session.SetString("UserName", user.FirstName);

            ViewBag.Success = "Profile updated successfully";

            return View(user);
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