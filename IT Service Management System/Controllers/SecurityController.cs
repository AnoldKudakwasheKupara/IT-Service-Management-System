using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Services;
using IT_Service_Management_System.ViewModels.Reports;
using IT_Service_Management_System.ViewModels.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class SecurityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ConfigurationService _configService;
        private readonly SessionService _sessions;

        public SecurityController(
            ApplicationDbContext context,
            ConfigurationService configService,
            SessionService sessions)
        {
            _context = context;
            _configService = configService;
            _sessions = sessions;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var config = _configService.Get();

            var users = await _context.Users.AsNoTracking().ToListAsync();

            // Expired-password computation (only when a policy is set).
            var expiredUsers = new List<Models.User>();
            if (config.PasswordExpiryDays > 0)
            {
                var cutoff = now.AddDays(-config.PasswordExpiryDays);
                expiredUsers = users
                    .Where(u => u.IsActive && (u.PasswordChangedAt ?? u.CreatedAt) < cutoff)
                    .OrderBy(u => u.PasswordChangedAt ?? u.CreatedAt)
                    .ToList();
            }

            var activeSessions = await _context.UserSessions.AsNoTracking()
                .Include(s => s.User)
                .Where(s => s.RevokedAt == null)
                .OrderByDescending(s => s.LastSeenAt)
                .ToListAsync();

            var vm = new SecurityDashboardVM
            {
                GeneratedAt = now,
                TotalUsers = users.Count,
                LockedUsersCount = users.Count(u => u.LockoutEnd.HasValue && u.LockoutEnd > now),
                ActiveSessionsCount = activeSessions.Count,
                ExpiredPasswordsCount = expiredUsers.Count,
                DisabledAccountsCount = users.Count(u => !u.IsActive),
                MfaEnabledCount = users.Count(u => u.MfaEnabled),
                PasswordExpiryDays = config.PasswordExpiryDays,
                MfaEnabled = config.MfaEnabled,

                LockedUsers = users.Where(u => u.LockoutEnd.HasValue && u.LockoutEnd > now)
                    .OrderByDescending(u => u.LockoutEnd).ToList(),
                DisabledAccounts = users.Where(u => !u.IsActive).OrderBy(u => u.FirstName).ToList(),
                ExpiredPasswordUsers = expiredUsers,
                ActiveSessions = activeSessions
            };

            return View(vm);
        }

        // Clears a user's lockout so they can sign in again.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.LockoutEnd = null;
            user.FailedLoginCount = 0;
            await _context.SaveChangesAsync();


            TempData["Success"] = $"{user.Email} has been unlocked.";
            return RedirectToAction(nameof(Index));
        }

        // Revokes a single active session.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeSession(int id)
        {
            var session = await _context.UserSessions.FindAsync(id);
            if (session == null) return NotFound();

            if (session.RevokedAt == null)
            {
                session.RevokedAt = DateTime.Now;
                session.RevokedReason = "Revoked by administrator";
                await _context.SaveChangesAsync();

            }

            TempData["Success"] = "Session revoked.";
            return RedirectToAction(nameof(Index));
        }
    }
}
