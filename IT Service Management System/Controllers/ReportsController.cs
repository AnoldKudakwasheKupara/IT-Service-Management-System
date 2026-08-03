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
        private readonly TimeProvider _clock;
        private readonly IT_Service_Management_System.Services.Hr.HrAnalyticsService _hrAnalytics;

        public ReportsController(ApplicationDbContext context, TimeProvider clock,
            IT_Service_Management_System.Services.Hr.HrAnalyticsService hrAnalytics)
        {
            _context = context;
            _clock = clock;
            _hrAnalytics = hrAnalytics;
        }

        /// <summary>
        /// Workforce analytics — headcount, turnover, tenure, and the aggregate of what the exit
        /// and stay interviews have been collecting all along.
        /// </summary>
        [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "HR")]
        public async Task<IActionResult> Workforce(DateTime? from, DateTime? to)
        {
            // Twelve months to date is the window most HR reporting is quoted over.
            var today = _clock.GetLocalNow().Date;
            var start = from ?? today.AddMonths(-12);
            var end = to ?? today;
            if (end < start) end = start;

            return View(await _hrAnalytics.BuildAsync(start, end));
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
        public async Task<IActionResult> Tickets()
        {
            var tickets = _context.Tickets.AsNoTracking();

            // Each breakdown is a GROUP BY on the server; only the grouped rows come back, never the
            // ticket table itself. Enum keys are grouped as their stored int and named client-side,
            // because ToString() on an enum has no SQL translation.
            var byStatus = await tickets.GroupBy(t => t.Status)
                .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
            var byPriority = await tickets.GroupBy(t => t.Priority)
                .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
            var byCategory = await tickets.GroupBy(t => t.Category)
                .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
            var topRequesters = await tickets.Where(t => t.CreatedBy != null)
                .GroupBy(t => new { t.CreatedBy!.FirstName, t.CreatedBy.LastName })
                .Select(g => new { g.Key.FirstName, g.Key.LastName, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(10).ToListAsync();

            int CountOf(Ticket.TicketStatus s) => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0;

            var vm = new TicketsReportVM
            {
                Total = byStatus.Sum(x => x.Count),
                Open = CountOf(Ticket.TicketStatus.Open),
                InProgress = CountOf(Ticket.TicketStatus.InProgress),
                Resolved = CountOf(Ticket.TicketStatus.Resolved),
                Closed = CountOf(Ticket.TicketStatus.Closed),
                ByStatus = byStatus.Select(x => new NameCount(x.Key.ToString(), x.Count))
                    .OrderByDescending(x => x.Count).ToList(),
                ByPriority = byPriority.Select(x => new NameCount(x.Key.ToString(), x.Count))
                    .OrderByDescending(x => x.Count).ToList(),
                // Null and blank categories collapse into one "Uncategorised" bucket after grouping.
                ByCategory = byCategory
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.Key) ? "Uncategorised" : x.Key)
                    .Select(g => new NameCount(g.Key, g.Sum(x => x.Count)))
                    .OrderByDescending(x => x.Count).ToList(),
                TopRequesters = topRequesters
                    .Select(x => new NameCount($"{x.FirstName} {x.LastName}", x.Count)).ToList(),
                Recent = await tickets.Include(t => t.CreatedBy)
                    .OrderByDescending(t => t.CreatedAt).Take(10).ToListAsync()
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
        public async Task<IActionResult> SlaCompliance()
        {
            var now = _clock.GetLocalNow().DateTime;

            // Only tickets with a resolution target are measurable. Everything below aggregates on the
            // server — SUM/COUNT/AVG over these predicates — so the page costs a few grouped rows
            // rather than the whole ticket table in memory.
            var measured = _context.Tickets.AsNoTracking().Where(t => t.DueAt != null);
            var resolved = measured.Where(t => t.ResolvedAt != null || t.ClosedAt != null);
            var withResponseTarget = measured.Where(t => t.ResponseDueAt != null);

            int measuredCount = await measured.CountAsync();
            int resolvedCount = await resolved.CountAsync();
            int resMet = await resolved.CountAsync(t => (t.ResolvedAt ?? t.ClosedAt) <= t.DueAt);
            int resBreached = resolvedCount - resMet;

            int respondedCount = await withResponseTarget.CountAsync(t => t.FirstRespondedAt != null);
            int respMet = await withResponseTarget.CountAsync(t => t.FirstRespondedAt <= t.ResponseDueAt);
            // A breach is either a late reply, or no reply at all once the target has passed.
            int respBreached = (respondedCount - respMet)
                + await withResponseTarget.CountAsync(t => t.FirstRespondedAt == null && t.ResponseDueAt < now);

            int openBreaching = await measured.CountAsync(t =>
                t.Status != Ticket.TicketStatus.Resolved && t.Status != Ticket.TicketStatus.Closed
                && t.DueAt < now);

            // DateDiffMinute maps to SQL DATEDIFF; averaging minutes and converting keeps the whole
            // calculation in the database (TimeSpan subtraction has no SQL translation).
            double avgResolutionHours = resolvedCount == 0 ? 0 : Math.Round(
                (await resolved.AverageAsync(t =>
                    (double?)EF.Functions.DateDiffMinute(t.CreatedAt, t.ResolvedAt ?? t.ClosedAt)) ?? 0) / 60.0, 1);
            double avgResponseHours = respondedCount == 0 ? 0 : Math.Round(
                (await withResponseTarget.Where(t => t.FirstRespondedAt != null).AverageAsync(t =>
                    (double?)EF.Functions.DateDiffMinute(t.CreatedAt, t.FirstRespondedAt)) ?? 0) / 60.0, 1);

            var byPriority = await measured
                .GroupBy(t => t.Priority)
                .Select(g => new
                {
                    Priority = g.Key,
                    Total = g.Count(),
                    Done = g.Count(t => t.ResolvedAt != null || t.ClosedAt != null),
                    Met = g.Count(t => (t.ResolvedAt ?? t.ClosedAt) <= t.DueAt),
                    AvgMinutes = g.Where(t => t.ResolvedAt != null || t.ClosedAt != null)
                        .Average(t => (double?)EF.Functions.DateDiffMinute(t.CreatedAt, t.ResolvedAt ?? t.ClosedAt))
                })
                .ToListAsync();

            var breachesByCategory = await resolved
                .Where(t => (t.ResolvedAt ?? t.ClosedAt) > t.DueAt)
                .GroupBy(t => t.Category)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync();

            var vm = new SlaComplianceVM
            {
                MeasuredTickets = measuredCount,
                ResolutionMet = resMet,
                ResolutionBreached = resBreached,
                ResponseMet = respMet,
                ResponseBreached = respBreached,
                OpenBreaching = openBreaching,
                ResolutionCompliance = resolvedCount == 0 ? 0 : Math.Round(100.0 * resMet / resolvedCount, 1),
                ResponseCompliance = (respMet + respBreached) == 0 ? 0 : Math.Round(100.0 * respMet / (respMet + respBreached), 1),
                AvgResolutionHours = avgResolutionHours,
                AvgResponseHours = avgResponseHours,
                ByPriority = byPriority.OrderByDescending(g => g.Priority).Select(g => new SlaByPriority(
                    g.Priority.ToString(), g.Total, g.Met, g.Done - g.Met,
                    g.Done == 0 ? 0 : Math.Round(100.0 * g.Met / g.Done, 1),
                    g.Done == 0 ? 0 : Math.Round((g.AvgMinutes ?? 0) / 60.0, 1))).ToList(),
                BreachesByCategory = breachesByCategory
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.Key) ? "Uncategorised" : x.Key)
                    .Select(g => new NameCount(g.Key, g.Sum(x => x.Count)))
                    .OrderByDescending(x => x.Count).ToList()
            };
            return View(vm);
        }

        // 👷 AGENT PERFORMANCE
        public async Task<IActionResult> AgentPerformance()
        {
            var now = _clock.GetLocalNow().DateTime;

            // Grouped by assignee with the name columns in the key, so the agent name arrives from the
            // same query — no second pass and no full Users dictionary loaded to look it up.
            var rows = await _context.Tickets.AsNoTracking()
                .Where(t => t.AssignedToId != null)
                .GroupBy(t => new { t.AssignedToId, t.AssignedTo!.FirstName, t.AssignedTo.LastName })
                .Select(g => new
                {
                    g.Key.AssignedToId,
                    g.Key.FirstName,
                    g.Key.LastName,
                    Assigned = g.Count(),
                    Open = g.Count(t => t.Status != Ticket.TicketStatus.Resolved && t.Status != Ticket.TicketStatus.Closed),
                    Resolved = g.Count(t => t.ResolvedAt != null || t.ClosedAt != null),
                    AvgMinutes = g.Where(t => t.ResolvedAt != null || t.ClosedAt != null)
                        .Average(t => (double?)EF.Functions.DateDiffMinute(t.CreatedAt, t.ResolvedAt ?? t.ClosedAt)),
                    Breached = g.Count(t => t.DueAt != null &&
                        (((t.ResolvedAt ?? t.ClosedAt) != null && (t.ResolvedAt ?? t.ClosedAt) > t.DueAt)
                         || (t.Status != Ticket.TicketStatus.Resolved && t.Status != Ticket.TicketStatus.Closed && t.DueAt < now))),
                    Csat = g.Average(t => (double?)t.SatisfactionRating)
                })
                .ToListAsync();

            var vm = new AgentPerformanceVM
            {
                Unassigned = await _context.Tickets.CountAsync(t => t.AssignedToId == null),
                Agents = rows.Select(r => new AgentRow(
                        string.IsNullOrWhiteSpace(r.FirstName + r.LastName)
                            ? "User #" + r.AssignedToId : $"{r.FirstName} {r.LastName}",
                        r.Assigned, r.Open, r.Resolved,
                        r.Resolved == 0 ? 0 : Math.Round((r.AvgMinutes ?? 0) / 60.0, 1),
                        r.Breached,
                        r.Csat == null ? null : Math.Round(r.Csat.Value, 1)))
                    .OrderByDescending(a => a.Resolved).ToList()
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
        public async Task<IActionResult> TicketTrends()
        {
            var today = _clock.GetLocalNow().DateTime;
            var start = new DateTime(today.Year, today.Month, 1).AddMonths(-11);

            // Bucketed in SQL by month offset from `start` (0..11), which avoids both a 12-pass client
            // scan and any dependence on how the provider translates .Year/.Month on a coalesced column.
            var created = await _context.Tickets.AsNoTracking()
                .Where(t => t.CreatedAt >= start)
                .GroupBy(t => EF.Functions.DateDiffMonth(start, t.CreatedAt))
                .Select(g => new { Offset = g.Key, Count = g.Count() })
                .ToListAsync();

            var completed = await _context.Tickets.AsNoTracking()
                .Where(t => (t.ResolvedAt ?? t.ClosedAt) >= start)
                .GroupBy(t => EF.Functions.DateDiffMonth(start, t.ResolvedAt ?? t.ClosedAt))
                .Select(g => new { Offset = g.Key, Count = g.Count() })
                .ToListAsync();

            // CreatedAt is non-nullable so its offset is int; the coalesced completion date is int?.
            var createdByOffset = created.ToDictionary(x => x.Offset, x => x.Count);
            var completedByOffset = completed.Where(x => x.Offset != null)
                .ToDictionary(x => x.Offset!.Value, x => x.Count);

            var vm = new TicketTrendsVM();
            for (int i = 0; i < 12; i++)
            {
                createdByOffset.TryGetValue(i, out int c);
                completedByOffset.TryGetValue(i, out int r);
                vm.Months.Add(new TrendPoint(start.AddMonths(i).ToString("MMM yy"), c, r));
                vm.TotalCreated += c;
                vm.TotalResolved += r;
            }
            vm.PeakValue = vm.Months.Count == 0 ? 1 : Math.Max(1, vm.Months.Max(p => Math.Max(p.Created, p.Resolved)));
            return View(vm);
        }
    }
}
