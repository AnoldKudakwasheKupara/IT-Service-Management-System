using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.ViewModels.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 🧑‍💼 HR ANALYTICS — accessible to HR as well as full-access roles.
        [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "HR")]
        public IActionResult Hr()
        {
            var clearances = _context.ExitClearances.AsNoTracking().Include(c => c.Employee).ToList();

            var vm = new HrReportVM
            {
                TotalClearances = clearances.Count,
                ClearancesInProgress = clearances.Count(c => c.Status == Models.ClearanceStatus.InProgress),
                ClearancesCompleted = clearances.Count(c => c.Status == Models.ClearanceStatus.Completed),
                ExitInterviews = _context.ExitInterviews.Count(),
                EngagementInterviews = _context.EngagementStayInterviews.Count(),
                TalentRecords = _context.TalentIdentifications.Count(),
                ClearancesByStatus = clearances.GroupBy(c => c.Status.ToString())
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                ClearancesByStage = clearances.GroupBy(c => c.CurrentStage.ToString())
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                RecentClearances = clearances.OrderByDescending(c => c.CreatedDate).Take(10).ToList()
            };

            return View(vm);
        }

        private static bool IsStock(string? status) =>
            string.IsNullOrEmpty(status) || status == "In Stock" || status == "Available";

        // 🏠 REPORTS HUB
        public IActionResult Index()
        {
            var assets = _context.Assets.AsNoTracking().ToList();
            var activities = _context.Activities.AsNoTracking().ToList();
            var payments = _context.Payments.AsNoTracking().ToList();
            var certs = _context.SSLCertificates.AsNoTracking().ToList();
            var now = DateTime.Now;

            var vm = new ReportsDashboardVM
            {
                GeneratedAt = now,

                TotalTickets = _context.Tickets.Count(),
                OpenTickets = _context.Tickets.Count(t => t.Status == Ticket.TicketStatus.Open),
                ResolvedTickets = _context.Tickets.Count(t => t.Status == Ticket.TicketStatus.Resolved),
                ClosedTickets = _context.Tickets.Count(t => t.Status == Ticket.TicketStatus.Closed),

                TotalAssets = assets.Count,
                AssetsIssued = assets.Count(a => a.Status == "Issued"),
                AssetsInStock = assets.Count(a => IsStock(a.Status)),
                AssetsInRepair = assets.Count(a => a.Status == "In Repair"),
                TotalAssetValue = assets.Sum(a => a.PurchaseCost ?? 0),

                TotalUsers = _context.Users.Count(),
                ActiveUsers = _context.Users.Count(u => u.IsActive),
                TotalDepartments = _context.Departments.Count(),

                TotalActivities = activities.Count,
                TotalActivityHours = Math.Round(activities.Where(a => a.Duration.HasValue).Sum(a => a.Duration!.Value.TotalHours), 1),

                TotalPaid = payments.Where(p => p.Status == "Paid").Sum(p => p.Amount),
                TotalOutstanding = payments.Where(p => p.Status != "Paid").Sum(p => p.Amount),
                OverduePayments = payments.Count(p => p.Status != "Paid" && p.DueDate < now),

                CertsExpired = certs.Count(c => c.ExpiryDate < now),
                CertsExpiringSoon = certs.Count(c => c.ExpiryDate >= now && c.ExpiryDate <= now.AddDays(30)),

                MaintenanceRecords = _context.MaintenanceRecords.Count()
            };

            return View(vm);
        }

        // 🎫 TICKETS
        public IActionResult Tickets()
        {
            var tickets = _context.Tickets.AsNoTracking().Include(t => t.CreatedBy).ToList();

            var vm = new TicketsReportVM
            {
                Total = tickets.Count,
                Open = tickets.Count(t => t.Status == Ticket.TicketStatus.Open),
                InProgress = tickets.Count(t => t.Status == Ticket.TicketStatus.InProgress),
                Resolved = tickets.Count(t => t.Status == Ticket.TicketStatus.Resolved),
                Closed = tickets.Count(t => t.Status == Ticket.TicketStatus.Closed),
                ByStatus = tickets.GroupBy(t => t.Status.ToString())
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                ByPriority = tickets.GroupBy(t => t.Priority.ToString())
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                ByCategory = tickets.GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Uncategorised" : t.Category)
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                TopRequesters = tickets.Where(t => t.CreatedBy != null)
                    .GroupBy(t => $"{t.CreatedBy!.FirstName} {t.CreatedBy.LastName}")
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).Take(10).ToList(),
                Recent = tickets.OrderByDescending(t => t.CreatedAt).Take(10).ToList()
            };
            vm.ResolutionRate = vm.Total == 0 ? 0 : Math.Round(100.0 * (vm.Resolved + vm.Closed) / vm.Total, 1);

            return View(vm);
        }

        // 💻 ASSETS
        public IActionResult Assets()
        {
            var assets = _context.Assets.AsNoTracking().Include(a => a.User).ToList();
            var history = _context.AssetHistories.AsNoTracking()
                .Include(h => h.Asset).Include(h => h.User)
                .OrderByDescending(h => h.Date).Take(25).ToList();

            var vm = new AssetsReportVM
            {
                Total = assets.Count,
                TotalValue = assets.Sum(a => a.PurchaseCost ?? 0),
                ByStatus = assets.GroupBy(a => IsStock(a.Status) ? "In Stock" : a.Status!)
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                ByCondition = assets.GroupBy(a => string.IsNullOrWhiteSpace(a.Condition) ? "Unknown" : a.Condition)
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                ByHolder = assets.Where(a => a.User != null)
                    .GroupBy(a => $"{a.User!.FirstName} {a.User.LastName}")
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).Take(10).ToList(),
                ByEventType = _context.AssetHistories.AsNoTracking().ToList()
                    .GroupBy(h => string.IsNullOrWhiteSpace(h.EventType) ? "Other" : h.EventType)
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                RecentActivity = history
            };

            return View(vm);
        }

        // 👥 USERS & DEPARTMENTS
        public IActionResult Users()
        {
            var users = _context.Users.AsNoTracking().Include(u => u.Department).ToList();
            var assets = _context.Assets.AsNoTracking().Include(a => a.User).Where(a => a.UserId != null).ToList();

            var vm = new UsersReportVM
            {
                Total = users.Count,
                Active = users.Count(u => u.IsActive),
                Inactive = users.Count(u => !u.IsActive),
                ByRole = users.GroupBy(u => u.Role.ToString())
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                ByDepartment = users.GroupBy(u => u.Department != null ? u.Department.Name : "Unassigned")
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                AssetsPerUser = assets.Where(a => a.User != null)
                    .GroupBy(a => $"{a.User!.FirstName} {a.User.LastName}")
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).Take(10).ToList()
            };

            return View(vm);
        }

        // 🗓️ ACTIVITY
        public IActionResult Activity()
        {
            var activities = _context.Activities.AsNoTracking().Include(a => a.Category).ToList();
            var userNames = _context.Users.AsNoTracking().ToDictionary(u => u.Id.ToString(), u => $"{u.FirstName} {u.LastName}");

            var vm = new ActivityReportVM
            {
                Total = activities.Count,
                Ongoing = activities.Count(a => !a.EndTime.HasValue),
                Completed = activities.Count(a => a.EndTime.HasValue),
                TotalHours = Math.Round(activities.Where(a => a.Duration.HasValue).Sum(a => a.Duration!.Value.TotalHours), 1),
                ByCategory = activities.GroupBy(a => a.Category != null ? a.Category.Name : "Uncategorised")
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                HoursByCategory = activities.Where(a => a.Duration.HasValue)
                    .GroupBy(a => a.Category != null ? a.Category.Name : "Uncategorised")
                    .Select(g => new NameAmount(g.Key, Math.Round((decimal)g.Sum(x => x.Duration!.Value.TotalHours), 1)))
                    .OrderByDescending(x => x.Amount).ToList(),
                HoursByUser = activities.Where(a => a.Duration.HasValue)
                    .GroupBy(a => a.UserId ?? "")
                    .Select(g => new NameAmount(
                        userNames.TryGetValue(g.Key, out var n) ? n : "Unknown",
                        Math.Round((decimal)g.Sum(x => x.Duration!.Value.TotalHours), 1)))
                    .OrderByDescending(x => x.Amount).Take(10).ToList()
            };

            return View(vm);
        }

        // 💳 PAYMENTS
        public IActionResult Payments()
        {
            var payments = _context.Payments.AsNoTracking().ToList();
            var now = DateTime.Now;

            var vm = new PaymentsReportVM
            {
                TotalAmount = payments.Sum(p => p.Amount),
                TotalPaid = payments.Where(p => p.Status == "Paid").Sum(p => p.Amount),
                TotalOutstanding = payments.Where(p => p.Status != "Paid").Sum(p => p.Amount),
                TotalOverdue = payments.Where(p => p.Status != "Paid" && p.DueDate < now).Sum(p => p.Amount),
                CountByStatus = payments.GroupBy(p => string.IsNullOrWhiteSpace(p.Status) ? "Pending" : p.Status)
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                AmountByStatus = payments.GroupBy(p => string.IsNullOrWhiteSpace(p.Status) ? "Pending" : p.Status)
                    .Select(g => new NameAmount(g.Key, g.Sum(x => x.Amount))).OrderByDescending(x => x.Amount).ToList(),
                Upcoming = payments.Where(p => p.Status != "Paid" && p.DueDate >= now && p.DueDate <= now.AddDays(30))
                    .OrderBy(p => p.DueDate).ToList(),
                Overdue = payments.Where(p => p.Status != "Paid" && p.DueDate < now)
                    .OrderBy(p => p.DueDate).ToList()
            };

            return View(vm);
        }

        // 🔒 SSL CERTIFICATES
        public IActionResult Certificates()
        {
            var certs = _context.SSLCertificates.AsNoTracking().ToList();
            var now = DateTime.Now;

            var vm = new CertificatesReportVM
            {
                Total = certs.Count,
                Expired = certs.Count(c => c.ExpiryDate < now),
                Within30 = certs.Count(c => c.ExpiryDate >= now && c.ExpiryDate <= now.AddDays(30)),
                Within90 = certs.Count(c => c.ExpiryDate > now.AddDays(30) && c.ExpiryDate <= now.AddDays(90)),
                Healthy = certs.Count(c => c.ExpiryDate > now.AddDays(90)),
                Attention = certs.Where(c => c.ExpiryDate <= now.AddDays(30)).OrderBy(c => c.ExpiryDate).ToList()
            };

            return View(vm);
        }

        // 🛠️ MAINTENANCE
        public IActionResult Maintenance()
        {
            var records = _context.MaintenanceRecords.AsNoTracking().ToList();
            var now = DateTime.Now;

            var vm = new MaintenanceReportVM
            {
                Total = records.Count,
                ByType = records.GroupBy(r => r.MaintenanceType.ToString())
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                Recent = records.OrderByDescending(r => r.MaintenanceDate).Take(10).ToList(),
                Upcoming = records.Where(r => r.NextMaintenanceDate != null && r.NextMaintenanceDate >= now)
                    .OrderBy(r => r.NextMaintenanceDate).Take(10).ToList()
            };
            vm.UpcomingCount = records.Count(r => r.NextMaintenanceDate != null && r.NextMaintenanceDate >= now);

            return View(vm);
        }
    }
}
