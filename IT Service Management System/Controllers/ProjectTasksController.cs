using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers.Pm;
using IT_Service_Management_System.Models.Pm;
using IT_Service_Management_System.Services.Pm;
using IT_Service_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Task management for a project: the list, the drag-and-drop Kanban board, the interactive
    /// Gantt chart, and everything on an individual task — subtasks, dependencies, checklist,
    /// comments and attachments.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager", "TeamLead",
                   "DepartmentManager", "Finance", "Procurement", "Auditor", "Employee", "HR")]
    public class ProjectTasksController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectMetricsService _metrics;
        private readonly ProjectActivityService _activity;
        private readonly ProjectSchedulingService _scheduling;
        private readonly ProjectIntelligenceService _intelligence;
        private readonly PmFileService _files;

        public ProjectTasksController(ApplicationDbContext db, ProjectMetricsService metrics,
            ProjectActivityService activity, ProjectSchedulingService scheduling,
            ProjectIntelligenceService intelligence, PmFileService files)
        {
            _db = db; _metrics = metrics; _activity = activity;
            _scheduling = scheduling; _intelligence = intelligence; _files = files;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        // ════════════════════════════════════════════════════════════════════════
        //  Task list
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Index(int projectId, ProjectTaskStatus? status,
            TaskPriority? priority, int? assignedTo, int? phaseId, string? q, bool mine = false)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.CanView) return AccessDenied();

            IQueryable<ProjectTask> query = _db.ProjectTasks.AsNoTracking()
                .Include(t => t.AssignedTo).Include(t => t.Phase).Include(t => t.ParentTask)
                .Where(t => t.ProjectId == projectId);

            if (status.HasValue) query = query.Where(t => t.Status == status.Value);
            if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);
            if (assignedTo.HasValue) query = query.Where(t => t.AssignedToId == assignedTo.Value);
            if (phaseId.HasValue) query = query.Where(t => t.PhaseId == phaseId.Value);
            if (mine) query = query.Where(t => t.AssignedToId == Uid);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(t => t.Name.Contains(term) || (t.Description != null && t.Description.Contains(term)));
            }

            ViewBag.Status = status; ViewBag.Priority = priority; ViewBag.AssignedTo = assignedTo;
            ViewBag.PhaseId = phaseId; ViewBag.Q = q; ViewBag.Mine = mine;
            await PopulateListsAsync(projectId);

            return View(await query
                .OrderBy(t => t.Status == ProjectTaskStatus.Completed)
                .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
                .ThenByDescending(t => t.Priority)
                .ToListAsync());
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Kanban board
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>The drag-and-drop board. Cards are grouped by lane and ordered within it.</summary>
        public async Task<IActionResult> Board(int projectId, int? assignedTo, TaskPriority? priority)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.CanView) return AccessDenied();

            IQueryable<ProjectTask> query = _db.ProjectTasks.AsNoTracking()
                .Include(t => t.AssignedTo)
                .Where(t => t.ProjectId == projectId && t.Status != ProjectTaskStatus.Cancelled);

            if (assignedTo.HasValue) query = query.Where(t => t.AssignedToId == assignedTo.Value);
            if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);

            var tasks = await query.OrderBy(t => t.BoardOrder).ThenBy(t => t.Id).ToListAsync();

            ViewBag.Lanes = tasks.GroupBy(t => t.Column)
                .ToDictionary(g => g.Key, g => g.ToList());
            ViewBag.AssignedTo = assignedTo; ViewBag.Priority = priority;
            await PopulateListsAsync(projectId);

            return View(tasks);
        }

        /// <summary>
        /// Persist a card drop. Called by the board over AJAX; returns the recomputed project
        /// progress so the header can update without a reload.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MoveCard(int taskId, KanbanColumn column, int position)
        {
            var task = await _db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return Json(new { ok = false, message = "You do not have permission to move this task." });

            var previousColumn = task.Column;
            task.Column = column;
            task.UpdatedAt = DateTime.Now;

            // Moving into or out of Completed keeps the status honest — the board is the primary
            // surface people work in, so it must not drift from the task record.
            if (column == KanbanColumn.Completed && task.Status != ProjectTaskStatus.Completed)
            {
                task.Status = ProjectTaskStatus.Completed;
                task.PercentComplete = 100;
                task.CompletionDate = DateTime.Today;
            }
            else if (column != KanbanColumn.Completed && task.Status == ProjectTaskStatus.Completed)
            {
                task.Status = column == KanbanColumn.Review ? ProjectTaskStatus.UnderReview : ProjectTaskStatus.InProgress;
                task.CompletionDate = null;
                if (task.PercentComplete == 100) task.PercentComplete = 90;
            }
            else if (column == KanbanColumn.InProgress && task.Status == ProjectTaskStatus.NotStarted)
            {
                task.Status = ProjectTaskStatus.InProgress;
            }
            else if (column == KanbanColumn.Review && task.Status != ProjectTaskStatus.UnderReview)
            {
                task.Status = ProjectTaskStatus.UnderReview;
            }

            // Re-number the destination lane so the manual order survives a reload.
            var lane = await _db.ProjectTasks
                .Where(t => t.ProjectId == task.ProjectId && t.Column == column && t.Id != taskId)
                .OrderBy(t => t.BoardOrder).ThenBy(t => t.Id)
                .ToListAsync();

            var index = Math.Clamp(position, 0, lane.Count);
            lane.Insert(index, task);
            for (var i = 0; i < lane.Count; i++) lane[i].BoardOrder = i;

            if (previousColumn != column)
                _activity.LogChange(task.ProjectId, nameof(ProjectTask), task.Id, "Board lane", previousColumn, column);

            await _db.SaveChangesAsync();
            await _metrics.RefreshProjectAsync(task.ProjectId);

            var project = await _db.Projects.AsNoTracking()
                .Where(p => p.Id == task.ProjectId)
                .Select(p => new { p.ProgressPercent, p.Health })
                .FirstAsync();

            return Json(new
            {
                ok = true,
                status = task.Status.ToString(),
                progress = project.ProgressPercent,
                health = project.Health.ToString()
            });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Gantt chart
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The timeline view. Bars are positioned as percentages across the project window, so the
        /// chart is pure CSS and prints correctly.
        /// </summary>
        public async Task<IActionResult> Gantt(int projectId, bool showBaseline = true)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.CanView) return AccessDenied();

            var tasks = await _db.ProjectTasks.AsNoTracking()
                .Include(t => t.AssignedTo).Include(t => t.Phase)
                .Where(t => t.ProjectId == projectId && t.Status != ProjectTaskStatus.Cancelled)
                .OrderBy(t => t.Phase!.Sequence).ThenBy(t => t.StartDate ?? DateTime.MaxValue).ThenBy(t => t.Id)
                .ToListAsync();

            var milestones = await _db.Milestones.AsNoTracking()
                .Where(m => m.ProjectId == projectId && m.Status != MilestoneStatus.Cancelled)
                .OrderBy(m => m.DueDate).ToListAsync();

            var dependencies = await _db.TaskDependencies.AsNoTracking()
                .Where(d => d.Task!.ProjectId == projectId)
                .Select(d => new { d.TaskId, d.PredecessorTaskId, d.Type, d.LagDays })
                .ToListAsync();

            // The chart window spans everything on the plan, padded to whole months.
            var dates = tasks.SelectMany(t => new[] { t.StartDate, t.DueDate, t.BaselineStartDate, t.BaselineDueDate })
                .Concat(milestones.Select(m => (DateTime?)m.DueDate))
                .Concat(new[] { ctx.Project.StartDate, ctx.Project.EndDate })
                .Where(d => d.HasValue).Select(d => d!.Value).ToList();

            var from = dates.Count > 0 ? dates.Min() : DateTime.Today;
            var to = dates.Count > 0 ? dates.Max() : DateTime.Today.AddDays(60);
            from = new DateTime(from.Year, from.Month, 1);
            to = new DateTime(to.Year, to.Month, 1).AddMonths(1).AddDays(-1);
            if ((to - from).TotalDays < 30) to = from.AddDays(60);

            ViewBag.From = from;
            ViewBag.To = to;
            ViewBag.Months = MonthsBetween(from, to);
            ViewBag.Milestones = milestones;
            ViewBag.Dependencies = dependencies.ToLookup(d => d.TaskId);
            ViewBag.ShowBaseline = showBaseline;
            ViewBag.CriticalCount = tasks.Count(t => t.IsOnCriticalPath);
            await PopulateListsAsync(projectId);

            return View(tasks);
        }

        private static List<(string Label, DateTime Start)> MonthsBetween(DateTime from, DateTime to)
        {
            var months = new List<(string, DateTime)>();
            var cursor = new DateTime(from.Year, from.Month, 1);
            while (cursor <= to)
            {
                months.Add((cursor.ToString("MMM yy"), cursor));
                cursor = cursor.AddMonths(1);
            }
            return months;
        }

        /// <summary>Recompute float and the critical path across the whole plan.</summary>
        [HttpPost]
        public async Task<IActionResult> RecalculateCriticalPath(int projectId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.CanContribute) return AccessDenied();

            var count = await _scheduling.RecalculateCriticalPathAsync(projectId);
            TempData["Success"] = count > 0
                ? $"Critical path recalculated — {count} task(s) have no float."
                : "Critical path could not be computed. Check the plan for a circular dependency.";

            return RedirectToAction(nameof(Gantt), new { projectId });
        }

        /// <summary>Drag a Gantt bar — moves the task's dates, keeping its duration.</summary>
        [HttpPost]
        public async Task<IActionResult> Reschedule(int taskId, DateTime start, DateTime due)
        {
            var task = await _db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return Json(new { ok = false, message = "You do not have permission to reschedule this task." });

            if (due < start) return Json(new { ok = false, message = "The due date cannot be before the start date." });

            _activity.LogChange(task.ProjectId, nameof(ProjectTask), task.Id, "Start date", task.StartDate?.ToString("d"), start.ToString("d"));
            _activity.LogChange(task.ProjectId, nameof(ProjectTask), task.Id, "Due date", task.DueDate?.ToString("d"), due.ToString("d"));

            task.StartDate = start.Date;
            task.DueDate = due.Date;
            task.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await _scheduling.RecalculateCriticalPathAsync(task.ProjectId);
            return Json(new { ok = true });
        }

        /// <summary>Freeze the current plan as the baseline the Gantt compares against.</summary>
        [HttpPost]
        public async Task<IActionResult> SetBaseline(int projectId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.CanEdit) return AccessDenied();

            await _scheduling.SetBaselineAsync(projectId);
            _activity.Log(projectId, nameof(Project), projectId, "BaselineSet", "Plan baselined");
            await _db.SaveChangesAsync();

            TempData["Success"] = "Baseline captured. Slippage is now measured against today's plan.";
            return RedirectToAction(nameof(Gantt), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Task CRUD
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Create(int projectId, int? parentTaskId, int? phaseId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.CanContribute) return AccessDenied();

            await PopulateListsAsync(projectId);
            return View("Form", new ProjectTask
            {
                ProjectId = projectId,
                ParentTaskId = parentTaskId,
                PhaseId = phaseId,
                StartDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(7)
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProjectTask input)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.CanContribute) return AccessDenied();

            if (input.DueDate.HasValue && input.StartDate.HasValue && input.DueDate < input.StartDate)
                ModelState.AddModelError(nameof(input.DueDate), "The due date cannot be before the start date.");

            if (!ModelState.IsValid) { await PopulateListsAsync(input.ProjectId); return View("Form", input); }

            input.CreatedById = Uid;
            input.CreatedAt = DateTime.Now;
            input.BaselineStartDate = input.StartDate;
            input.BaselineDueDate = input.DueDate;
            input.Column = input.Status switch
            {
                ProjectTaskStatus.InProgress => KanbanColumn.InProgress,
                ProjectTaskStatus.UnderReview => KanbanColumn.Review,
                ProjectTaskStatus.Completed => KanbanColumn.Completed,
                ProjectTaskStatus.Assigned => KanbanColumn.Ready,
                _ => KanbanColumn.Backlog
            };
            input.BoardOrder = await _db.ProjectTasks.CountAsync(t => t.ProjectId == input.ProjectId && t.Column == input.Column);

            _db.ProjectTasks.Add(input);
            await _db.SaveChangesAsync();

            _activity.Log(input.ProjectId, nameof(ProjectTask), input.Id, "Created", input.Name);
            if (input.AssignedToId is int assignee)
                _activity.Notify(assignee, PmNotificationType.TaskAssigned, $"Task assigned: {input.Name}",
                    $"Due {input.DueDate:d MMM yyyy}", Url.Action(nameof(Details), "ProjectTasks", new { id = input.Id }), input.ProjectId);
            await _db.SaveChangesAsync();

            await _metrics.RefreshProjectAsync(input.ProjectId);

            TempData["Success"] = $"Task {input.Reference} created.";
            return RedirectToAction(nameof(Details), new { id = input.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var task = await _db.ProjectTasks.FindAsync(id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return AccessDenied();

            await PopulateListsAsync(task.ProjectId, id);
            return View("Form", task);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ProjectTask input)
        {
            var task = await _db.ProjectTasks.FindAsync(input.Id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return AccessDenied();

            if (input.DueDate.HasValue && input.StartDate.HasValue && input.DueDate < input.StartDate)
                ModelState.AddModelError(nameof(input.DueDate), "The due date cannot be before the start date.");
            if (input.ParentTaskId == input.Id)
                ModelState.AddModelError(nameof(input.ParentTaskId), "A task cannot be its own parent.");

            if (!ModelState.IsValid) { await PopulateListsAsync(task.ProjectId, input.Id); return View("Form", input); }

            var previousAssignee = task.AssignedToId;

            _activity.LogChange(task.ProjectId, nameof(ProjectTask), task.Id, "Name", task.Name, input.Name);
            _activity.LogChange(task.ProjectId, nameof(ProjectTask), task.Id, "Status", task.Status, input.Status);
            _activity.LogChange(task.ProjectId, nameof(ProjectTask), task.Id, "Assignee", task.AssignedToId, input.AssignedToId);
            _activity.LogChange(task.ProjectId, nameof(ProjectTask), task.Id, "Due date", task.DueDate?.ToString("d"), input.DueDate?.ToString("d"));

            task.Name = input.Name;
            task.Description = input.Description;
            task.ParentTaskId = input.ParentTaskId;
            task.PhaseId = input.PhaseId;
            task.WbsItemId = input.WbsItemId;
            task.MilestoneId = input.MilestoneId;
            task.AssignedToId = input.AssignedToId;
            task.ReviewerId = input.ReviewerId;
            task.Priority = input.Priority;
            task.EstimatedHours = input.EstimatedHours;
            task.StartDate = input.StartDate;
            task.DueDate = input.DueDate;
            task.PercentComplete = Math.Clamp(input.PercentComplete, 0, 100);
            task.IsBillable = input.IsBillable;
            task.Tags = input.Tags;
            task.BlockedReason = input.BlockedReason;
            ApplyStatus(task, input.Status);
            task.UpdatedAt = DateTime.Now;

            if (input.AssignedToId is int newAssignee && newAssignee != previousAssignee)
                _activity.Notify(newAssignee, PmNotificationType.TaskAssigned, $"Task assigned: {task.Name}",
                    $"Due {task.DueDate:d MMM yyyy}", Url.Action(nameof(Details), "ProjectTasks", new { id = task.Id }), task.ProjectId);

            await _db.SaveChangesAsync();
            await _metrics.RefreshProjectAsync(task.ProjectId);

            TempData["Success"] = $"Task {task.Reference} updated.";
            return RedirectToAction(nameof(Details), new { id = task.Id });
        }

        /// <summary>Keeps status, board lane, progress and completion date consistent with one another.</summary>
        private static void ApplyStatus(ProjectTask task, ProjectTaskStatus status)
        {
            task.Status = status;
            task.Column = status switch
            {
                ProjectTaskStatus.NotStarted => KanbanColumn.Backlog,
                ProjectTaskStatus.Assigned => KanbanColumn.Ready,
                ProjectTaskStatus.InProgress or ProjectTaskStatus.Waiting or ProjectTaskStatus.Blocked => KanbanColumn.InProgress,
                ProjectTaskStatus.UnderReview => KanbanColumn.Review,
                ProjectTaskStatus.Completed => KanbanColumn.Completed,
                _ => task.Column
            };

            if (status == ProjectTaskStatus.Completed)
            {
                task.PercentComplete = 100;
                task.CompletionDate ??= DateTime.Today;
            }
            else
            {
                task.CompletionDate = null;
                if (task.PercentComplete >= 100) task.PercentComplete = 90;
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var task = await _db.ProjectTasks
                .Include(t => t.AssignedTo).Include(t => t.Reviewer).Include(t => t.CreatedBy)
                .Include(t => t.Phase).Include(t => t.Milestone).Include(t => t.WbsItem)
                .Include(t => t.ParentTask)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanView) return AccessDenied();

            ViewBag.Subtasks = await _db.ProjectTasks.AsNoTracking()
                .Include(t => t.AssignedTo).Where(t => t.ParentTaskId == id).OrderBy(t => t.Id).ToListAsync();
            ViewBag.Checklist = await _db.TaskChecklistItems.AsNoTracking()
                .Where(c => c.TaskId == id).OrderBy(c => c.Sequence).ThenBy(c => c.Id).ToListAsync();
            ViewBag.Comments = await _db.TaskComments.AsNoTracking()
                .Include(c => c.Author).Where(c => c.TaskId == id).OrderBy(c => c.CreatedAt).ToListAsync();
            ViewBag.Attachments = await _db.TaskAttachments.AsNoTracking()
                .Include(a => a.UploadedBy).Where(a => a.TaskId == id).OrderByDescending(a => a.UploadedAt).ToListAsync();
            ViewBag.Predecessors = await _db.TaskDependencies.AsNoTracking()
                .Include(d => d.PredecessorTask).Where(d => d.TaskId == id).ToListAsync();
            ViewBag.Successors = await _db.TaskDependencies.AsNoTracking()
                .Include(d => d.Task).Where(d => d.PredecessorTaskId == id).ToListAsync();
            ViewBag.TimeEntries = await _db.TimeEntries.AsNoTracking()
                .Include(t => t.User).Where(t => t.TaskId == id).OrderByDescending(t => t.WorkDate).Take(10).ToListAsync();
            ViewBag.LinkableTasks = await _db.ProjectTasks.AsNoTracking()
                .Where(t => t.ProjectId == task.ProjectId && t.Id != id)
                .OrderBy(t => t.Name).Select(t => new { t.Id, t.Name }).ToListAsync();

            await PopulateListsAsync(task.ProjectId, id);
            return View(task);
        }

        /// <summary>Quick status change from the list or the detail header.</summary>
        [HttpPost]
        public async Task<IActionResult> ChangeStatus(int id, ProjectTaskStatus status, string? returnUrl)
        {
            var task = await _db.ProjectTasks.FindAsync(id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return AccessDenied();

            _activity.LogChange(task.ProjectId, nameof(ProjectTask), id, "Status", task.Status, status);
            ApplyStatus(task, status);
            task.UpdatedAt = DateTime.Now;

            // A task moving to review is the reviewer's cue to look at it.
            if (status == ProjectTaskStatus.UnderReview && task.ReviewerId is int reviewer)
                _activity.Notify(reviewer, PmNotificationType.StatusChanged, $"Ready for review: {task.Name}",
                    null, Url.Action(nameof(Details), new { id }), task.ProjectId);

            await _db.SaveChangesAsync();
            await _metrics.RefreshProjectAsync(task.ProjectId);

            TempData["Success"] = $"Task status set to {status}.";
            return SafeRedirect(returnUrl, nameof(Details), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Dependencies
        // ════════════════════════════════════════════════════════════════════════

        [HttpPost]
        public async Task<IActionResult> AddDependency(int id, int predecessorTaskId, DependencyType type, int lagDays)
        {
            var task = await _db.ProjectTasks.FindAsync(id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return AccessDenied();

            if (await _scheduling.WouldCreateCycleAsync(id, predecessorTaskId))
            {
                TempData["Error"] = "That dependency would create a circular chain, which cannot be scheduled.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var duplicate = await _db.TaskDependencies.AnyAsync(d => d.TaskId == id && d.PredecessorTaskId == predecessorTaskId);
            if (!duplicate)
            {
                _db.TaskDependencies.Add(new TaskDependency
                {
                    TaskId = id,
                    PredecessorTaskId = predecessorTaskId,
                    Type = type,
                    LagDays = lagDays
                });
                _activity.Log(task.ProjectId, nameof(TaskDependency), id, "DependencyAdded", $"Predecessor #{predecessorTaskId} ({type})");
                await _db.SaveChangesAsync();
                await _scheduling.RecalculateCriticalPathAsync(task.ProjectId);
            }

            TempData["Success"] = "Dependency added.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveDependency(int id, int dependencyId)
        {
            var task = await _db.ProjectTasks.FindAsync(id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return AccessDenied();

            var dependency = await _db.TaskDependencies.FirstOrDefaultAsync(d => d.Id == dependencyId && d.TaskId == id);
            if (dependency != null)
            {
                _db.TaskDependencies.Remove(dependency);
                await _db.SaveChangesAsync();
                await _scheduling.RecalculateCriticalPathAsync(task.ProjectId);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Checklist, comments, attachments
        // ════════════════════════════════════════════════════════════════════════

        [HttpPost]
        public async Task<IActionResult> AddChecklistItem(int id, string text)
        {
            var task = await _db.ProjectTasks.FindAsync(id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return AccessDenied();

            if (!string.IsNullOrWhiteSpace(text))
            {
                var next = await _db.TaskChecklistItems.CountAsync(c => c.TaskId == id);
                _db.TaskChecklistItems.Add(new TaskChecklistItem { TaskId = id, Text = text.Trim(), Sequence = next });
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleChecklistItem(int id, int itemId)
        {
            var task = await _db.ProjectTasks.FindAsync(id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return AccessDenied();

            var item = await _db.TaskChecklistItems.FirstOrDefaultAsync(c => c.Id == itemId && c.TaskId == id);
            if (item != null)
            {
                item.IsDone = !item.IsDone;
                item.CompletedAt = item.IsDone ? DateTime.Now : null;
                item.CompletedById = item.IsDone ? Uid : null;

                // Nudge the task's percentage to match how much of its checklist is ticked, unless
                // the task is already finished.
                if (task.Status != ProjectTaskStatus.Completed)
                {
                    var items = await _db.TaskChecklistItems.Where(c => c.TaskId == id).ToListAsync();
                    if (items.Count > 0)
                        task.PercentComplete = (int)Math.Round(items.Count(c => c.IsDone) * 100.0 / items.Count);
                }

                await _db.SaveChangesAsync();
                await _metrics.RefreshProjectAsync(task.ProjectId);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteChecklistItem(int id, int itemId)
        {
            var task = await _db.ProjectTasks.FindAsync(id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return AccessDenied();

            var item = await _db.TaskChecklistItems.FirstOrDefaultAsync(c => c.Id == itemId && c.TaskId == id);
            if (item != null) { _db.TaskChecklistItems.Remove(item); await _db.SaveChangesAsync(); }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(int id, string body)
        {
            var task = await _db.ProjectTasks.FindAsync(id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanView) return AccessDenied();

            if (string.IsNullOrWhiteSpace(body)) return RedirectToAction(nameof(Details), new { id });

            var mentioned = await _activity.ResolveMentionsAsync(body);
            _db.TaskComments.Add(new TaskComment
            {
                TaskId = id,
                Body = body.Trim(),
                AuthorId = Uid,
                MentionedUserIds = mentioned.Count > 0 ? string.Join(",", mentioned) : null
            });

            var url = Url.Action(nameof(Details), new { id });
            foreach (var userId in mentioned)
                _activity.Notify(userId, PmNotificationType.Mention, $"You were mentioned on {task.Name}",
                    body.Length > 200 ? body[..200] : body, url, task.ProjectId);

            // The assignee hears about every comment on their task, mentioned or not.
            if (task.AssignedToId is int assignee && !mentioned.Contains(assignee))
                _activity.Notify(assignee, PmNotificationType.Comment, $"New comment on {task.Name}",
                    body.Length > 200 ? body[..200] : body, url, task.ProjectId);

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> UploadAttachment(int id, IFormFile? file)
        {
            var task = await _db.ProjectTasks.FindAsync(id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return AccessDenied();

            var saved = await _files.SaveAsync(file, "tasks", id);
            if (saved == null)
            {
                TempData["Error"] = _files.LastError ?? "The file could not be attached.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _db.TaskAttachments.Add(new TaskAttachment
            {
                TaskId = id,
                FileName = saved.OriginalName,
                StoredPath = saved.RelativePath,
                ContentType = saved.ContentType,
                SizeBytes = saved.Size,
                UploadedById = Uid
            });
            await _db.SaveChangesAsync();

            TempData["Success"] = "File attached.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAttachment(int id, int attachmentId)
        {
            var task = await _db.ProjectTasks.FindAsync(id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return AccessDenied();

            var attachment = await _db.TaskAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.TaskId == id);
            if (attachment != null)
            {
                _files.Delete(attachment.StoredPath);
                _db.TaskAttachments.Remove(attachment);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Suggested priorities & delete
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Ranked "what to do next" for a project, with the reasoning behind each score.</summary>
        public async Task<IActionResult> Priorities(int projectId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.CanView) return AccessDenied();

            await PopulateListsAsync(projectId);
            return View(await _intelligence.PrioritiseTasksAsync(projectId, 25));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var task = await _db.ProjectTasks
                .Include(t => t.AssignedTo).Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return AccessDenied();

            var vm = new DeleteConfirmationVm
            {
                EntityName = "Task",
                Icon = "fa-list-check",
                RecordTitle = task.Name,
                Reference = task.Reference,
                Id = task.Id,
                Controller = "ProjectTasks",
                CancelAction = "Details"
            };
            vm.Add("Project", task.Project?.Name);
            vm.Add("Status", task.Status.ToString());
            vm.Add("Assignee", task.AssignedTo == null ? null : $"{task.AssignedTo.FirstName} {task.AssignedTo.LastName}");
            vm.Add("Due", task.DueDate?.ToString("d MMM yyyy"));
            vm.Add("Progress", $"{task.PercentComplete}%");

            var subtasks = await _db.ProjectTasks.CountAsync(t => t.ParentTaskId == id);
            var hours = await _db.TimeEntries.Where(t => t.TaskId == id).SumAsync(t => (decimal?)t.Hours) ?? 0m;

            if (subtasks > 0) vm.Consequences.Add($"{subtasks} subtask(s) will be orphaned — reassign them first.");
            vm.Consequences.Add("All comments, checklist items and attachments on this task");
            if (hours > 0) vm.Consequences.Add($"{hours:N1} recorded hour(s) will lose their task link (the time itself is kept against the project)");
            vm.Consequences.Add("Any dependency where this task is a predecessor");

            return View("DeleteConfirm", vm);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var task = await _db.ProjectTasks.FindAsync(id);
            if (task == null) return NotFound();

            var ctx = await LoadContextAsync(task.ProjectId);
            if (ctx == null || !ctx.CanContribute) return AccessDenied();

            var subtasks = await _db.ProjectTasks.CountAsync(t => t.ParentTaskId == id);
            if (subtasks > 0)
            {
                TempData["Error"] = $"This task has {subtasks} subtask(s). Move or delete them first.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Detach the time entries so the hours stay on the project ledger.
            foreach (var entry in await _db.TimeEntries.Where(t => t.TaskId == id).ToListAsync())
                entry.TaskId = null;

            var projectId = task.ProjectId;
            task.IsDeleted = true;
            _activity.Log(projectId, nameof(ProjectTask), id, "Deleted", task.Name);
            await _db.SaveChangesAsync();
            await _metrics.RefreshProjectAsync(projectId);

            TempData["Success"] = "Task deleted.";
            return RedirectToAction(nameof(Index), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Permission flags plus the project header data every task view renders.</summary>
        private sealed class TaskContext
        {
            public Project Project { get; init; } = null!;
            public bool CanView { get; init; }
            public bool CanEdit { get; init; }
            public bool CanContribute { get; init; }
        }

        private async Task<TaskContext?> LoadContextAsync(int projectId)
        {
            var (project, team) = await ProjectsController.LoadAsync(_db, projectId);
            if (project == null) return null;

            var teamIds = team.Select(t => t.UserId).ToList();
            var ctx = new TaskContext
            {
                Project = project,
                CanView = PmAccess.CanView(project, Uid, Role, teamIds),
                CanEdit = PmAccess.CanEdit(project, Uid, Role),
                CanContribute = PmAccess.CanContribute(project, Uid, Role, teamIds)
            };

            ViewBag.Project = project;
            ViewBag.Team = team;
            ViewBag.CanEdit = ctx.CanEdit;
            ViewBag.CanContribute = ctx.CanContribute;
            return ctx;
        }

        /// <summary>Only ever redirect to a local URL — a caller-supplied absolute URL is discarded.</summary>
        private IActionResult SafeRedirect(string? returnUrl, string fallbackAction, object routeValues) =>
            !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction(fallbackAction, routeValues);

        private async Task PopulateListsAsync(int projectId, int? excludeTaskId = null)
        {
            ViewBag.Users = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive).OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();

            ViewBag.Phases = await _db.ProjectPhases.AsNoTracking()
                .Where(p => p.ProjectId == projectId).OrderBy(p => p.Sequence)
                .Select(p => new { p.Id, p.Name }).ToListAsync();

            ViewBag.Milestones = await _db.Milestones.AsNoTracking()
                .Where(m => m.ProjectId == projectId).OrderBy(m => m.DueDate)
                .Select(m => new { m.Id, m.Name }).ToListAsync();

            ViewBag.WbsItems = await _db.WbsItems.AsNoTracking()
                .Where(w => w.ProjectId == projectId).OrderBy(w => w.WbsCode)
                .Select(w => new { w.Id, Name = w.WbsCode + " " + w.Name }).ToListAsync();

            ViewBag.ParentOptions = await _db.ProjectTasks.AsNoTracking()
                .Where(t => t.ProjectId == projectId && (excludeTaskId == null || t.Id != excludeTaskId))
                .OrderBy(t => t.Name).Select(t => new { t.Id, t.Name }).ToListAsync();
        }
    }
}
