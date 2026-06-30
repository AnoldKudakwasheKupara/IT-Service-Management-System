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
        private readonly AuditService _auditService;
        private readonly SessionService _sessions;

        // Audit actions considered "security events" for the dashboard feed.
        private static readonly string[] SecurityActions =
        {
            "Login", "Login Failed", "Login Blocked", "Logout", "Logout All Devices",
            "Account Locked", "Account Unlocked", "Set Password", "Forgot Password",
            "Reset Password", "Resend Invitation", "Session Revoked"
        };

        public SecurityController(
            ApplicationDbContext context,
            ConfigurationService configService,
            AuditService auditService,
            SessionService sessions)
        {
            _context = context;
            _configService = configService;
            _auditService = auditService;
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

            var failed24 = await _context.AuditLogs.AsNoTracking()
                .CountAsync(a => a.Action == "Login Failed" && a.Timestamp >= now.AddHours(-24));
            var failed7 = await _context.AuditLogs.AsNoTracking()
                .CountAsync(a => a.Action == "Login Failed" && a.Timestamp >= now.AddDays(-7));

            var topFailed = await _context.AuditLogs.AsNoTracking()
                .Where(a => a.Action == "Login Failed" && a.Timestamp >= now.AddDays(-7))
                .GroupBy(a => a.Details)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(8)
                .ToListAsync();

            var recentEvents = await _context.AuditLogs.AsNoTracking()
                .Where(a => SecurityActions.Contains(a.Action))
                .OrderByDescending(a => a.Timestamp)
                .Take(25)
                .ToListAsync();

            var vm = new SecurityDashboardVM
            {
                GeneratedAt = now,
                TotalUsers = users.Count,
                FailedLogins24h = failed24,
                FailedLogins7d = failed7,
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
                ActiveSessions = activeSessions,
                RecentSecurityEvents = recentEvents,
                TopFailedLoginTargets = topFailed.Select(x => new NameCount(x.Name, x.Count)).ToList()
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

            await _auditService.LogAsync("Account Unlocked", "User", user.Id,
                $"Account {user.Email} manually unlocked");

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

                await _auditService.LogAsync("Session Revoked", "UserSession", session.Id,
                    $"Session for user #{session.UserId} revoked by admin");
            }

            TempData["Success"] = "Session revoked.";
            return RedirectToAction(nameof(Index));
        }
    }
}
