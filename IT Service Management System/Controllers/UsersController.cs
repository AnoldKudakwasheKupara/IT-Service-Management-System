using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly AuditService _auditService; // ✅ ADDED

        public UsersController(ApplicationDbContext context, EmailService emailService, AuditService auditService) // ✅ UPDATED
        {
            _context = context;
            _emailService = emailService;
            _auditService = auditService;
        }

        // 🔒 AUTH + ROLE CHECK
        private IActionResult CheckAccess()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            if (HttpContext.Session.GetString("UserRole") != "Admin")
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
                    .ThenInclude(d => d.Hod)
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
                    .ThenInclude(d => d.Hod)
                .Include(u => u.Supervisor)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound();

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
            user.ResetToken = Guid.NewGuid().ToString();
            user.TokenExpiry = DateTime.Now.AddHours(24);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "Created",
                "User",
                user.Id,
                $"User {user.Email} created");

            var link = Url.Action(
                "SetPassword",
                "Account",
                new { token = user.ResetToken },
                Request.Scheme);

            var body = $@"
        <p>Good Day {user.FirstName},</p>

        <p>Welcome to the <strong>IT Service Management System</strong>.</p>

        <p>Your account has been created successfully. To get started, please set your password by clicking the link below:</p>

        <p>
            <a href='{link}' style='color:blue; font-weight:bold;'>
                Set Your Password
            </a>
        </p>

        <p>This link will expire in 24 hours for security purposes.</p>

        <br/>

        <p>If you did not expect this email, please ignore it.</p>

        <p>
            Kind Regards,<br/>
            IT Support Team
        </p>";

            await _emailService.SendEmailAsync(
                user.Email,
                "Welcome - Set Your Password",
                body);

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
        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return NotFound();

            return View(user);
        }

        [HttpPost]
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

        // 🔹 ADMIN-TRIGGERED PASSWORD RESET (emails a set-password link)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var user = await _context.Users.FindAsync(id);

            if (user == null) return NotFound();

            user.ResetToken = Guid.NewGuid().ToString();
            user.TokenExpiry = DateTime.Now.AddHours(24);

            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync("Reset Password", "User", user.Id, $"Password reset link sent to {user.Email}");

            var link = Url.Action(
                "SetPassword",
                "Account",
                new { token = user.ResetToken },
                Request.Scheme);

            var body = $@"
        <p>Good Day {user.FirstName},</p>

        <p>An administrator has requested a password reset for your account.</p>

        <p>Click the link below to set a new password:</p>

        <p>
            <a href='{link}' style='color:blue; font-weight:bold;'>Set Your Password</a>
        </p>

        <p>This link will expire in 24 hours.</p>

        <p>If you did not expect this email, please contact IT Support.</p>";

            await _emailService.SendEmailAsync(user.Email, "Password Reset", body);

            TempData["Success"] = $"A password reset link has been sent to {user.Email}.";

            return RedirectToAction("Details", new { id });
        }
    }
}