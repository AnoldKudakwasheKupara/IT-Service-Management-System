using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models.Pm;
using IT_Service_Management_System.Services.Pm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Time tracking: personal timesheets, the approval queue, and the hours/cost reports that fall
    /// out of them. Approved time is costed at the resource's rate and feeds project actual spend.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager", "TeamLead",
                   "DepartmentManager", "Finance", "Procurement", "Auditor", "Employee", "HR")]
    public class ProjectTimeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectMetricsService _metrics;
        private readonly ProjectActivityService _activity;

        public ProjectTimeController(ApplicationDbContext db, ProjectMetricsService metrics, ProjectActivityService activity)
        {
            _db = db; _metrics = metrics; _activity = activity;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool CanApprove => Roles.IsPmManager(Role) || Role is Roles.TeamLead or Roles.DepartmentManager or Roles.Finance;
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        // ════════════════════════════════════════════════════════════════════════
        //  My timesheet
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>The signed-in user's week. Defaults to the current week, Monday to Sunday.</summary>
        public async Task<IActionResult> Index(DateTime? weekOf)
        {
            var anchor = (weekOf ?? DateTime.Today).Date;
            var monday = anchor.AddDays(-(((int)anchor.DayOfWeek + 6) % 7));
            var sunday = monday.AddDays(6);

            var entries = await _db.TimeEntries.AsNoTracking()
                .Include(t => t.Project).Include(t => t.Task).Include(t => t.ApprovedBy)
                .Where(t => t.UserId == Uid && t.WorkDate >= monday && t.WorkDate <= sunday)
                .OrderBy(t => t.WorkDate).ThenBy(t => t.Id)
                .ToListAsync();

            ViewBag.Monday = monday;
            ViewBag.Sunday = sunday;
            ViewBag.Days = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).ToList();
            ViewBag.TotalHours = entries.Sum(e => e.NetHours);
            ViewBag.BillableHours = entries.Where(e => e.IsBillable).Sum(e => e.NetHours);
            ViewBag.OvertimeHours = entries.Where(e => e.Type == TimeEntryType.Overtime).Sum(e => e.NetHours);
            ViewBag.DraftCount = entries.Count(e => e.Status == TimeEntryStatus.Draft);

            // Only projects the user is actually on can be booked against.
            ViewBag.MyProjects = await MyProjectsAsync();
            ViewBag.MyTasks = await _db.ProjectTasks.AsNoTracking()
                .Where(t => t.AssignedToId == Uid && t.Status != ProjectTaskStatus.Completed && t.Status != ProjectTaskStatus.Cancelled)
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.ProjectId, t.Name })
                .ToListAsync();

            return View(entries);
        }

        [HttpPost]
        public async Task<IActionResult> Log(TimeEntry input)
        {
            if (input.Hours <= 0 || input.Hours > 24)
            {
                TempData["Error"] = "Hours must be between 0 and 24.";
                return RedirectToAction(nameof(Index), new { weekOf = input.WorkDate });
            }
            if (input.BreakHours < 0 || input.BreakHours >= input.Hours)
            {
                TempData["Error"] = "Break time must be less than the hours worked.";
                return RedirectToAction(nameof(Index), new { weekOf = input.WorkDate });
            }
            if (input.WorkDate > DateTime.Today)
            {
                TempData["Error"] = "Time cannot be booked against a future date.";
                return RedirectToAction(nameof(Index), new { weekOf = DateTime.Today });
            }

            // Time is always logged as the signed-in user — never on someone else's behalf.
            input.UserId = Uid;
            input.Status = TimeEntryStatus.Draft;
            input.CreatedAt = DateTime.Now;
            input.CostRate = await CostRateForAsync(Uid);
            if (input.TaskId == 0) input.TaskId = null;

            _db.TimeEntries.Add(input);
            await _db.SaveChangesAsync();

            // Keep the task's actual hours in step with what has been booked against it.
            if (input.TaskId is int taskId) await RefreshTaskHoursAsync(taskId);

            TempData["Success"] = $"{input.NetHours:N2} hour(s) logged.";
            return RedirectToAction(nameof(Index), new { weekOf = input.WorkDate });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteEntry(int id)
        {
            var entry = await _db.TimeEntries.FirstOrDefaultAsync(t => t.Id == id);
            if (entry == null) return NotFound();

            // Only your own draft or rejected time is yours to delete.
            if (entry.UserId != Uid && !Roles.IsFullAccess(Role)) return AccessDenied();
            if (entry.Status == TimeEntryStatus.Approved && !Roles.IsFullAccess(Role))
            {
                TempData["Error"] = "Approved time cannot be deleted. Ask an administrator to reverse it.";
                return RedirectToAction(nameof(Index), new { weekOf = entry.WorkDate });
            }

            var week = entry.WorkDate;
            var taskId = entry.TaskId;
            _db.TimeEntries.Remove(entry);
            await _db.SaveChangesAsync();
            if (taskId is int t) await RefreshTaskHoursAsync(t);

            TempData["Success"] = "Time entry removed.";
            return RedirectToAction(nameof(Index), new { weekOf = week });
        }

        /// <summary>Submit a week's draft entries for approval, as one batch.</summary>
        [HttpPost]
        public async Task<IActionResult> SubmitWeek(DateTime weekOf)
        {
            var monday = weekOf.Date.AddDays(-(((int)weekOf.DayOfWeek + 6) % 7));
            var sunday = monday.AddDays(6);

            var entries = await _db.TimeEntries
                .Where(t => t.UserId == Uid && t.WorkDate >= monday && t.WorkDate <= sunday
                            && t.Status == TimeEntryStatus.Draft)
                .ToListAsync();

            if (entries.Count == 0)
            {
                TempData["Error"] = "There is no draft time to submit for that week.";
                return RedirectToAction(nameof(Index), new { weekOf = monday });
            }

            foreach (var entry in entries) entry.Status = TimeEntryStatus.Submitted;
            await _db.SaveChangesAsync();

            // Each project manager whose project received time hears about it once.
            var projectIds = entries.Select(e => e.ProjectId).Distinct().ToList();
            var managers = await _db.Projects.AsNoTracking()
                .Where(p => projectIds.Contains(p.Id) && p.ProjectManagerId != null)
                .Select(p => new { p.Id, ManagerId = p.ProjectManagerId!.Value })
                .ToListAsync();

            foreach (var manager in managers.DistinctBy(m => m.ManagerId))
                _activity.Notify(manager.ManagerId, PmNotificationType.ApprovalPending,
                    "Timesheet submitted for approval",
                    $"{entries.Sum(e => e.NetHours):N1} hour(s) for the week of {monday:d MMM yyyy}.",
                    Url.Action(nameof(Approvals)), manager.Id);

            await _db.SaveChangesAsync();

            TempData["Success"] = $"{entries.Count} entr(ies) submitted for approval.";
            return RedirectToAction(nameof(Index), new { weekOf = monday });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Approval queue
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Approvals(int? projectId, int? userId)
        {
            if (!CanApprove) return AccessDenied();

            IQueryable<TimeEntry> query = _db.TimeEntries.AsNoTracking()
                .Include(t => t.User).Include(t => t.Project).Include(t => t.Task)
                .Where(t => t.Status == TimeEntryStatus.Submitted);

            // A project manager sees their own projects; administrators and finance see everything.
            if (!Roles.IsFullAccess(Role) && Role != Roles.Finance)
            {
                var mine = await _db.Projects.AsNoTracking()
                    .Where(p => p.ProjectManagerId == Uid).Select(p => p.Id).ToListAsync();
                query = query.Where(t => mine.Contains(t.ProjectId));
            }

            if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);
            if (userId.HasValue) query = query.Where(t => t.UserId == userId.Value);

            var entries = await query.OrderBy(t => t.WorkDate).ToListAsync();

            ViewBag.ProjectId = projectId; ViewBag.UserId = userId;
            ViewBag.TotalHours = entries.Sum(e => e.NetHours);
            ViewBag.TotalCost = entries.Sum(e => e.Cost);
            ViewBag.Projects = await _db.Projects.AsNoTracking()
                .OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync();
            ViewBag.Users = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive).OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();

            return View(entries);
        }

        [HttpPost]
        public async Task<IActionResult> Decide(int[] entryIds, bool approve, string? reason)
        {
            if (!CanApprove) return AccessDenied();
            if (entryIds.Length == 0)
            {
                TempData["Error"] = "Select at least one entry.";
                return RedirectToAction(nameof(Approvals));
            }

            var entries = await _db.TimeEntries
                .Where(t => entryIds.Contains(t.Id) && t.Status == TimeEntryStatus.Submitted)
                .ToListAsync();

            foreach (var entry in entries)
            {
                // Approving your own time would defeat the point of the queue.
                if (entry.UserId == Uid && !Roles.IsFullAccess(Role)) continue;

                entry.Status = approve ? TimeEntryStatus.Approved : TimeEntryStatus.Rejected;
                entry.ApprovedById = Uid;
                entry.ApprovedAt = DateTime.Now;
                entry.RejectionReason = approve ? null : reason;

                _activity.Notify(entry.UserId, PmNotificationType.ApprovalDecided,
                    approve ? "Time approved" : "Time rejected",
                    $"{entry.NetHours:N2} h on {entry.WorkDate:d MMM yyyy}" + (approve ? "" : $" — {reason}"),
                    Url.Action(nameof(Index), new { weekOf = entry.WorkDate }), entry.ProjectId);
            }

            await _db.SaveChangesAsync();

            // Approved time changes project cost, so refresh every affected project.
            foreach (var projectId in entries.Select(e => e.ProjectId).Distinct())
                await _metrics.RefreshProjectAsync(projectId);

            TempData["Success"] = $"{entries.Count} entr(ies) {(approve ? "approved" : "rejected")}.";
            return RedirectToAction(nameof(Approvals));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Reports
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Hours and cost sliced by employee and by project over a date range.</summary>
        public async Task<IActionResult> Report(DateTime? from, DateTime? to, int? projectId, bool approvedOnly = true)
        {
            var start = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var end = to ?? DateTime.Today;

            IQueryable<TimeEntry> query = _db.TimeEntries.AsNoTracking()
                .Include(t => t.User).Include(t => t.Project)
                .Where(t => t.WorkDate >= start && t.WorkDate <= end);

            if (approvedOnly) query = query.Where(t => t.Status == TimeEntryStatus.Approved);
            if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);

            var entries = await query.ToListAsync();

            ViewBag.From = start; ViewBag.To = end;
            ViewBag.ProjectId = projectId; ViewBag.ApprovedOnly = approvedOnly;

            ViewBag.ByEmployee = entries
                .GroupBy(e => new { e.UserId, Name = e.User == null ? "—" : $"{e.User.FirstName} {e.User.LastName}" })
                .Select(g => new
                {
                    g.Key.Name,
                    Hours = g.Sum(x => x.NetHours),
                    Billable = g.Where(x => x.IsBillable).Sum(x => x.NetHours),
                    Overtime = g.Where(x => x.Type == TimeEntryType.Overtime).Sum(x => x.NetHours),
                    Cost = g.Sum(x => x.Cost),
                    Projects = g.Select(x => x.ProjectId).Distinct().Count()
                })
                .OrderByDescending(x => x.Hours)
                .ToList();

            ViewBag.ByProject = entries
                .GroupBy(e => new { e.ProjectId, Name = e.Project?.Name ?? "—" })
                .Select(g => new
                {
                    g.Key.ProjectId,
                    g.Key.Name,
                    Hours = g.Sum(x => x.NetHours),
                    Billable = g.Where(x => x.IsBillable).Sum(x => x.NetHours),
                    Cost = g.Sum(x => x.Cost),
                    People = g.Select(x => x.UserId).Distinct().Count()
                })
                .OrderByDescending(x => x.Hours)
                .ToList();

            ViewBag.TotalHours = entries.Sum(e => e.NetHours);
            ViewBag.BillableHours = entries.Where(e => e.IsBillable).Sum(e => e.NetHours);
            ViewBag.TotalCost = entries.Sum(e => e.Cost);
            ViewBag.BillablePercent = entries.Sum(e => e.NetHours) <= 0 ? 0
                : (int)Math.Round(entries.Where(e => e.IsBillable).Sum(e => e.NetHours) / entries.Sum(e => e.NetHours) * 100);

            ViewBag.Projects = await _db.Projects.AsNoTracking()
                .OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync();

            return View();
        }

        /// <summary>Download the filtered time report as CSV.</summary>
        public async Task<IActionResult> ExportCsv(DateTime? from, DateTime? to, int? projectId, bool approvedOnly = true)
        {
            var start = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var end = to ?? DateTime.Today;

            IQueryable<TimeEntry> query = _db.TimeEntries.AsNoTracking()
                .Include(t => t.User).Include(t => t.Project).Include(t => t.Task)
                .Where(t => t.WorkDate >= start && t.WorkDate <= end);
            if (approvedOnly) query = query.Where(t => t.Status == TimeEntryStatus.Approved);
            if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);

            var entries = await query.OrderBy(t => t.WorkDate).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Date,Employee,Project,Task,Type,Hours,Break,Net hours,Billable,Status,Rate,Cost,Notes");
            foreach (var e in entries)
            {
                csv.AppendLine(string.Join(",",
                    e.WorkDate.ToString("yyyy-MM-dd"),
                    Csv(e.User == null ? "" : $"{e.User.FirstName} {e.User.LastName}"),
                    Csv(e.Project?.Name),
                    Csv(e.Task?.Name),
                    e.Type,
                    e.Hours.ToString("0.00"),
                    e.BreakHours.ToString("0.00"),
                    e.NetHours.ToString("0.00"),
                    e.IsBillable ? "Yes" : "No",
                    e.Status,
                    e.CostRate.ToString("0.00"),
                    e.Cost.ToString("0.00"),
                    Csv(e.Notes)));
            }

            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv",
                $"time-report-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
        }

        /// <summary>Quote and escape a CSV field so commas, quotes and newlines survive the round trip.</summary>
        private static string Csv(string? value) =>
            string.IsNullOrEmpty(value) ? "" : $"\"{value.Replace("\"", "\"\"")}\"";

        // ════════════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>The hourly cost of a person, taken from their resource record.</summary>
        private async Task<decimal> CostRateForAsync(int userId) =>
            await _db.Resources.AsNoTracking()
                .Where(r => r.UserId == userId)
                .Select(r => (decimal?)r.HourlyRate)
                .FirstOrDefaultAsync() ?? 0m;

        /// <summary>Recompute a task's actual hours from the time booked against it.</summary>
        private async Task RefreshTaskHoursAsync(int taskId)
        {
            var task = await _db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null) return;

            task.ActualHours = await _db.TimeEntries
                .Where(t => t.TaskId == taskId && t.Status != TimeEntryStatus.Rejected)
                .SumAsync(t => (decimal?)(t.Hours - t.BreakHours)) ?? 0m;
            await _db.SaveChangesAsync();
        }

        private async Task<List<object>> MyProjectsAsync()
        {
            var teamProjectIds = await _db.ProjectTeamMembers.AsNoTracking()
                .Where(m => m.UserId == Uid && m.IsActive).Select(m => m.ProjectId).ToListAsync();

            return await _db.Projects.AsNoTracking()
                .Where(p => (p.ProjectManagerId == Uid || teamProjectIds.Contains(p.Id))
                            && p.Status != ProjectStatus.Archived && p.Status != ProjectStatus.Cancelled)
                .OrderBy(p => p.Name)
                .Select(p => (object)new { p.Id, Name = p.Code + " · " + p.Name })
                .ToListAsync();
        }
    }
}
