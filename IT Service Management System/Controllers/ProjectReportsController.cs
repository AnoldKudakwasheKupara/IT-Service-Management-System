using System.Text;
using ClosedXML.Excel;
using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models.Pm;
using IT_Service_Management_System.Services.Pm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Portfolio reporting: the executive report pack, KPI tracking, project templates, and export
    /// to PDF, Excel and CSV.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager",
                   "DepartmentManager", "Finance", "Procurement", "Auditor")]
    public class ProjectReportsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectMetricsService _metrics;
        private readonly ProjectIntelligenceService _intelligence;
        private readonly ProjectSchedulingService _scheduling;
        private readonly ProjectActivityService _activity;

        public ProjectReportsController(ApplicationDbContext db, ProjectMetricsService metrics,
            ProjectIntelligenceService intelligence, ProjectSchedulingService scheduling,
            ProjectActivityService activity)
        {
            _db = db; _metrics = metrics; _intelligence = intelligence;
            _scheduling = scheduling; _activity = activity;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");

        // ════════════════════════════════════════════════════════════════════════
        //  Report hub
        // ════════════════════════════════════════════════════════════════════════

        public IActionResult Index() => View();

        /// <summary>Portfolio performance: status, health, schedule and cost variance per project.</summary>
        public async Task<IActionResult> Portfolio(DateTime? from, DateTime? to, int? departmentId)
        {
            var start = from ?? DateTime.Today.AddMonths(-12);
            var end = to ?? DateTime.Today;

            var rows = await BuildPortfolioRowsAsync(start, end, departmentId);

            ViewBag.From = start; ViewBag.To = end; ViewBag.DepartmentId = departmentId;
            ViewBag.Departments = await _db.Departments.AsNoTracking()
                .OrderBy(d => d.Name).Select(d => new { d.Id, d.Name }).ToListAsync();

            ViewBag.TotalBudget = rows.Sum(r => r.Budget);
            ViewBag.TotalSpent = rows.Sum(r => r.Spent);
            ViewBag.OnTime = rows.Count(r => r.ScheduleVariance >= 0);
            ViewBag.OverBudget = rows.Count(r => r.Spent > r.Budget && r.Budget > 0);

            return View(rows);
        }

        /// <summary>One row per project on the portfolio report — the shape every export shares.</summary>
        public record PortfolioRow(int Id, string Reference, string Name, string? Department, string? Manager,
            ProjectStatus Status, ProjectHealth Health, int Progress, int ScheduleElapsed,
            DateTime? Start, DateTime? End, decimal Budget, decimal Spent, int OpenRisks, int OpenIssues, decimal Hours)
        {
            public int ScheduleVariance => Progress - ScheduleElapsed;
            public decimal CostVariance => Budget - Spent;
            public int BudgetUsedPercent => Budget <= 0 ? 0 : (int)Math.Round(Spent / Budget * 100);
        }

        private async Task<List<PortfolioRow>> BuildPortfolioRowsAsync(DateTime from, DateTime to, int? departmentId)
        {
            IQueryable<Project> query = _db.Projects.AsNoTracking()
                .Include(p => p.Department).Include(p => p.ProjectManager)
                .Where(p => p.CreatedAt >= from && p.CreatedAt <= to.AddDays(1));
            if (departmentId.HasValue) query = query.Where(p => p.DepartmentId == departmentId.Value);

            var projects = await query.ToListAsync();
            var ids = projects.Select(p => p.Id).ToList();

            var spend = await _metrics.SpendByProjectAsync();
            var risks = await _db.ProjectRisks.Where(r => ids.Contains(r.ProjectId) && r.Status != PmRiskStatus.Closed)
                .GroupBy(r => r.ProjectId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            var issues = await _db.ProjectIssues.Where(i => ids.Contains(i.ProjectId)
                    && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed)
                .GroupBy(i => i.ProjectId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            var hours = await _db.TimeEntries.Where(t => ids.Contains(t.ProjectId))
                .GroupBy(t => t.ProjectId).Select(g => new { g.Key, Total = g.Sum(x => x.Hours - x.BreakHours) })
                .ToDictionaryAsync(x => x.Key, x => x.Total);

            return projects.Select(p => new PortfolioRow(
                p.Id, p.Reference, p.Name, p.Department?.Name,
                p.ProjectManager == null ? null : $"{p.ProjectManager.FirstName} {p.ProjectManager.LastName}",
                p.Status, p.Health, p.ProgressPercent, p.SchedulePercentElapsed,
                p.StartDate, p.EndDate, p.TotalBudget, spend.GetValueOrDefault(p.Id),
                risks.GetValueOrDefault(p.Id), issues.GetValueOrDefault(p.Id), hours.GetValueOrDefault(p.Id)))
            .OrderBy(r => r.Status).ThenBy(r => r.Name)
            .ToList();
        }

        /// <summary>The single-project status report, ready to take into a steering meeting.</summary>
        public async Task<IActionResult> StatusReport(int projectId)
        {
            var project = await _db.Projects.AsNoTracking()
                .Include(p => p.ProjectManager).Include(p => p.Sponsor).Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return NotFound();

            ViewBag.Summary = await _intelligence.ExecutiveSummaryAsync(projectId);
            ViewBag.ScheduleForecast = await _intelligence.ForecastCompletionAsync(projectId);
            ViewBag.BudgetForecast = await _intelligence.ForecastBudgetAsync(projectId);
            ViewBag.Spent = await _metrics.ActualSpendAsync(projectId);

            ViewBag.Milestones = await _db.Milestones.AsNoTracking()
                .Where(m => m.ProjectId == projectId).OrderBy(m => m.DueDate).ToListAsync();
            ViewBag.TopRisks = (await _db.ProjectRisks.AsNoTracking().Include(r => r.Owner)
                .Where(r => r.ProjectId == projectId && r.Status != PmRiskStatus.Closed).ToListAsync())
                .OrderByDescending(r => r.Score).Take(8).ToList();
            ViewBag.OpenIssues = await _db.ProjectIssues.AsNoTracking().Include(i => i.AssignedTo)
                .Where(i => i.ProjectId == projectId && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed)
                .OrderByDescending(i => i.Severity).ToListAsync();
            ViewBag.TaskStats = await _db.ProjectTasks.AsNoTracking()
                .Where(t => t.ProjectId == projectId)
                .GroupBy(t => t.Status).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            ViewBag.Hours = await _db.TimeEntries.Where(t => t.ProjectId == projectId)
                .SumAsync(t => (decimal?)(t.Hours - t.BreakHours)) ?? 0m;

            return View(project);
        }

        /// <summary>Portfolio risk and issue exposure across every open project.</summary>
        public async Task<IActionResult> RiskAndIssues()
        {
            var risks = await _db.ProjectRisks.AsNoTracking()
                .Include(r => r.Project).Include(r => r.Owner)
                .Where(r => r.Status != PmRiskStatus.Closed).ToListAsync();

            var issues = await _db.ProjectIssues.AsNoTracking()
                .Include(i => i.Project).Include(i => i.AssignedTo)
                .Where(i => i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed).ToListAsync();

            // Portfolio-wide 5×5 heat map.
            var matrix = new int[5, 5];
            foreach (var risk in risks)
                matrix[Math.Clamp(risk.Impact, 1, 5) - 1, Math.Clamp(risk.Probability, 1, 5) - 1]++;

            ViewBag.Matrix = matrix;
            ViewBag.Risks = risks.OrderByDescending(r => r.Score).Take(30).ToList();
            ViewBag.Issues = issues.OrderByDescending(i => i.Severity).ThenBy(i => i.DueDate ?? DateTime.MaxValue).Take(30).ToList();
            ViewBag.CriticalRisks = risks.Count(r => r.Score >= 15);
            ViewBag.OverdueIssues = issues.Count(i => i.IsOverdue);
            ViewBag.TotalContingency = risks.Sum(r => r.ContingencyAmount);
            ViewBag.ByCategory = risks.GroupBy(r => r.Category ?? "Uncategorised")
                .OrderByDescending(g => g.Count()).Take(8)
                .ToDictionary(g => g.Key, g => g.Count());

            return View();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  KPIs
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Portfolio KPIs. The headline six are computed live from the record; anything else the
        /// organisation wants to track is stored and maintained by hand.
        /// </summary>
        public async Task<IActionResult> Kpis(int? projectId)
        {
            var projects = await _db.Projects.AsNoTracking().ToListAsync();
            var completed = projects.Where(p => p.Status == ProjectStatus.Completed).ToList();

            ViewBag.ProjectsCompleted = completed.Count;
            ViewBag.ProjectsDelayed = projects.Count(p => p.Status == ProjectStatus.Delayed || p.IsOverdue);

            // Average calendar days from actual start to actual finish.
            var durations = completed
                .Where(p => p.ActualStartDate.HasValue && p.ActualEndDate.HasValue)
                .Select(p => (p.ActualEndDate!.Value - p.ActualStartDate!.Value).TotalDays)
                .ToList();
            ViewBag.AverageCompletionDays = durations.Count == 0 ? 0 : (int)Math.Round(durations.Average());

            // Cost variance as a percentage of budget, across projects that had one.
            var spend = await _metrics.SpendByProjectAsync();
            var withBudget = projects.Where(p => p.TotalBudget > 0).ToList();
            ViewBag.BudgetVariancePercent = withBudget.Count == 0 ? 0
                : (int)Math.Round(withBudget.Average(p => (double)((p.TotalBudget - spend.GetValueOrDefault(p.Id)) / p.TotalBudget * 100)));

            var workload = await _metrics.ResourceWorkloadAsync(DateTime.Today.AddDays(-28), DateTime.Today, 200);
            ViewBag.ResourceUtilisation = workload.Count == 0 ? 0 : (int)Math.Round(workload.Average(w => (double)w.UtilisationPercent));

            // Delivered value against cost on completed work — a simple profitability proxy.
            var billableHours = await _db.TimeEntries.Where(t => t.IsBillable && t.Status == TimeEntryStatus.Approved)
                .SumAsync(t => (decimal?)(t.Hours - t.BreakHours)) ?? 0m;
            var totalHours = await _db.TimeEntries.Where(t => t.Status == TimeEntryStatus.Approved)
                .SumAsync(t => (decimal?)(t.Hours - t.BreakHours)) ?? 0m;
            ViewBag.BillableUtilisation = totalHours <= 0 ? 0 : (int)Math.Round(billableHours / totalHours * 100);

            ViewBag.OnTimeDeliveryPercent = completed.Count == 0 ? 0
                : (int)Math.Round(completed.Count(p => p.BaselineEndDate == null || p.ActualEndDate <= p.BaselineEndDate)
                    * 100.0 / completed.Count);

            ViewBag.Custom = await _db.ProjectKpis.AsNoTracking()
                .Include(k => k.Project).Include(k => k.Owner)
                .Where(k => projectId == null || k.ProjectId == projectId)
                .OrderByDescending(k => k.PeriodEnd).ToListAsync();

            ViewBag.ProjectId = projectId;
            ViewBag.Projects = await _db.Projects.AsNoTracking()
                .OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync();
            ViewBag.Users = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive).OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();
            ViewBag.CanManage = Roles.IsPmManager(Role);

            return View();
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager")]
        public async Task<IActionResult> SaveKpi(ProjectKpi input)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "A KPI needs a name.";
                return RedirectToAction(nameof(Kpis));
            }
            if (input.ProjectId == 0) input.ProjectId = null;

            if (input.Id == 0)
            {
                input.CreatedAt = DateTime.Now;
                _db.ProjectKpis.Add(input);
            }
            else
            {
                var kpi = await _db.ProjectKpis.FirstOrDefaultAsync(k => k.Id == input.Id);
                if (kpi == null) return NotFound();

                kpi.Name = input.Name;
                kpi.Description = input.Description;
                kpi.Unit = input.Unit;
                kpi.TargetValue = input.TargetValue;
                kpi.ActualValue = input.ActualValue;
                kpi.HigherIsBetter = input.HigherIsBetter;
                kpi.PeriodStart = input.PeriodStart;
                kpi.PeriodEnd = input.PeriodEnd;
                kpi.OwnerId = input.OwnerId;
                kpi.ProjectId = input.ProjectId;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "KPI saved.";
            return RedirectToAction(nameof(Kpis), new { projectId = input.ProjectId });
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager")]
        public async Task<IActionResult> DeleteKpi(int id)
        {
            var kpi = await _db.ProjectKpis.FirstOrDefaultAsync(k => k.Id == id);
            if (kpi != null) { _db.ProjectKpis.Remove(kpi); await _db.SaveChangesAsync(); }
            return RedirectToAction(nameof(Kpis));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Templates
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Templates()
        {
            var templates = await _db.ProjectTemplates.AsNoTracking()
                .Include(t => t.CreatedBy).OrderBy(t => t.Category).ThenBy(t => t.Name).ToListAsync();

            var ids = templates.Select(t => t.Id).ToList();
            ViewBag.ItemCounts = await _db.ProjectTemplateItems.AsNoTracking()
                .Where(i => ids.Contains(i.TemplateId))
                .GroupBy(i => i.TemplateId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            ViewBag.UsageCounts = await _db.Projects.AsNoTracking()
                .Where(p => p.CreatedFromTemplateId != null)
                .GroupBy(p => p.CreatedFromTemplateId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            ViewBag.CanManage = Roles.IsPmManager(Role);
            return View(templates);
        }

        public async Task<IActionResult> TemplateDetails(int id)
        {
            var template = await _db.ProjectTemplates.AsNoTracking()
                .Include(t => t.CreatedBy).FirstOrDefaultAsync(t => t.Id == id);
            if (template == null) return NotFound();

            ViewBag.Items = await _db.ProjectTemplateItems.AsNoTracking()
                .Where(i => i.TemplateId == id).OrderBy(i => i.Sequence).ToListAsync();
            ViewBag.CanManage = Roles.IsPmManager(Role);

            return View(template);
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager")]
        public async Task<IActionResult> SaveTemplate(ProjectTemplate input)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "A template needs a name.";
                return RedirectToAction(nameof(Templates));
            }

            if (input.Id == 0)
            {
                input.CreatedById = Uid;
                input.CreatedAt = DateTime.Now;
                input.IsSystem = false;
                _db.ProjectTemplates.Add(input);
            }
            else
            {
                var template = await _db.ProjectTemplates.FirstOrDefaultAsync(t => t.Id == input.Id);
                if (template == null) return NotFound();

                template.Name = input.Name;
                template.Description = input.Description;
                template.Category = input.Category;
                template.Type = input.Type;
                template.DefaultDurationDays = input.DefaultDurationDays;
                template.DefaultBudget = input.DefaultBudget;
                template.IsActive = input.IsActive;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Template saved.";
            return RedirectToAction(nameof(Templates));
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager")]
        public async Task<IActionResult> SaveTemplateItem(ProjectTemplateItem input)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "A template item needs a name.";
                return RedirectToAction(nameof(TemplateDetails), new { id = input.TemplateId });
            }
            if (input.ParentSequence == 0) input.ParentSequence = null;

            if (input.Id == 0)
            {
                input.Sequence = await _db.ProjectTemplateItems.CountAsync(i => i.TemplateId == input.TemplateId) + 1;
                _db.ProjectTemplateItems.Add(input);
            }
            else
            {
                var item = await _db.ProjectTemplateItems.FirstOrDefaultAsync(i => i.Id == input.Id);
                if (item == null) return NotFound();

                item.ItemType = input.ItemType;
                item.Name = input.Name;
                item.Description = input.Description;
                item.StartOffsetDays = input.StartOffsetDays;
                item.DurationDays = input.DurationDays;
                item.EstimatedHours = input.EstimatedHours;
                item.ParentSequence = input.ParentSequence;
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(TemplateDetails), new { id = input.TemplateId });
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager")]
        public async Task<IActionResult> DeleteTemplateItem(int templateId, int itemId)
        {
            var item = await _db.ProjectTemplateItems.FirstOrDefaultAsync(i => i.Id == itemId && i.TemplateId == templateId);
            if (item != null) { _db.ProjectTemplateItems.Remove(item); await _db.SaveChangesAsync(); }
            return RedirectToAction(nameof(TemplateDetails), new { id = templateId });
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager")]
        public async Task<IActionResult> DeleteTemplate(int id)
        {
            var template = await _db.ProjectTemplates.FirstOrDefaultAsync(t => t.Id == id);
            if (template == null) return NotFound();

            if (template.IsSystem)
            {
                TempData["Error"] = "Built-in templates cannot be deleted. Deactivate it instead.";
                return RedirectToAction(nameof(Templates));
            }

            _db.ProjectTemplates.Remove(template);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Template removed.";
            return RedirectToAction(nameof(Templates));
        }

        /// <summary>Capture a live project's plan as a reusable template.</summary>
        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager")]
        public async Task<IActionResult> TemplateFromProject(int projectId, string name)
        {
            var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return NotFound();

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Give the new template a name.";
                return RedirectToAction(nameof(Templates));
            }

            var template = new ProjectTemplate
            {
                Name = name.Trim(),
                Description = $"Captured from {project.Reference} — {project.Name}.",
                Category = project.Category,
                Type = project.Type,
                DefaultDurationDays = project.EstimatedDurationDays ?? 90,
                DefaultBudget = project.Budget,
                CreatedById = Uid,
                IsActive = true
            };
            _db.ProjectTemplates.Add(template);
            await _db.SaveChangesAsync();

            var origin = project.StartDate ?? project.CreatedAt.Date;
            var sequence = 1;

            var phases = await _db.ProjectPhases.AsNoTracking()
                .Where(p => p.ProjectId == projectId).OrderBy(p => p.Sequence).ToListAsync();
            var phaseSequences = new Dictionary<int, int>();

            foreach (var phase in phases)
            {
                phaseSequences[phase.Id] = sequence;
                _db.ProjectTemplateItems.Add(new ProjectTemplateItem
                {
                    TemplateId = template.Id,
                    ItemType = "Phase",
                    Name = phase.Name,
                    Description = phase.Description,
                    Sequence = sequence++,
                    StartOffsetDays = phase.StartDate.HasValue ? Math.Max(0, (int)(phase.StartDate.Value - origin).TotalDays) : 0,
                    DurationDays = phase.StartDate.HasValue && phase.EndDate.HasValue
                        ? Math.Max(1, (int)(phase.EndDate.Value - phase.StartDate.Value).TotalDays) : 14
                });
            }

            var tasks = await _db.ProjectTasks.AsNoTracking()
                .Where(t => t.ProjectId == projectId && t.Status != ProjectTaskStatus.Cancelled)
                .OrderBy(t => t.StartDate).ToListAsync();
            foreach (var task in tasks)
            {
                _db.ProjectTemplateItems.Add(new ProjectTemplateItem
                {
                    TemplateId = template.Id,
                    ItemType = "Task",
                    Name = task.Name,
                    Description = task.Description,
                    Sequence = sequence++,
                    ParentSequence = task.PhaseId.HasValue && phaseSequences.TryGetValue(task.PhaseId.Value, out var ps) ? ps : null,
                    StartOffsetDays = task.StartDate.HasValue ? Math.Max(0, (int)(task.StartDate.Value - origin).TotalDays) : 0,
                    DurationDays = task.StartDate.HasValue && task.DueDate.HasValue
                        ? Math.Max(1, (int)(task.DueDate.Value - task.StartDate.Value).TotalDays) : 1,
                    EstimatedHours = task.EstimatedHours
                });
            }

            var milestones = await _db.Milestones.AsNoTracking()
                .Where(m => m.ProjectId == projectId).OrderBy(m => m.DueDate).ToListAsync();
            foreach (var milestone in milestones)
            {
                _db.ProjectTemplateItems.Add(new ProjectTemplateItem
                {
                    TemplateId = template.Id,
                    ItemType = "Milestone",
                    Name = milestone.Name,
                    Description = milestone.Description,
                    Sequence = sequence++,
                    ParentSequence = milestone.PhaseId.HasValue && phaseSequences.TryGetValue(milestone.PhaseId.Value, out var ms) ? ms : null,
                    StartOffsetDays = Math.Max(0, (int)(milestone.DueDate - origin).TotalDays),
                    DurationDays = 0
                });
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Template “{template.Name}” created with {sequence - 1} item(s).";
            return RedirectToAction(nameof(TemplateDetails), new { id = template.Id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Exports
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> ExportPortfolioCsv(DateTime? from, DateTime? to, int? departmentId)
        {
            var rows = await BuildPortfolioRowsAsync(from ?? DateTime.Today.AddMonths(-12), to ?? DateTime.Today, departmentId);

            var csv = new StringBuilder();
            csv.AppendLine("Reference,Project,Department,Manager,Status,Health,Progress %,Schedule elapsed %," +
                           "Schedule variance,Start,End,Budget,Spent,Cost variance,Budget used %,Open risks,Open issues,Hours");
            foreach (var r in rows)
                csv.AppendLine(string.Join(",", Csv(r.Reference), Csv(r.Name), Csv(r.Department), Csv(r.Manager),
                    r.Status, r.Health, r.Progress, r.ScheduleElapsed, r.ScheduleVariance,
                    r.Start?.ToString("yyyy-MM-dd"), r.End?.ToString("yyyy-MM-dd"),
                    r.Budget.ToString("0.00"), r.Spent.ToString("0.00"), r.CostVariance.ToString("0.00"),
                    r.BudgetUsedPercent, r.OpenRisks, r.OpenIssues, r.Hours.ToString("0.00")));

            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv",
                $"project-portfolio-{DateTime.Today:yyyyMMdd}.csv");
        }

        public async Task<IActionResult> ExportPortfolioExcel(DateTime? from, DateTime? to, int? departmentId)
        {
            var rows = await BuildPortfolioRowsAsync(from ?? DateTime.Today.AddMonths(-12), to ?? DateTime.Today, departmentId);

            using var workbook = new XLWorkbook();
            var sheet = workbook.AddWorksheet("Portfolio");

            string[] headers =
            {
                "Reference", "Project", "Department", "Manager", "Status", "Health", "Progress %",
                "Schedule elapsed %", "Schedule variance", "Start", "End", "Budget", "Spent",
                "Cost variance", "Budget used %", "Open risks", "Open issues", "Hours"
            };
            for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
            sheet.Row(1).Style.Font.Bold = true;
            sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#f1f5f9");

            var row = 2;
            foreach (var r in rows)
            {
                sheet.Cell(row, 1).Value = r.Reference;
                sheet.Cell(row, 2).Value = r.Name;
                sheet.Cell(row, 3).Value = r.Department ?? "";
                sheet.Cell(row, 4).Value = r.Manager ?? "";
                sheet.Cell(row, 5).Value = r.Status.ToString();
                sheet.Cell(row, 6).Value = r.Health.ToString();
                sheet.Cell(row, 7).Value = r.Progress;
                sheet.Cell(row, 8).Value = r.ScheduleElapsed;
                sheet.Cell(row, 9).Value = r.ScheduleVariance;
                if (r.Start.HasValue) sheet.Cell(row, 10).Value = r.Start.Value;
                if (r.End.HasValue) sheet.Cell(row, 11).Value = r.End.Value;
                sheet.Cell(row, 12).Value = r.Budget;
                sheet.Cell(row, 13).Value = r.Spent;
                sheet.Cell(row, 14).Value = r.CostVariance;
                sheet.Cell(row, 15).Value = r.BudgetUsedPercent;
                sheet.Cell(row, 16).Value = r.OpenRisks;
                sheet.Cell(row, 17).Value = r.OpenIssues;
                sheet.Cell(row, 18).Value = r.Hours;

                // Red rows are the ones a reader should look at first.
                if (r.Health == ProjectHealth.Red)
                    sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#fdecea");
                else if (r.Health == ProjectHealth.Amber)
                    sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#fdf3e1");
                row++;
            }

            sheet.Columns().AdjustToContents();
            sheet.SheetView.FreezeRows(1);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"project-portfolio-{DateTime.Today:yyyyMMdd}.xlsx");
        }

        /// <summary>A printable status report for one project, as PDF.</summary>
        public async Task<IActionResult> ExportProjectPdf(int projectId)
        {
            var project = await _db.Projects.AsNoTracking()
                .Include(p => p.ProjectManager).Include(p => p.Sponsor).Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return NotFound();

            var summary = await _intelligence.ExecutiveSummaryAsync(projectId);
            var spent = await _metrics.ActualSpendAsync(projectId);
            var milestones = await _db.Milestones.AsNoTracking()
                .Where(m => m.ProjectId == projectId).OrderBy(m => m.DueDate).Take(12).ToListAsync();
            var risks = (await _db.ProjectRisks.AsNoTracking()
                .Where(r => r.ProjectId == projectId && r.Status != PmRiskStatus.Closed).ToListAsync())
                .OrderByDescending(r => r.Score).Take(8).ToList();

            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Segoe UI"));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Project Status Report").FontSize(18).Bold().FontColor("#b11d23");
                        col.Item().Text($"{project.Reference} — {project.Name}").FontSize(12).SemiBold();
                        col.Item().Text($"Generated {DateTime.Now:d MMMM yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(6).LineHorizontal(1).LineColor("#e2e8f0");
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Spacing(12);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });

                            void Cell(string label, string value)
                            {
                                table.Cell().Padding(3).Column(inner =>
                                {
                                    inner.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken1);
                                    inner.Item().Text(value).SemiBold();
                                });
                            }

                            Cell("Status", project.Status.ToString());
                            Cell("Health", project.Health.ToString());
                            Cell("Progress", $"{project.ProgressPercent}%");
                            Cell("Schedule elapsed", $"{project.SchedulePercentElapsed}%");
                            Cell("Manager", project.ProjectManager == null ? "—" : $"{project.ProjectManager.FirstName} {project.ProjectManager.LastName}");
                            Cell("Department", project.Department?.Name ?? "—");
                            Cell("Start", project.StartDate?.ToString("d MMM yyyy") ?? "—");
                            Cell("End", project.EndDate?.ToString("d MMM yyyy") ?? "—");
                            Cell("Budget", $"{project.Currency} {project.TotalBudget:N2}");
                            Cell("Spent", $"{project.Currency} {spent:N2}");
                            Cell("Remaining", $"{project.Currency} {project.TotalBudget - spent:N2}");
                            Cell("Client", project.Client ?? "—");
                        });

                        col.Item().Column(section =>
                        {
                            section.Item().Text("Executive summary").FontSize(12).Bold().FontColor("#b11d23");
                            section.Item().PaddingTop(4).Text(summary).LineHeight(1.4f);
                        });

                        if (milestones.Count > 0)
                        {
                            col.Item().Column(section =>
                            {
                                section.Item().Text("Milestones").FontSize(12).Bold().FontColor("#b11d23");
                                section.Item().PaddingTop(4).Table(table =>
                                {
                                    table.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); });
                                    table.Header(h =>
                                    {
                                        foreach (var title in new[] { "Milestone", "Due", "Status", "Slippage" })
                                            h.Cell().Background("#f1f5f9").Padding(4).Text(title).SemiBold().FontSize(8);
                                    });
                                    foreach (var m in milestones)
                                    {
                                        table.Cell().Padding(4).Text(m.Name);
                                        table.Cell().Padding(4).Text(m.DueDate.ToString("d MMM yyyy"));
                                        table.Cell().Padding(4).Text(m.Status.ToString());
                                        table.Cell().Padding(4).Text(m.SlippageDays > 0 ? $"{m.SlippageDays} d" : "—");
                                    }
                                });
                            });
                        }

                        if (risks.Count > 0)
                        {
                            col.Item().Column(section =>
                            {
                                section.Item().Text("Top risks").FontSize(12).Bold().FontColor("#b11d23");
                                section.Item().PaddingTop(4).Table(table =>
                                {
                                    table.ColumnsDefinition(c => { c.RelativeColumn(5); c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(4); });
                                    table.Header(h =>
                                    {
                                        foreach (var title in new[] { "Risk", "Score", "Status", "Mitigation" })
                                            h.Cell().Background("#f1f5f9").Padding(4).Text(title).SemiBold().FontSize(8);
                                    });
                                    foreach (var r in risks)
                                    {
                                        table.Cell().Padding(4).Text(r.Title);
                                        table.Cell().Padding(4).Text(r.Score.ToString());
                                        table.Cell().Padding(4).Text(r.Status.ToString());
                                        table.Cell().Padding(4).Text(r.Mitigation ?? "—").FontSize(8);
                                    }
                                });
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Axis IT Operations · Project Management · Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        t.CurrentPageNumber().FontSize(8);
                        t.Span(" of ").FontSize(8);
                        t.TotalPages().FontSize(8);
                    });
                });
            });

            return File(document.GeneratePdf(), "application/pdf",
                $"{project.Reference}-status-{DateTime.Today:yyyyMMdd}.pdf");
        }

        private static string Csv(string? value) =>
            string.IsNullOrEmpty(value) ? "" : $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
