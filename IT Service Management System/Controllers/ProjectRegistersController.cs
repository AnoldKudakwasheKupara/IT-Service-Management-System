using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers.Pm;
using IT_Service_Management_System.Models.Pm;
using IT_Service_Management_System.Services.Pm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// The project governance registers: risk (with its heat map), issues, change control and
    /// quality checks. They share a controller because they share the same lifecycle shape —
    /// raise, assess, assign, resolve — and the same project-scoped permissions.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager", "TeamLead",
                   "DepartmentManager", "Finance", "Procurement", "Auditor", "Employee", "HR")]
    public class ProjectRegistersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectActivityService _activity;
        private readonly ProjectApprovalService _approvals;
        private readonly ProjectMetricsService _metrics;
        private readonly ProjectIntelligenceService _intelligence;

        public ProjectRegistersController(ApplicationDbContext db, ProjectActivityService activity,
            ProjectApprovalService approvals, ProjectMetricsService metrics, ProjectIntelligenceService intelligence)
        {
            _db = db; _activity = activity; _approvals = approvals;
            _metrics = metrics; _intelligence = intelligence;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        // ════════════════════════════════════════════════════════════════════════
        //  Risk register
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Risks(int projectId, PmRiskStatus? status, string? band)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            var risks = await _db.ProjectRisks.AsNoTracking()
                .Include(r => r.Owner).Include(r => r.CreatedBy)
                .Where(r => r.ProjectId == projectId && (status == null || r.Status == status))
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(band))
                risks = risks.Where(r => r.Band.Equals(band, StringComparison.OrdinalIgnoreCase)).ToList();

            risks = risks.OrderByDescending(r => r.Score).ThenBy(r => r.Status).ToList();

            // 5×5 heat map, indexed [impact-1, probability-1] so it reads bottom-left to top-right.
            var matrix = new int[5, 5];
            foreach (var risk in risks.Where(r => r.Status != PmRiskStatus.Closed))
                matrix[Math.Clamp(risk.Impact, 1, 5) - 1, Math.Clamp(risk.Probability, 1, 5) - 1]++;

            ViewBag.Matrix = matrix;
            ViewBag.Status = status;
            ViewBag.Band = band;
            ViewBag.OpenCount = risks.Count(r => r.Status != PmRiskStatus.Closed);
            ViewBag.CriticalCount = risks.Count(r => r.Status != PmRiskStatus.Closed && r.Score >= 15);
            ViewBag.Contingency = risks.Where(r => r.Status != PmRiskStatus.Closed).Sum(r => r.ContingencyAmount);
            ViewBag.Predictions = await _intelligence.PredictRisksAsync(projectId);
            await PopulateUsersAsync();

            return View(risks);
        }

        [HttpPost]
        public async Task<IActionResult> SaveRisk(ProjectRisk input)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Title))
            {
                TempData["Error"] = "A risk needs a title.";
                return RedirectToAction(nameof(Risks), new { projectId = input.ProjectId });
            }

            input.Probability = Math.Clamp(input.Probability, 1, 5);
            input.Impact = Math.Clamp(input.Impact, 1, 5);

            if (input.Id == 0)
            {
                input.CreatedById = Uid;
                input.CreatedAt = DateTime.Now;
                input.IdentifiedDate ??= DateTime.Today;
                _db.ProjectRisks.Add(input);
                await _db.SaveChangesAsync();

                _activity.Log(input.ProjectId, nameof(ProjectRisk), input.Id, "Created", $"{input.Title} (score {input.Score})");

                // A serious risk is escalated to everyone on the project the moment it is logged.
                if (input.Score >= 10)
                    _activity.NotifyMany(await _activity.ProjectAudienceAsync(input.ProjectId), PmNotificationType.RiskRaised,
                        $"{input.Band} risk raised: {input.Title}",
                        $"Probability {input.Probability} × impact {input.Impact} = {input.Score}.",
                        Url.Action(nameof(Risks), new { projectId = input.ProjectId }), input.ProjectId);
                else if (input.OwnerId is int owner)
                    _activity.Notify(owner, PmNotificationType.RiskRaised, $"You own the risk “{input.Title}”",
                        null, Url.Action(nameof(Risks), new { projectId = input.ProjectId }), input.ProjectId);

                await _db.SaveChangesAsync();
            }
            else
            {
                var risk = await _db.ProjectRisks.FirstOrDefaultAsync(r => r.Id == input.Id && r.ProjectId == input.ProjectId);
                if (risk == null) return NotFound();

                _activity.LogChange(input.ProjectId, nameof(ProjectRisk), risk.Id, "Score", risk.Score, input.Probability * input.Impact);

                risk.Title = input.Title;
                risk.Description = input.Description;
                risk.Category = input.Category;
                risk.Probability = input.Probability;
                risk.Impact = input.Impact;
                risk.OwnerId = input.OwnerId;
                risk.Mitigation = input.Mitigation;
                risk.Response = input.Response;
                risk.ResponsePlan = input.ResponsePlan;
                risk.ContingencyPlan = input.ContingencyPlan;
                risk.ContingencyAmount = input.ContingencyAmount;
                risk.TargetScore = input.TargetScore;
                risk.ReviewDate = input.ReviewDate;
                await _db.SaveChangesAsync();
            }

            await _metrics.RefreshProjectAsync(input.ProjectId);
            TempData["Success"] = "Risk saved.";
            return RedirectToAction(nameof(Risks), new { projectId = input.ProjectId });
        }

        [HttpPost]
        public async Task<IActionResult> SetRiskStatus(int projectId, int riskId, PmRiskStatus status)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            var risk = await _db.ProjectRisks.FirstOrDefaultAsync(r => r.Id == riskId && r.ProjectId == projectId);
            if (risk == null) return NotFound();

            _activity.LogChange(projectId, nameof(ProjectRisk), riskId, "Status", risk.Status, status);
            risk.Status = status;
            risk.ClosedDate = status == PmRiskStatus.Closed ? DateTime.Today : null;
            await _db.SaveChangesAsync();
            await _metrics.RefreshProjectAsync(projectId);

            TempData["Success"] = $"Risk marked {status}.";
            return RedirectToAction(nameof(Risks), new { projectId });
        }

        /// <summary>Turn a risk that has materialised into an issue, keeping the link between them.</summary>
        [HttpPost]
        public async Task<IActionResult> EscalateRiskToIssue(int projectId, int riskId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            var risk = await _db.ProjectRisks.FirstOrDefaultAsync(r => r.Id == riskId && r.ProjectId == projectId);
            if (risk == null) return NotFound();

            var issue = new ProjectIssue
            {
                ProjectId = projectId,
                RaisedFromRiskId = riskId,
                Title = risk.Title,
                Description = risk.Description ?? $"Escalated from risk {risk.Reference}.",
                Severity = risk.Score switch { >= 15 => IssueSeverity.Critical, >= 10 => IssueSeverity.High, >= 5 => IssueSeverity.Medium, _ => IssueSeverity.Low },
                Priority = risk.Score >= 15 ? TaskPriority.Critical : TaskPriority.High,
                AssignedToId = risk.OwnerId,
                RaisedById = Uid,
                DueDate = DateTime.Today.AddDays(7)
            };
            _db.ProjectIssues.Add(issue);

            risk.Status = PmRiskStatus.Realised;
            await _db.SaveChangesAsync();

            _activity.Log(projectId, nameof(ProjectIssue), issue.Id, "Created", $"Escalated from risk {risk.Reference}");
            _activity.NotifyMany(await _activity.ProjectAudienceAsync(projectId), PmNotificationType.IssueRaised,
                $"Risk materialised: {risk.Title}", "It has been raised as an issue and needs resolving.",
                Url.Action(nameof(Issues), new { projectId }), projectId);
            await _db.SaveChangesAsync();
            await _metrics.RefreshProjectAsync(projectId);

            TempData["Success"] = $"Risk escalated to issue {issue.Reference}.";
            return RedirectToAction(nameof(Issues), new { projectId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRisk(int projectId, int riskId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit) return AccessDenied();

            var risk = await _db.ProjectRisks.FirstOrDefaultAsync(r => r.Id == riskId && r.ProjectId == projectId);
            if (risk != null)
            {
                _db.ProjectRisks.Remove(risk);
                _activity.Log(projectId, nameof(ProjectRisk), riskId, "Deleted", risk.Title);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Risk removed from the register.";
            return RedirectToAction(nameof(Risks), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Issue register
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Issues(int projectId, IssueStatus? status, IssueSeverity? severity, int? assignedTo)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            IQueryable<ProjectIssue> query = _db.ProjectIssues.AsNoTracking()
                .Include(i => i.AssignedTo).Include(i => i.RaisedBy).Include(i => i.RaisedFromRisk)
                .Where(i => i.ProjectId == projectId);

            if (status.HasValue) query = query.Where(i => i.Status == status.Value);
            if (severity.HasValue) query = query.Where(i => i.Severity == severity.Value);
            if (assignedTo.HasValue) query = query.Where(i => i.AssignedToId == assignedTo.Value);

            var issues = await query.OrderByDescending(i => i.Severity).ThenBy(i => i.DueDate ?? DateTime.MaxValue).ToListAsync();

            ViewBag.Status = status; ViewBag.Severity = severity; ViewBag.AssignedTo = assignedTo;
            ViewBag.OpenCount = await _db.ProjectIssues.CountAsync(i => i.ProjectId == projectId
                && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);
            ViewBag.CriticalCount = await _db.ProjectIssues.CountAsync(i => i.ProjectId == projectId
                && i.Severity == IssueSeverity.Critical && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);
            ViewBag.OverdueCount = issues.Count(i => i.IsOverdue);
            await PopulateUsersAsync();

            return View(issues);
        }

        [HttpPost]
        public async Task<IActionResult> SaveIssue(ProjectIssue input)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Description))
            {
                TempData["Error"] = "An issue needs both a title and a description.";
                return RedirectToAction(nameof(Issues), new { projectId = input.ProjectId });
            }

            if (input.Id == 0)
            {
                input.RaisedById = Uid;
                input.CreatedAt = DateTime.Now;
                _db.ProjectIssues.Add(input);
                await _db.SaveChangesAsync();

                _activity.Log(input.ProjectId, nameof(ProjectIssue), input.Id, "Created", input.Title);

                if (input.Severity == IssueSeverity.Critical)
                    _activity.NotifyMany(await _activity.ProjectAudienceAsync(input.ProjectId), PmNotificationType.IssueRaised,
                        $"Critical issue raised: {input.Title}", input.Description,
                        Url.Action(nameof(Issues), new { projectId = input.ProjectId }), input.ProjectId);
                else if (input.AssignedToId is int assignee)
                    _activity.Notify(assignee, PmNotificationType.IssueRaised, $"Issue assigned: {input.Title}",
                        input.Description, Url.Action(nameof(Issues), new { projectId = input.ProjectId }), input.ProjectId);

                await _db.SaveChangesAsync();
            }
            else
            {
                var issue = await _db.ProjectIssues.FirstOrDefaultAsync(i => i.Id == input.Id && i.ProjectId == input.ProjectId);
                if (issue == null) return NotFound();

                var previousAssignee = issue.AssignedToId;
                _activity.LogChange(input.ProjectId, nameof(ProjectIssue), issue.Id, "Severity", issue.Severity, input.Severity);

                issue.Title = input.Title;
                issue.Description = input.Description;
                issue.Severity = input.Severity;
                issue.Priority = input.Priority;
                issue.AssignedToId = input.AssignedToId;
                issue.RootCause = input.RootCause;
                issue.Resolution = input.Resolution;
                issue.DueDate = input.DueDate;

                if (input.AssignedToId is int newAssignee && newAssignee != previousAssignee)
                    _activity.Notify(newAssignee, PmNotificationType.IssueRaised, $"Issue assigned: {issue.Title}",
                        issue.Description, Url.Action(nameof(Issues), new { projectId = input.ProjectId }), input.ProjectId);

                await _db.SaveChangesAsync();
            }

            await _metrics.RefreshProjectAsync(input.ProjectId);
            TempData["Success"] = "Issue saved.";
            return RedirectToAction(nameof(Issues), new { projectId = input.ProjectId });
        }

        [HttpPost]
        public async Task<IActionResult> SetIssueStatus(int projectId, int issueId, IssueStatus status, string? resolution)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            var issue = await _db.ProjectIssues.FirstOrDefaultAsync(i => i.Id == issueId && i.ProjectId == projectId);
            if (issue == null) return NotFound();

            // Nothing is closed without saying how it was settled.
            if (status is IssueStatus.Resolved or IssueStatus.Closed
                && string.IsNullOrWhiteSpace(resolution) && string.IsNullOrWhiteSpace(issue.Resolution))
            {
                TempData["Error"] = "Record how the issue was resolved before closing it.";
                return RedirectToAction(nameof(Issues), new { projectId });
            }

            _activity.LogChange(projectId, nameof(ProjectIssue), issueId, "Status", issue.Status, status);
            issue.Status = status;
            if (!string.IsNullOrWhiteSpace(resolution)) issue.Resolution = resolution;
            if (status == IssueStatus.Resolved) issue.ResolvedAt = DateTime.Now;
            if (status == IssueStatus.Closed) { issue.ResolvedAt ??= DateTime.Now; issue.ClosedAt = DateTime.Now; }

            _activity.Notify(issue.RaisedById, PmNotificationType.StatusChanged,
                $"Issue {issue.Reference} is now {status}", issue.Title,
                Url.Action(nameof(Issues), new { projectId }), projectId);

            await _db.SaveChangesAsync();
            await _metrics.RefreshProjectAsync(projectId);

            TempData["Success"] = $"Issue marked {status}.";
            return RedirectToAction(nameof(Issues), new { projectId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteIssue(int projectId, int issueId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit) return AccessDenied();

            var issue = await _db.ProjectIssues.FirstOrDefaultAsync(i => i.Id == issueId && i.ProjectId == projectId);
            if (issue != null)
            {
                _db.ProjectIssues.Remove(issue);
                _activity.Log(projectId, nameof(ProjectIssue), issueId, "Deleted", issue.Title);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Issue removed.";
            return RedirectToAction(nameof(Issues), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Change control
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Changes(int projectId, ChangeRequestStatus? status)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            var changes = await _db.ProjectChangeRequests.AsNoTracking()
                .Include(c => c.RequestedBy).Include(c => c.ApprovedBy)
                .Where(c => c.ProjectId == projectId && (status == null || c.Status == status))
                .OrderByDescending(c => c.CreatedAt).ToListAsync();

            ViewBag.Status = status;
            ViewBag.ApprovedCost = changes.Where(c => c.Status is ChangeRequestStatus.Approved or ChangeRequestStatus.Implemented)
                .Sum(c => c.CostImpact);
            ViewBag.ApprovedDays = changes.Where(c => c.Status is ChangeRequestStatus.Approved or ChangeRequestStatus.Implemented)
                .Sum(c => c.ScheduleImpactDays);
            ViewBag.PendingCount = changes.Count(c => c.Status is ChangeRequestStatus.Submitted or ChangeRequestStatus.UnderReview);
            await PopulateUsersAsync();

            return View(changes);
        }

        [HttpPost]
        public async Task<IActionResult> SaveChange(ProjectChangeRequest input)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Reason))
            {
                TempData["Error"] = "A change request needs a title and a reason.";
                return RedirectToAction(nameof(Changes), new { projectId = input.ProjectId });
            }

            if (input.Id == 0)
            {
                input.RequestedById = Uid;
                input.CreatedAt = DateTime.Now;
                input.Status = ChangeRequestStatus.Draft;
                _db.ProjectChangeRequests.Add(input);
                await _db.SaveChangesAsync();
                _activity.Log(input.ProjectId, nameof(ProjectChangeRequest), input.Id, "Created", input.Title);
                await _db.SaveChangesAsync();
            }
            else
            {
                var change = await _db.ProjectChangeRequests.FirstOrDefaultAsync(c => c.Id == input.Id && c.ProjectId == input.ProjectId);
                if (change == null) return NotFound();

                // Once a change has been decided its numbers are part of the record.
                if (change.Status is ChangeRequestStatus.Approved or ChangeRequestStatus.Implemented or ChangeRequestStatus.Rejected)
                {
                    TempData["Error"] = "A decided change request can no longer be edited.";
                    return RedirectToAction(nameof(Changes), new { projectId = input.ProjectId });
                }

                change.Title = input.Title;
                change.Reason = input.Reason;
                change.ImpactAssessment = input.ImpactAssessment;
                change.ImpactLevel = input.ImpactLevel;
                change.CostImpact = input.CostImpact;
                change.ScheduleImpactDays = input.ScheduleImpactDays;
                change.ImplementationPlan = input.ImplementationPlan;
                _activity.Log(input.ProjectId, nameof(ProjectChangeRequest), change.Id, "Updated", change.Title);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Change request saved.";
            return RedirectToAction(nameof(Changes), new { projectId = input.ProjectId });
        }

        /// <summary>Route a change request into the approval chain.</summary>
        [HttpPost]
        public async Task<IActionResult> SubmitChange(int projectId, int changeId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            var change = await _db.ProjectChangeRequests.FirstOrDefaultAsync(c => c.Id == changeId && c.ProjectId == projectId);
            if (change == null) return NotFound();

            var steps = await _approvals.RequestAsync(ApprovalSubject.ChangeRequest, changeId,
                $"{change.Reference} — {change.Title}", projectId, Uid, Math.Abs(change.CostImpact));

            if (steps == 0)
            {
                TempData["Error"] = "No approver could be found for this change request.";
                return RedirectToAction(nameof(Changes), new { projectId });
            }

            change.Status = ChangeRequestStatus.Submitted;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Change request submitted — {steps} approval step(s) raised.";
            return RedirectToAction(nameof(Changes), new { projectId });
        }

        /// <summary>
        /// Apply an approved change to the project: the cost impact joins the budget as an approved
        /// change order, and the schedule impact moves the end date.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ImplementChange(int projectId, int changeId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit) return AccessDenied();

            var change = await _db.ProjectChangeRequests.FirstOrDefaultAsync(c => c.Id == changeId && c.ProjectId == projectId);
            if (change == null) return NotFound();

            if (change.Status != ChangeRequestStatus.Approved)
            {
                TempData["Error"] = "Only an approved change request can be implemented.";
                return RedirectToAction(nameof(Changes), new { projectId });
            }
            if (change.AppliedToBaseline)
            {
                TempData["Error"] = "This change has already been applied to the project baseline.";
                return RedirectToAction(nameof(Changes), new { projectId });
            }

            var project = ctx.Value.Project;
            project.ApprovedChangeValue += change.CostImpact;
            if (change.ScheduleImpactDays != 0 && project.EndDate.HasValue)
                project.EndDate = project.EndDate.Value.AddDays(change.ScheduleImpactDays);

            change.Status = ChangeRequestStatus.Implemented;
            change.ImplementedAt = DateTime.Now;
            change.AppliedToBaseline = true;

            _activity.Log(projectId, nameof(ProjectChangeRequest), changeId, "Implemented",
                $"Budget {change.CostImpact:+#,##0.00;-#,##0.00;0}, schedule {change.ScheduleImpactDays:+0;-0;0} day(s)");
            _activity.NotifyMany(await _activity.ProjectAudienceAsync(projectId), PmNotificationType.StatusChanged,
                $"Change implemented: {change.Title}",
                $"The project baseline has moved by {change.ScheduleImpactDays} day(s) and {change.CostImpact:N2}.",
                Url.Action(nameof(Changes), new { projectId }), projectId);

            await _db.SaveChangesAsync();
            await _metrics.RefreshProjectAsync(projectId);

            TempData["Success"] = "Change applied to the project baseline.";
            return RedirectToAction(nameof(Changes), new { projectId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteChange(int projectId, int changeId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit) return AccessDenied();

            var change = await _db.ProjectChangeRequests.FirstOrDefaultAsync(c => c.Id == changeId && c.ProjectId == projectId);
            if (change == null) return NotFound();

            if (change.Status == ChangeRequestStatus.Implemented)
            {
                TempData["Error"] = "An implemented change is part of the project baseline and cannot be deleted.";
                return RedirectToAction(nameof(Changes), new { projectId });
            }

            _db.ProjectChangeRequests.Remove(change);
            _activity.Log(projectId, nameof(ProjectChangeRequest), changeId, "Deleted", change.Title);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Change request removed.";
            return RedirectToAction(nameof(Changes), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Quality management
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Quality(int projectId, QualityResult? result)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            var checks = await _db.QualityChecks.AsNoTracking()
                .Include(q => q.Inspector).Include(q => q.Deliverable).Include(q => q.Task)
                .Where(q => q.ProjectId == projectId && (result == null || q.Result == result))
                .OrderByDescending(q => q.ScheduledDate ?? q.CreatedAt).ToListAsync();

            ViewBag.Result = result;
            ViewBag.PassCount = checks.Count(c => c.Result == QualityResult.Pass);
            ViewBag.FailCount = checks.Count(c => c.Result == QualityResult.Fail);
            ViewBag.PendingCount = checks.Count(c => c.Result == QualityResult.Pending);
            ViewBag.Deliverables = await _db.Deliverables.AsNoTracking()
                .Where(d => d.ProjectId == projectId).Select(d => new { d.Id, d.Name }).ToListAsync();
            ViewBag.Tasks = await _db.ProjectTasks.AsNoTracking()
                .Where(t => t.ProjectId == projectId).OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name }).ToListAsync();
            await PopulateUsersAsync();

            return View(checks);
        }

        [HttpPost]
        public async Task<IActionResult> SaveQualityCheck(QualityCheck input)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Title))
            {
                TempData["Error"] = "A quality check needs a title.";
                return RedirectToAction(nameof(Quality), new { projectId = input.ProjectId });
            }

            if (input.Id == 0)
            {
                _db.QualityChecks.Add(input);
                _activity.Log(input.ProjectId, nameof(QualityCheck), null, "Created", input.Title);
            }
            else
            {
                var check = await _db.QualityChecks.FirstOrDefaultAsync(q => q.Id == input.Id && q.ProjectId == input.ProjectId);
                if (check == null) return NotFound();

                _activity.LogChange(input.ProjectId, nameof(QualityCheck), check.Id, "Result", check.Result, input.Result);

                check.Title = input.Title;
                check.Type = input.Type;
                check.AcceptanceCriteria = input.AcceptanceCriteria;
                check.DeliverableId = input.DeliverableId;
                check.TaskId = input.TaskId;
                check.InspectorId = input.InspectorId;
                check.ScheduledDate = input.ScheduledDate;
                check.Result = input.Result;
                check.Findings = input.Findings;
                check.CorrectiveAction = input.CorrectiveAction;
                if (input.Result != QualityResult.Pending) check.PerformedDate ??= DateTime.Today;

                // A failed check is a problem the project needs to see, so it becomes an issue.
                if (input.Result == QualityResult.Fail && !string.IsNullOrWhiteSpace(input.Findings))
                {
                    _db.ProjectIssues.Add(new ProjectIssue
                    {
                        ProjectId = input.ProjectId,
                        Title = $"Failed quality check: {check.Title}",
                        Description = input.Findings,
                        Severity = IssueSeverity.High,
                        Priority = TaskPriority.High,
                        AssignedToId = check.InspectorId,
                        RaisedById = Uid,
                        DueDate = DateTime.Today.AddDays(7)
                    });
                }
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Quality check saved.";
            return RedirectToAction(nameof(Quality), new { projectId = input.ProjectId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteQualityCheck(int projectId, int checkId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit) return AccessDenied();

            var check = await _db.QualityChecks.FirstOrDefaultAsync(q => q.Id == checkId && q.ProjectId == projectId);
            if (check != null)
            {
                _db.QualityChecks.Remove(check);
                _activity.Log(projectId, nameof(QualityCheck), checkId, "Deleted", check.Title);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Quality check removed.";
            return RedirectToAction(nameof(Quality), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════════════

        private async Task<(Project Project, bool CanView, bool CanEdit, bool CanContribute)?> LoadContextAsync(int projectId)
        {
            var (project, team) = await ProjectsController.LoadAsync(_db, projectId);
            if (project == null) return null;

            var teamIds = team.Select(t => t.UserId).ToList();
            var canView = PmAccess.CanView(project, Uid, Role, teamIds);
            var canEdit = PmAccess.CanEdit(project, Uid, Role);
            var canContribute = PmAccess.CanContribute(project, Uid, Role, teamIds);

            ViewBag.Project = project;
            ViewBag.Team = team;
            ViewBag.CanEdit = canEdit;
            ViewBag.CanContribute = canContribute;
            return (project, canView, canEdit, canContribute);
        }

        private async Task PopulateUsersAsync() =>
            ViewBag.Users = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive).OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();
    }
}
