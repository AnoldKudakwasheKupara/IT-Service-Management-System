using System.Text;
using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class ComplianceController : Controller
    {
        private const int LargeExportThreshold = 500;

        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;
        private readonly AlertService _alerts;

        public ComplianceController(ApplicationDbContext context, AuditService auditService, AlertService alerts)
        {
            _context = context;
            _auditService = auditService;
            _alerts = alerts;
        }

        // type key -> (title, icon, audit actions [] (empty = all), description)
        public static readonly Dictionary<string, (string Title, string Icon, string[] Actions, string Desc)> Reports = new()
        {
            ["login-history"] = ("Login History", "fa-right-to-bracket",
                new[] { "Login", "Logout", "Logout All Devices", "MFA Verified", "MFA Challenge" },
                "Successful sign-ins, sign-outs and MFA events."),
            ["failed-logins"] = ("Failed Logins", "fa-user-xmark",
                new[] { "Login Failed", "Login Blocked", "MFA Failed", "Account Locked" },
                "Failed sign-ins, lockouts and blocked attempts."),
            ["permission-changes"] = ("Permission Changes", "fa-user-shield",
                new[] { "Permission Changed", "Configuration Updated", "MFA Enabled", "MFA Disabled" },
                "Role changes, configuration and security-setting changes."),
            ["deleted-records"] = ("Deleted Records", "fa-trash",
                new[] { "Deleted" },
                "Records removed from the system, with who and when."),
            ["data-exports"] = ("Data Exports", "fa-file-export",
                new[] { "Data Export" },
                "Every CSV/report export performed in the system."),
            ["user-activity"] = ("User Activity", "fa-user-clock",
                Array.Empty<string>(),
                "Complete audited activity trail, filterable by user."),
            ["audit-logs"] = ("Audit Logs", "fa-clipboard-list",
                Array.Empty<string>(),
                "The full, unfiltered audit log."),
        };

        public IActionResult Index()
        {
            ViewBag.Reports = Reports;
            return View();
        }

        public async Task<IActionResult> Report(string type, DateTime? from, DateTime? to, string? user)
        {
            if (!Reports.ContainsKey(type)) return NotFound();

            var (logs, def, fromD, toD) = await QueryAsync(type, from, to, user);

            ViewBag.Type = type;
            ViewBag.Title = def.Title;
            ViewBag.Icon = def.Icon;
            ViewBag.Desc = def.Desc;
            ViewBag.From = fromD;
            ViewBag.To = toD;
            ViewBag.User = user;
            ViewBag.Users = await _context.Users.AsNoTracking()
                .OrderBy(u => u.FirstName).Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();

            return View(logs);
        }

        public async Task<IActionResult> Export(string type, DateTime? from, DateTime? to, string? user)
        {
            if (!Reports.ContainsKey(type)) return NotFound();

            var (logs, def, fromD, toD) = await QueryAsync(type, from, to, user);

            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Action,User,Entity,EntityId,Details,IP Address,Location,Device");
            foreach (var l in logs)
            {
                sb.AppendLine(string.Join(",",
                    Csv(l.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")),
                    Csv(l.Action), Csv(l.UserName), Csv(l.Entity),
                    Csv(l.EntityId?.ToString() ?? ""), Csv(l.Details),
                    Csv(l.IpAddress), Csv(l.Location ?? ""), Csv(l.Device ?? "")));
            }

            // A data export is itself an auditable event.
            await _auditService.LogAsync("Data Export", "Compliance", null,
                $"Exported '{def.Title}' ({logs.Count} rows, {fromD:yyyy-MM-dd}–{toD:yyyy-MM-dd})");

            // Alert admins on unusually large exports.
            if (logs.Count >= LargeExportThreshold)
            {
                var who = HttpContext.Session.GetString("UserName") ?? "Unknown";
                await _alerts.LargeDataExportAsync(who, def.Title, logs.Count);
            }

            var fileName = $"compliance-{type}-{DateTime.Now:yyyyMMdd-HHmm}.csv";
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
        }

        // ── shared query ───────────────────────────────────────────────────────────
        private async Task<(List<AuditLog> logs,
            (string Title, string Icon, string[] Actions, string Desc) def,
            DateTime from, DateTime to)>
            QueryAsync(string type, DateTime? from, DateTime? to, string? user)
        {
            var def = Reports[type];
            var fromD = (from ?? DateTime.Now.AddDays(-30)).Date;
            var toD = (to ?? DateTime.Now).Date.AddDays(1).AddSeconds(-1); // inclusive end-of-day

            var q = _context.AuditLogs.AsNoTracking()
                .Where(a => a.Timestamp >= fromD && a.Timestamp <= toD);

            if (def.Actions.Length > 0)
                q = q.Where(a => def.Actions.Contains(a.Action));

            if (!string.IsNullOrEmpty(user))
                q = q.Where(a => a.UserId == user);

            var logs = await q.OrderByDescending(a => a.Timestamp).Take(2000).ToListAsync();
            return (logs, def, fromD, toD);
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
