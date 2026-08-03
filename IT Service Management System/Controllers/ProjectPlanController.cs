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
    /// Project planning: phases, the Work Breakdown Structure, milestones and deliverables — plus
    /// the project calendar that draws all of them onto one month view.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager", "TeamLead",
                   "DepartmentManager", "Finance", "Procurement", "Auditor", "Employee", "HR")]
    public class ProjectPlanController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectSchedulingService _scheduling;
        private readonly ProjectActivityService _activity;
        private readonly ProjectMetricsService _metrics;
        private readonly ProjectIntelligenceService _intelligence;

        public ProjectPlanController(ApplicationDbContext db, ProjectSchedulingService scheduling,
            ProjectActivityService activity, ProjectMetricsService metrics, ProjectIntelligenceService intelligence)
        {
            _db = db; _scheduling = scheduling; _activity = activity;
            _metrics = metrics; _intelligence = intelligence;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        // ════════════════════════════════════════════════════════════════════════
        //  Plan overview
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Phases, the WBS tree and the plan's headline figures on one page.</summary>
        public async Task<IActionResult> Index(int projectId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            var phases = await _db.ProjectPhases.AsNoTracking()
                .Where(p => p.ProjectId == projectId).OrderBy(p => p.Sequence).ToListAsync();

            var wbs = await _db.WbsItems.AsNoTracking()
                .Include(w => w.Owner)
                .Where(w => w.ProjectId == projectId)
                .OrderBy(w => w.WbsCode).ToListAsync();

            var taskCounts = await _db.ProjectTasks.AsNoTracking()
                .Where(t => t.ProjectId == projectId && t.PhaseId != null)
                .GroupBy(t => t.PhaseId!.Value)
                .Select(g => new { g.Key, Total = g.Count(), Done = g.Count(t => t.Status == ProjectTaskStatus.Completed) })
                .ToListAsync();

            ViewBag.Phases = phases;
            ViewBag.WbsTree = BuildTree(wbs);
            ViewBag.PhaseTaskCounts = taskCounts.ToDictionary(x => x.Key, x => (x.Total, x.Done));
            ViewBag.TotalEstimatedHours = wbs.Where(w => w.ParentId == null).Sum(w => w.EstimatedHours);
            ViewBag.Suggestions = wbs.Count == 0
                ? await _intelligence.SuggestWbsAsync(ctx.Value.Project.Category, ctx.Value.Project.Type)
                : new List<WbsSuggestion>();

            await PopulateListsAsync(projectId);
            return View(ctx.Value.Project);
        }

        /// <summary>Flatten the WBS into render order, carrying each node's depth for indentation.</summary>
        private static List<(WbsItem Item, int Depth)> BuildTree(List<WbsItem> items)
        {
            var byParent = items.GroupBy(w => w.ParentId)
                .ToDictionary(g => g.Key ?? 0, g => g.OrderBy(x => x.Sequence).ThenBy(x => x.Id).ToList());
            var result = new List<(WbsItem, int)>();

            void Walk(int parentKey, int depth)
            {
                if (!byParent.TryGetValue(parentKey, out var children)) return;
                foreach (var child in children)
                {
                    result.Add((child, depth));
                    Walk(child.Id, depth + 1);
                }
            }

            Walk(0, 0);
            return result;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Phases
        // ════════════════════════════════════════════════════════════════════════

        [HttpPost]
        public async Task<IActionResult> SavePhase(ProjectPhase input)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "A phase needs a name.";
                return RedirectToAction(nameof(Index), new { projectId = input.ProjectId });
            }

            if (input.Id == 0)
            {
                input.Sequence = await _db.ProjectPhases.CountAsync(p => p.ProjectId == input.ProjectId) + 1;
                _db.ProjectPhases.Add(input);
                _activity.Log(input.ProjectId, nameof(ProjectPhase), null, "Created", input.Name);
            }
            else
            {
                var phase = await _db.ProjectPhases.FirstOrDefaultAsync(p => p.Id == input.Id && p.ProjectId == input.ProjectId);
                if (phase == null) return NotFound();

                phase.Name = input.Name;
                phase.Description = input.Description;
                phase.StartDate = input.StartDate;
                phase.EndDate = input.EndDate;
                phase.Status = input.Status;
                phase.ProgressPercent = Math.Clamp(input.ProgressPercent, 0, 100);
                _activity.Log(input.ProjectId, nameof(ProjectPhase), phase.Id, "Updated", phase.Name);
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Phase saved.";
            return RedirectToAction(nameof(Index), new { projectId = input.ProjectId });
        }

        [HttpPost]
        public async Task<IActionResult> DeletePhase(int projectId, int phaseId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit) return AccessDenied();

            var phase = await _db.ProjectPhases.FirstOrDefaultAsync(p => p.Id == phaseId && p.ProjectId == projectId);
            if (phase != null)
            {
                // Tasks and WBS nodes survive — they simply lose their phase.
                _db.ProjectPhases.Remove(phase);
                _activity.Log(projectId, nameof(ProjectPhase), phaseId, "Deleted", phase.Name);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Phase removed. Tasks that were in it are now unphased.";
            return RedirectToAction(nameof(Index), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Work Breakdown Structure
        // ════════════════════════════════════════════════════════════════════════

        [HttpPost]
        public async Task<IActionResult> SaveWbsItem(WbsItem input)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "A work package needs a name.";
                return RedirectToAction(nameof(Index), new { projectId = input.ProjectId });
            }
            if (input.ParentId == input.Id && input.Id != 0)
            {
                TempData["Error"] = "A work package cannot be its own parent.";
                return RedirectToAction(nameof(Index), new { projectId = input.ProjectId });
            }

            if (input.Id == 0)
            {
                input.Sequence = await _db.WbsItems.CountAsync(w => w.ProjectId == input.ProjectId && w.ParentId == input.ParentId) + 1;
                _db.WbsItems.Add(input);
                _activity.Log(input.ProjectId, nameof(WbsItem), null, "Created", input.Name);
            }
            else
            {
                var item = await _db.WbsItems.FirstOrDefaultAsync(w => w.Id == input.Id && w.ProjectId == input.ProjectId);
                if (item == null) return NotFound();

                // Re-parenting under a descendant would detach the branch from the tree.
                if (input.ParentId.HasValue && await IsDescendantAsync(input.ParentId.Value, item.Id))
                {
                    TempData["Error"] = "That would move the work package underneath one of its own children.";
                    return RedirectToAction(nameof(Index), new { projectId = input.ProjectId });
                }

                item.Name = input.Name;
                item.Description = input.Description;
                item.ParentId = input.ParentId;
                item.PhaseId = input.PhaseId;
                item.OwnerId = input.OwnerId;
                item.StartDate = input.StartDate;
                item.EndDate = input.EndDate;
                item.EstimatedHours = input.EstimatedHours;
                item.EstimatedCost = input.EstimatedCost;
                item.ProgressPercent = Math.Clamp(input.ProgressPercent, 0, 100);
                _activity.Log(input.ProjectId, nameof(WbsItem), item.Id, "Updated", item.Name);
            }

            await _db.SaveChangesAsync();
            await _scheduling.RenumberWbsAsync(input.ProjectId);
            await _scheduling.RollUpWbsAsync(input.ProjectId);

            TempData["Success"] = "Work package saved.";
            return RedirectToAction(nameof(Index), new { projectId = input.ProjectId });
        }

        /// <summary>True when <paramref name="candidateId"/> sits below <paramref name="ancestorId"/> in the tree.</summary>
        private async Task<bool> IsDescendantAsync(int candidateId, int ancestorId)
        {
            var current = await _db.WbsItems.AsNoTracking()
                .Where(w => w.Id == candidateId).Select(w => w.ParentId).FirstOrDefaultAsync();
            var guard = 0;
            while (current.HasValue && guard++ < 50)
            {
                if (current.Value == ancestorId) return true;
                current = await _db.WbsItems.AsNoTracking()
                    .Where(w => w.Id == current.Value).Select(w => w.ParentId).FirstOrDefaultAsync();
            }
            return false;
        }

        [HttpPost]
        public async Task<IActionResult> DeleteWbsItem(int projectId, int itemId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit) return AccessDenied();

            var children = await _db.WbsItems.CountAsync(w => w.ParentId == itemId);
            if (children > 0)
            {
                TempData["Error"] = $"This work package has {children} child package(s). Remove or re-parent them first.";
                return RedirectToAction(nameof(Index), new { projectId });
            }

            var item = await _db.WbsItems.FirstOrDefaultAsync(w => w.Id == itemId && w.ProjectId == projectId);
            if (item != null)
            {
                _db.WbsItems.Remove(item);
                _activity.Log(projectId, nameof(WbsItem), itemId, "Deleted", item.Name);
                await _db.SaveChangesAsync();
                await _scheduling.RenumberWbsAsync(projectId);
            }

            TempData["Success"] = "Work package removed.";
            return RedirectToAction(nameof(Index), new { projectId });
        }

        /// <summary>Create the whole suggested breakdown in one go.</summary>
        [HttpPost]
        public async Task<IActionResult> ApplySuggestedWbs(int projectId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit) return AccessDenied();

            var project = ctx.Value.Project;
            var suggestions = await _intelligence.SuggestWbsAsync(project.Category, project.Type);
            if (suggestions.Count == 0)
            {
                TempData["Error"] = "There is no comparable history or template to base a breakdown on yet.";
                return RedirectToAction(nameof(Index), new { projectId });
            }

            var start = project.StartDate ?? DateTime.Today;
            var cursor = start;
            var sequence = 1;

            foreach (var suggestion in suggestions.Where(s => s.ItemType != "Milestone"))
            {
                _db.WbsItems.Add(new WbsItem
                {
                    ProjectId = projectId,
                    Name = suggestion.Name,
                    Sequence = sequence++,
                    StartDate = cursor,
                    EndDate = cursor.AddDays(Math.Max(1, suggestion.DurationDays)),
                    EstimatedHours = suggestion.EstimatedHours
                });
                cursor = cursor.AddDays(Math.Max(1, suggestion.DurationDays));
            }

            _activity.Log(projectId, nameof(WbsItem), null, "Created", $"{sequence - 1} work packages created from a suggested breakdown");
            await _db.SaveChangesAsync();
            await _scheduling.RenumberWbsAsync(projectId);

            TempData["Success"] = $"{sequence - 1} work package(s) created. Adjust the dates and estimates to fit.";
            return RedirectToAction(nameof(Index), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Milestones
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Milestones(int projectId, MilestoneStatus? status)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            IQueryable<Milestone> query = _db.Milestones.AsNoTracking()
                .Include(m => m.Owner).Include(m => m.Phase)
                .Where(m => m.ProjectId == projectId);
            if (status.HasValue) query = query.Where(m => m.Status == status.Value);

            var milestones = await query.OrderBy(m => m.DueDate).ToListAsync();

            ViewBag.Status = status;
            ViewBag.Deliverables = await _db.Deliverables.AsNoTracking()
                .Where(d => d.ProjectId == projectId && d.MilestoneId != null)
                .GroupBy(d => d.MilestoneId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            await PopulateListsAsync(projectId);

            return View(milestones);
        }

        [HttpPost]
        public async Task<IActionResult> SaveMilestone(Milestone input)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "A milestone needs a name.";
                return RedirectToAction(nameof(Milestones), new { projectId = input.ProjectId });
            }

            if (input.Id == 0)
            {
                input.BaselineDate = input.DueDate;
                _db.Milestones.Add(input);
                _activity.Log(input.ProjectId, nameof(Milestone), null, "Created", input.Name);
                await _db.SaveChangesAsync();

                if (input.OwnerId is int owner)
                {
                    _activity.Notify(owner, PmNotificationType.StatusChanged, $"You own the milestone “{input.Name}”",
                        $"Due {input.DueDate:d MMM yyyy}", Url.Action(nameof(Milestones), new { projectId = input.ProjectId }), input.ProjectId);
                    await _db.SaveChangesAsync();
                }
            }
            else
            {
                var milestone = await _db.Milestones.FirstOrDefaultAsync(m => m.Id == input.Id && m.ProjectId == input.ProjectId);
                if (milestone == null) return NotFound();

                _activity.LogChange(input.ProjectId, nameof(Milestone), milestone.Id, "Due date",
                    milestone.DueDate.ToString("d"), input.DueDate.ToString("d"));

                milestone.Name = input.Name;
                milestone.Description = input.Description;
                milestone.DueDate = input.DueDate;
                milestone.PhaseId = input.PhaseId;
                milestone.OwnerId = input.OwnerId;
                milestone.RequiresClientApproval = input.RequiresClientApproval;
                milestone.Notes = input.Notes;
                milestone.BaselineDate ??= input.DueDate;
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Milestone saved.";
            return RedirectToAction(nameof(Milestones), new { projectId = input.ProjectId });
        }

        [HttpPost]
        public async Task<IActionResult> SetMilestoneStatus(int projectId, int milestoneId, MilestoneStatus status)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            var milestone = await _db.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.ProjectId == projectId);
            if (milestone == null) return NotFound();

            _activity.LogChange(projectId, nameof(Milestone), milestoneId, "Status", milestone.Status, status);
            milestone.Status = status;
            milestone.AchievedDate = status == MilestoneStatus.Achieved ? DateTime.Today : null;

            if (status == MilestoneStatus.Achieved)
                _activity.NotifyMany(await _activity.ProjectAudienceAsync(projectId), PmNotificationType.MilestoneAchieved,
                    $"Milestone achieved: {milestone.Name}",
                    milestone.SlippageDays > 0 ? $"{milestone.SlippageDays} day(s) later than baselined." : "On or ahead of baseline.",
                    Url.Action(nameof(Milestones), new { projectId }), projectId);

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Milestone marked {status}.";
            return RedirectToAction(nameof(Milestones), new { projectId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMilestone(int projectId, int milestoneId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit) return AccessDenied();

            var milestone = await _db.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.ProjectId == projectId);
            if (milestone != null)
            {
                _db.Milestones.Remove(milestone);
                _activity.Log(projectId, nameof(Milestone), milestoneId, "Deleted", milestone.Name);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Milestone removed.";
            return RedirectToAction(nameof(Milestones), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Deliverables
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Deliverables(int projectId, DeliverableStatus? status)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            IQueryable<Deliverable> query = _db.Deliverables.AsNoTracking()
                .Include(d => d.Owner).Include(d => d.Milestone).Include(d => d.AcceptedBy)
                .Where(d => d.ProjectId == projectId);
            if (status.HasValue) query = query.Where(d => d.Status == status.Value);

            ViewBag.Status = status;
            await PopulateListsAsync(projectId);

            return View(await query.OrderBy(d => d.DueDate ?? DateTime.MaxValue).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> SaveDeliverable(Deliverable input)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "A deliverable needs a name.";
                return RedirectToAction(nameof(Deliverables), new { projectId = input.ProjectId });
            }

            if (input.Id == 0)
            {
                _db.Deliverables.Add(input);
                _activity.Log(input.ProjectId, nameof(Deliverable), null, "Created", input.Name);
            }
            else
            {
                var deliverable = await _db.Deliverables.FirstOrDefaultAsync(d => d.Id == input.Id && d.ProjectId == input.ProjectId);
                if (deliverable == null) return NotFound();

                deliverable.Name = input.Name;
                deliverable.Description = input.Description;
                deliverable.AcceptanceCriteria = input.AcceptanceCriteria;
                deliverable.MilestoneId = input.MilestoneId;
                deliverable.PhaseId = input.PhaseId;
                deliverable.OwnerId = input.OwnerId;
                deliverable.DueDate = input.DueDate;
                deliverable.IsClosureItem = input.IsClosureItem;
                _activity.Log(input.ProjectId, nameof(Deliverable), deliverable.Id, "Updated", deliverable.Name);
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Deliverable saved.";
            return RedirectToAction(nameof(Deliverables), new { projectId = input.ProjectId });
        }

        /// <summary>Move a deliverable through submit → review → accept/reject.</summary>
        [HttpPost]
        public async Task<IActionResult> SetDeliverableStatus(int projectId, int deliverableId, DeliverableStatus status, string? notes)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            // Accepting or rejecting is a sign-off decision, not a contributor action.
            if (status is DeliverableStatus.Accepted or DeliverableStatus.Rejected && !ctx.Value.CanEdit)
            {
                TempData["Error"] = "Only the project manager or an administrator can accept or reject a deliverable.";
                return RedirectToAction(nameof(Deliverables), new { projectId });
            }

            var deliverable = await _db.Deliverables.FirstOrDefaultAsync(d => d.Id == deliverableId && d.ProjectId == projectId);
            if (deliverable == null) return NotFound();

            _activity.LogChange(projectId, nameof(Deliverable), deliverableId, "Status", deliverable.Status, status);
            deliverable.Status = status;
            deliverable.AcceptanceNotes = notes;

            if (status == DeliverableStatus.Submitted) deliverable.SubmittedDate = DateTime.Today;
            if (status == DeliverableStatus.Accepted)
            {
                deliverable.AcceptedDate = DateTime.Today;
                deliverable.AcceptedById = Uid;
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Deliverable marked {status}.";
            return RedirectToAction(nameof(Deliverables), new { projectId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDeliverable(int projectId, int deliverableId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit) return AccessDenied();

            var deliverable = await _db.Deliverables.FirstOrDefaultAsync(d => d.Id == deliverableId && d.ProjectId == projectId);
            if (deliverable != null)
            {
                _db.Deliverables.Remove(deliverable);
                _activity.Log(projectId, nameof(Deliverable), deliverableId, "Deleted", deliverable.Name);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Deliverable removed.";
            return RedirectToAction(nameof(Deliverables), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Calendar
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A month view combining task due dates, milestones, meetings and resource leave. Omitting
        /// the project id shows the whole portfolio.
        /// </summary>
        public async Task<IActionResult> Calendar(int? projectId, int? year, int? month)
        {
            var today = DateTime.Today;
            var anchor = new DateTime(year ?? today.Year, month ?? today.Month, 1);
            var from = anchor;
            var to = anchor.AddMonths(1).AddDays(-1);

            if (projectId.HasValue)
            {
                var ctx = await LoadContextAsync(projectId.Value);
                if (ctx == null) return NotFound();
                if (!ctx.Value.CanView) return AccessDenied();
            }

            var events = new List<CalendarEvent>();

            var tasks = await _db.ProjectTasks.AsNoTracking()
                .Include(t => t.Project).Include(t => t.AssignedTo)
                .Where(t => t.DueDate >= from && t.DueDate <= to
                            && t.Status != ProjectTaskStatus.Cancelled
                            && (projectId == null || t.ProjectId == projectId))
                .ToListAsync();
            events.AddRange(tasks.Select(t => new CalendarEvent(
                t.DueDate!.Value, "Task", t.Name, t.Project?.Name,
                t.Status == ProjectTaskStatus.Completed ? "done" : t.IsOverdue ? "late" : "task",
                $"/ProjectTasks/Details/{t.Id}")));

            var milestones = await _db.Milestones.AsNoTracking()
                .Include(m => m.Project)
                .Where(m => m.DueDate >= from && m.DueDate <= to && m.Status != MilestoneStatus.Cancelled
                            && (projectId == null || m.ProjectId == projectId))
                .ToListAsync();
            events.AddRange(milestones.Select(m => new CalendarEvent(
                m.DueDate, "Milestone", m.Name, m.Project?.Name,
                m.Status == MilestoneStatus.Achieved ? "done" : m.IsOverdue ? "late" : "milestone",
                $"/ProjectPlan/Milestones?projectId={m.ProjectId}")));

            var meetings = await _db.ProjectMeetings.AsNoTracking()
                .Include(m => m.Project)
                .Where(m => m.ScheduledAt >= from && m.ScheduledAt <= to.AddDays(1)
                            && m.Status != ProjectMeetingStatus.Cancelled
                            && (projectId == null || m.ProjectId == projectId))
                .ToListAsync();
            events.AddRange(meetings.Select(m => new CalendarEvent(
                m.ScheduledAt.Date, "Meeting", m.Title, m.Project?.Name, "meeting",
                $"/ProjectMeetings/Details/{m.Id}")));

            var leave = await _db.ResourceUnavailabilities.AsNoTracking()
                .Include(u => u.Resource)
                .Where(u => u.FromDate <= to && u.ToDate >= from)
                .ToListAsync();
            events.AddRange(leave.Select(l => new CalendarEvent(
                l.FromDate < from ? from : l.FromDate, "Leave",
                $"{l.Resource?.Name}: {l.Reason}", null, "leave", "/ProjectResources/Capacity")));

            ViewBag.Anchor = anchor;
            ViewBag.Events = events.GroupBy(e => e.Date.Date).ToDictionary(g => g.Key, g => g.ToList());
            ViewBag.ProjectId = projectId;
            ViewBag.Project = projectId.HasValue ? await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId) : null;
            ViewBag.Projects = await _db.Projects.AsNoTracking()
                .Where(p => p.Status != ProjectStatus.Archived)
                .OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync();

            return View();
        }

        /// <summary>One entry on the project calendar.</summary>
        public record CalendarEvent(DateTime Date, string Kind, string Title, string? Project, string Css, string Url);

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

        private async Task PopulateListsAsync(int projectId)
        {
            ViewBag.Users = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive).OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();

            ViewBag.PhaseOptions = await _db.ProjectPhases.AsNoTracking()
                .Where(p => p.ProjectId == projectId).OrderBy(p => p.Sequence)
                .Select(p => new { p.Id, p.Name }).ToListAsync();

            ViewBag.MilestoneOptions = await _db.Milestones.AsNoTracking()
                .Where(m => m.ProjectId == projectId).OrderBy(m => m.DueDate)
                .Select(m => new { m.Id, m.Name }).ToListAsync();

            ViewBag.WbsOptions = await _db.WbsItems.AsNoTracking()
                .Where(w => w.ProjectId == projectId).OrderBy(w => w.WbsCode)
                .Select(w => new { w.Id, Name = w.WbsCode + " " + w.Name }).ToListAsync();
        }
    }
}
