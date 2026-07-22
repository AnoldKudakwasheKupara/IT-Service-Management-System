using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Itsm;
using IT_Service_Management_System.ViewModels.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

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

        // ⏱️ SLA COMPLIANCE
        public IActionResult SlaCompliance()
        {
            var now = DateTime.Now;
            var tickets = _context.Tickets.AsNoTracking().ToList();
            var measured = tickets.Where(t => t.DueAt != null).ToList();
            DateTime? DoneAt(Ticket t) => t.ResolvedAt ?? t.ClosedAt;
            var resolved = measured.Where(t => DoneAt(t) != null).ToList();

            int resMet = resolved.Count(t => DoneAt(t) <= t.DueAt);
            int resBreached = resolved.Count - resMet;

            var withResp = measured.Where(t => t.ResponseDueAt != null).ToList();
            var responded = withResp.Where(t => t.FirstRespondedAt != null).ToList();
            int respMet = responded.Count(t => t.FirstRespondedAt <= t.ResponseDueAt);
            int respBreached = (responded.Count - respMet)
                + withResp.Count(t => t.FirstRespondedAt == null && t.ResponseDueAt < now);

            var vm = new SlaComplianceVM
            {
                MeasuredTickets = measured.Count,
                ResolutionMet = resMet,
                ResolutionBreached = resBreached,
                ResponseMet = respMet,
                ResponseBreached = respBreached,
                OpenBreaching = measured.Count(t => t.IsOpen && t.DueAt < now),
                ResolutionCompliance = resolved.Count == 0 ? 0 : Math.Round(100.0 * resMet / resolved.Count, 1),
                ResponseCompliance = (respMet + respBreached) == 0 ? 0 : Math.Round(100.0 * respMet / (respMet + respBreached), 1),
                AvgResolutionHours = resolved.Count == 0 ? 0 : Math.Round(resolved.Average(t => (DoneAt(t)!.Value - t.CreatedAt).TotalHours), 1),
                AvgResponseHours = responded.Count == 0 ? 0 : Math.Round(responded.Average(t => (t.FirstRespondedAt!.Value - t.CreatedAt).TotalHours), 1),
                ByPriority = measured.GroupBy(t => t.Priority).OrderByDescending(g => g.Key).Select(g =>
                {
                    var gr = g.Where(t => DoneAt(t) != null).ToList();
                    int met = gr.Count(t => DoneAt(t) <= t.DueAt);
                    return new SlaByPriority(g.Key.ToString(), g.Count(), met, gr.Count - met,
                        gr.Count == 0 ? 0 : Math.Round(100.0 * met / gr.Count, 1),
                        gr.Count == 0 ? 0 : Math.Round(gr.Average(t => (DoneAt(t)!.Value - t.CreatedAt).TotalHours), 1));
                }).ToList(),
                BreachesByCategory = resolved.Where(t => DoneAt(t) > t.DueAt)
                    .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Uncategorised" : t.Category)
                    .Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList()
            };
            return View(vm);
        }

        // 👷 AGENT PERFORMANCE
        public IActionResult AgentPerformance()
        {
            var now = DateTime.Now;
            var tickets = _context.Tickets.AsNoTracking().Where(t => t.AssignedToId != null).ToList();
            var names = _context.Users.AsNoTracking().ToDictionary(u => u.Id, u => u.FirstName + " " + u.LastName);
            DateTime? DoneAt(Ticket t) => t.ResolvedAt ?? t.ClosedAt;

            var vm = new AgentPerformanceVM
            {
                Unassigned = _context.Tickets.Count(t => t.AssignedToId == null),
                Agents = tickets.GroupBy(t => t.AssignedToId!.Value).Select(g =>
                {
                    var resolved = g.Where(t => DoneAt(t) != null).ToList();
                    var csats = g.Where(t => t.SatisfactionRating != null).Select(t => (double)t.SatisfactionRating!.Value).ToList();
                    return new AgentRow(
                        names.TryGetValue(g.Key, out var n) ? n : "User #" + g.Key,
                        g.Count(),
                        g.Count(t => t.IsOpen),
                        resolved.Count,
                        resolved.Count == 0 ? 0 : Math.Round(resolved.Average(t => (DoneAt(t)!.Value - t.CreatedAt).TotalHours), 1),
                        g.Count(t => (t.DueAt != null && DoneAt(t) != null && DoneAt(t) > t.DueAt) || (t.DueAt != null && t.IsOpen && t.DueAt < now)),
                        csats.Count == 0 ? (double?)null : Math.Round(csats.Average(), 1));
                }).OrderByDescending(a => a.Resolved).ToList()
            };
            vm.TotalResolved = vm.Agents.Sum(a => a.Resolved);
            return View(vm);
        }

        // 🧩 ITIL OVERVIEW (Problems, Changes, CMDB)
        public IActionResult ItilOverview()
        {
            var now = DateTime.Now;
            var problems = _context.Problems.AsNoTracking().ToList();
            var changes = _context.ChangeRequests.AsNoTracking().ToList();
            var cis = _context.ConfigurationItems.AsNoTracking().ToList();
            var closedChanges = changes.Where(c => c.ImplementedSuccessfully != null).ToList();

            var vm = new ItilOverviewVM
            {
                TotalProblems = problems.Count,
                OpenProblems = problems.Count(p => p.Status != ProblemStatus.Resolved && p.Status != ProblemStatus.Closed),
                KnownErrors = problems.Count(p => p.Status == ProblemStatus.KnownError),
                TotalChanges = changes.Count,
                ChangesAwaitingApproval = changes.Count(c => c.Status == ChangeStatus.SubmittedForApproval),
                ChangeSuccessRate = closedChanges.Count == 0 ? 0 : (int)Math.Round(100.0 * closedChanges.Count(c => c.ImplementedSuccessfully == true) / closedChanges.Count),
                TotalCis = cis.Count,
                CriticalCis = cis.Count(c => c.Criticality == CiCriticality.Critical),
                ProblemsByStatus = problems.GroupBy(p => p.Status.ToString()).Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                ChangesByStatus = changes.GroupBy(c => c.Status.ToString()).Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                ChangesByType = changes.GroupBy(c => c.Type.ToString()).Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                ChangesByRisk = changes.GroupBy(c => c.Risk.ToString()).Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                CisByStatus = cis.GroupBy(c => c.Status.ToString()).Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                CisByCriticality = cis.GroupBy(c => c.Criticality.ToString()).Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
                RecentProblems = _context.Problems.AsNoTracking().OrderByDescending(p => p.CreatedAt).Take(8).ToList(),
                UpcomingChanges = _context.ChangeRequests.AsNoTracking()
                    .Where(c => c.ScheduledStart != null && c.ScheduledStart >= now)
                    .OrderBy(c => c.ScheduledStart).Take(8).ToList()
            };
            return View(vm);
        }

        // 📈 TICKET TRENDS (last 12 months)
        public IActionResult TicketTrends()
        {
            var start = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-11);
            var tickets = _context.Tickets.AsNoTracking()
                .Where(t => t.CreatedAt >= start || t.ResolvedAt >= start || t.ClosedAt >= start).ToList();

            var vm = new TicketTrendsVM();
            for (int i = 0; i < 12; i++)
            {
                var m = start.AddMonths(i);
                var next = m.AddMonths(1);
                int created = tickets.Count(t => t.CreatedAt >= m && t.CreatedAt < next);
                int resolved = tickets.Count(t => (t.ResolvedAt ?? t.ClosedAt) is DateTime d && d >= m && d < next);
                vm.Months.Add(new TrendPoint(m.ToString("MMM yy"), created, resolved));
                vm.TotalCreated += created;
                vm.TotalResolved += resolved;
            }
            vm.PeakValue = vm.Months.Count == 0 ? 1 : Math.Max(1, vm.Months.Max(p => Math.Max(p.Created, p.Resolved)));
            return View(vm);
        }
    }
}
