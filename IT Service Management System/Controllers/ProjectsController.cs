using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Helpers.Pm;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Pm;
using IT_Service_Management_System.Services.Pm;
using IT_Service_Management_System.ViewModels.Pm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// The project register and the project record itself.
    ///
    /// Visibility is not a role gate. Anyone can be put on a project team whatever their role, so
    /// the module is open to every signed-in user and the <em>query</em> does the restricting:
    /// portfolio-wide roles see everything, everyone else sees the projects they own or are a member
    /// of. <see cref="PmAccess"/> then decides what they may do once they are looking at one.
    /// </summary>
    public class ProjectsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectMetricsService _metrics;
        private readonly ProjectSchedulingService _scheduling;

        public ProjectsController(ApplicationDbContext db, ProjectMetricsService metrics,
            ProjectSchedulingService scheduling)
        {
            _db = db;
            _metrics = metrics;
            _scheduling = scheduling;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");

        // Session-based auth has no auth scheme, so Forbid() would throw. Redirect instead.
        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        public async Task<IActionResult> Index(
            int page = 1,
            string? q = null,
            ProjectStatus? status = null,
            ProjectCategory? category = null,
            ProjectHealth? health = null,
            string? manager = null,
            bool overdue = false)
        {
            // Clients reach projects only through the client portal, which scopes by client name.
            if (Roles.IsClient(Role)) return Denied();

            var filter = new ProjectFilter
            {
                Q = q, Status = status, Category = category,
                Health = health, Manager = manager, Overdue = overdue
            };

            var visible = Visible();

            // Counts describe the whole visible portfolio, deliberately ignoring the filter — they
            // are the denominator the reader narrows against.
            var today = DateTime.Today;
            var model = new ProjectsIndexVm
            {
                Filter = filter,
                TotalCount = await visible.CountAsync(),
                OpenCount = await visible.CountAsync(p =>
                    p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled &&
                    p.Status != ProjectStatus.Archived),
                OverdueCount = await visible.CountAsync(p =>
                    p.EndDate != null && p.EndDate < today &&
                    p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled &&
                    p.Status != ProjectStatus.Archived),
                AtRiskCount = await visible.CountAsync(p => p.Health != ProjectHealth.Green),
                // User.FullName is computed in C# and has no column, so the name is composed
                // in the query instead — the same shape the filter below compares against.
                Managers = await visible
                    .Where(p => p.ProjectManager != null)
                    .Select(p => p.ProjectManager!.FirstName + " " + p.ProjectManager.LastName)
                    .Distinct().OrderBy(n => n).Take(100).ToListAsync()
            };

            var query = Filtered(visible, filter, today);

            var (items, paging) = await query
                .Include(p => p.ProjectManager)
                .Include(p => p.Department)
                .OrderByDescending(p => p.Status == ProjectStatus.Active)
                .ThenBy(p => p.EndDate ?? DateTime.MaxValue)
                .ThenByDescending(p => p.Id)
                .PageAsync(page, 20);

            model.Projects = items;
            ViewBag.Paging = paging;
            ViewBag.CanCreate = CanCreate();

            return View(model);
        }

        // ── portfolio dashboard ──────────────────────────────────────────────────

        /// <summary>
        /// The executive view across every project. Deliberately not scoped per user: these are
        /// portfolio totals, and a figure that silently means "your slice" would be read as the
        /// organisation's. Only the roles with portfolio-wide sight get in at all.
        /// </summary>
        public async Task<IActionResult> Dashboard()
        {
            if (!CanSeePortfolio()) return Denied();

            var model = await _metrics.BuildDashboardAsync();
            return View(model);
        }

        // ── create and edit ──────────────────────────────────────────────────────

        public async Task<IActionResult> Create()
        {
            if (!CanCreate()) return Denied();

            await PopulateFormAsync();
            return View("Form", new Project
            {
                // Sensible opening plan: starts today, a quarter long. Both are editable, but a
                // blank date pair makes every progress and variance figure read as unknown.
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(3),
                Status = ProjectStatus.Draft
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Project input)
        {
            if (!CanCreate()) return Denied();

            if (!ModelState.IsValid)
            {
                await PopulateFormAsync();
                return View("Form", input);
            }

            input.Code = await ResolveCodeAsync(input.Code, null);
            input.CreatedById = Uid;
            input.CreatedAt = DateTime.Now;

            // Whoever starts a project manages it until someone says otherwise. A project with no
            // owner is the thing that quietly rots, so it never starts out that way.
            input.ProjectManagerId ??= Uid;

            _db.Projects.Add(input);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Project {input.Reference} created.";
            return RedirectToAction(nameof(Details), new { id = input.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return Denied();

            await PopulateFormAsync();
            return View("Form", project);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Project input)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == input.Id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return Denied();

            if (!ModelState.IsValid)
            {
                await PopulateFormAsync();
                return View("Form", input);
            }

            // Copied field by field rather than by attaching the posted entity: a form post must
            // not be able to reassign ownership metadata, progress roll-ups or the approval stamp.
            project.Code = await ResolveCodeAsync(input.Code, project.Id);
            project.Name = input.Name;
            project.Description = input.Description;
            project.Client = input.Client;
            project.DepartmentId = input.DepartmentId;
            project.SponsorId = input.SponsorId;
            project.ProjectManagerId = input.ProjectManagerId;
            project.Priority = input.Priority;
            project.Category = input.Category;
            project.Type = input.Type;
            project.StartDate = input.StartDate;
            project.EndDate = input.EndDate;
            project.Budget = input.Budget;
            project.Currency = input.Currency;
            project.Location = input.Location;
            project.Tags = input.Tags;
            project.Health = input.Health;
            project.HealthNote = input.HealthNote;
            project.AutoCalculateProgress = input.AutoCalculateProgress;

            // Hand-entered progress is only honoured when the roll-up is switched off; otherwise
            // the figure belongs to the tasks and would be overwritten on the next refresh anyway.
            if (!input.AutoCalculateProgress)
                project.ProgressPercent = input.ProgressPercent;

            project.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Project updated.";
            return RedirectToAction(nameof(Details), new { id = project.Id });
        }

        /// <summary>
        /// Settles the project code. Blank means "generate one"; a supplied code is kept unless it
        /// collides, since Code carries a unique index and a duplicate would surface as a raw SQL
        /// error on save rather than something the user can act on.
        /// </summary>
        private async Task<string> ResolveCodeAsync(string? requested, int? ownId)
        {
            var code = (requested ?? string.Empty).Trim();

            if (code.Length > 0)
            {
                var taken = await _db.Projects
                    .AnyAsync(p => p.Code == code && (ownId == null || p.Id != ownId));
                if (!taken) return code;
            }

            var year = DateTime.Today.Year;
            var prefix = $"PRJ-{year}-";
            var used = await _db.Projects
                .Where(p => p.Code.StartsWith(prefix))
                .Select(p => p.Code)
                .ToListAsync();

            var next = 1;
            foreach (var existing in used)
            {
                if (int.TryParse(existing[prefix.Length..], out var n) && n >= next)
                    next = n + 1;
            }

            return $"{prefix}{next:D3}";
        }

        private async Task PopulateFormAsync()
        {
            ViewBag.Departments = await _db.Departments.AsNoTracking()
                .OrderBy(d => d.Name).ToListAsync();
            ViewBag.Users = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();
        }

        // ── the project record ───────────────────────────────────────────────────

        public async Task<IActionResult> Details(int id)
        {
            var project = await _db.Projects
                .Include(p => p.ProjectManager)
                .Include(p => p.Sponsor)
                .Include(p => p.Department)
                .Include(p => p.CreatedBy)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null) return NotFound();

            var team = await _db.ProjectTeamMembers
                .Include(m => m.User)
                .Where(m => m.ProjectId == id && m.IsActive)
                .OrderBy(m => m.Role)
                .ToListAsync();

            if (!PmAccess.CanView(project, Uid, Role, team.Select(m => m.UserId))) return Denied();

            // Roll-ups are counted in the database. A project with hundreds of tasks should not
            // load them all just to print "84 of 95 done".
            var model = new ProjectDetailsVm
            {
                Project = project,
                Team = team,
                MilestonesTotal = await _db.Milestones.CountAsync(m => m.ProjectId == id),
                MilestonesAchieved = await _db.Milestones
                    .CountAsync(m => m.ProjectId == id && m.Status == MilestoneStatus.Achieved),
                TasksTotal = await _db.ProjectTasks.CountAsync(t => t.ProjectId == id),
                TasksCompleted = await _db.ProjectTasks
                    .CountAsync(t => t.ProjectId == id && t.Status == ProjectTaskStatus.Completed),
                LinkedRecords = await _db.ProjectItsmLinks.CountAsync(l => l.ProjectId == id),
                OpenRisks = await _db.ProjectRisks
                    .CountAsync(r => r.ProjectId == id && r.Status != PmRiskStatus.Closed),
                OpenIssues = await _db.ProjectIssues
                    .CountAsync(i => i.ProjectId == id && i.Status != IssueStatus.Closed),
                CanEdit = PmAccess.CanEdit(project, Uid, Role),
                CanContribute = PmAccess.CanContribute(project, Uid, Role, team.Select(m => m.UserId)),
                CanApprove = PmAccess.CanApprove(Role),
                CanDelete = Roles.IsFullAccess(Role) || (project.IsOpen && project.ProjectManagerId == Uid)
            };

            if (model.CanEdit)
            {
                var onTeam = team.Select(m => m.UserId).ToList();
                model.AssignableUsers = await _db.Users.AsNoTracking()
                    .Where(u => u.IsActive && !onTeam.Contains(u.Id))
                    .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                    .ToListAsync();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, ProjectStatus status)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return Denied();

            // Reaching a terminal state stamps the actual finish, which is what every schedule
            // variance figure is measured against — leaving it null would report a growing delay
            // on a project that has in fact landed.
            project.Status = status;
            project.UpdatedAt = DateTime.Now;

            if (status is ProjectStatus.Completed or ProjectStatus.Cancelled)
                project.ActualEndDate ??= DateTime.Today;
            else
                project.ActualEndDate = null;

            if (status == ProjectStatus.Active)
                project.ActualStartDate ??= DateTime.Today;

            await _db.SaveChangesAsync();
            TempData["Success"] = $"Project moved to {status}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTeamMember(int id, int userId, TeamRole role, int allocationPercent = 100)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return Denied();

            // Someone who rolled off keeps their row so their time entries stay attributable;
            // adding them back reactivates that row rather than creating a duplicate.
            var existing = await _db.ProjectTeamMembers
                .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == userId);

            if (existing != null)
            {
                existing.IsActive = true;
                existing.Role = role;
                existing.AllocationPercent = allocationPercent;
                existing.ToDate = null;
            }
            else
            {
                _db.ProjectTeamMembers.Add(new ProjectTeamMember
                {
                    ProjectId = id,
                    UserId = userId,
                    Role = role,
                    AllocationPercent = allocationPercent,
                    FromDate = DateTime.Today
                });
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Team member added.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveTeamMember(int id, int memberId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return Denied();

            var member = await _db.ProjectTeamMembers
                .FirstOrDefaultAsync(m => m.Id == memberId && m.ProjectId == id);
            if (member == null) return NotFound();

            // Rolled off, not deleted: time already booked against this project must stay
            // attributable to the person who booked it.
            member.IsActive = false;
            member.ToDate = DateTime.Today;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Team member rolled off.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetBaseline(int id)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return Denied();

            // Copies every current date — project end, milestone due dates, task start and due —
            // into its baseline field. From here on, variance is measured against this snapshot,
            // and a slipped date shows as a shift from a ghost bar rather than simply moving.
            // Freezing again replaces the previous baseline; there is no history of baselines.
            await _scheduling.SetBaselineAsync(id);

            TempData["Success"] = "Baseline frozen. Slippage is now measured against today's plan.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound();

            // Removing a project is a step beyond editing it. Administrators can always do it;
            // otherwise it stays with the project's own manager, on an open project.
            if (!(Roles.IsFullAccess(Role) || (project.IsOpen && project.ProjectManagerId == Uid)))
                return Denied();

            // Soft delete. The row and everything under it — tasks, milestones, links, booked
            // time — stay in the database and out of every query, so the record can be restored
            // and history stays attributable. Its code stays claimed by the unique index; a new
            // project cannot reuse it, which is the right outcome for an identifier.
            project.IsDeleted = true;
            project.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Project {project.Reference} deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── milestones ───────────────────────────────────────────────────────────

        public async Task<IActionResult> Milestones(int id)
        {
            var context = await LoadForTabAsync(id);
            if (context.Result != null) return context.Result;

            var milestones = await _db.Milestones
                .Include(m => m.Owner)
                .Where(m => m.ProjectId == id)
                .OrderBy(m => m.SortOrder).ThenBy(m => m.DueDate).ThenBy(m => m.Id)
                .ToListAsync();

            // Task counts per milestone, in one query rather than one per row.
            var taskCounts = await _db.ProjectTasks
                .Where(t => t.ProjectId == id && t.MilestoneId != null)
                .GroupBy(t => t.MilestoneId!.Value)
                .Select(g => new
                {
                    MilestoneId = g.Key,
                    Total = g.Count(),
                    Done = g.Count(t => t.Status == ProjectTaskStatus.Completed)
                })
                .ToListAsync();

            ViewBag.TaskCounts = taskCounts.ToDictionary(c => c.MilestoneId, c => (c.Total, c.Done));
            ViewBag.Owners = await TeamAndOwnersAsync(id);

            return View(new ProjectPlanVm
            {
                Project = context.Project!,
                Milestones = milestones,
                CanContribute = context.CanContribute
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveMilestone(int id, Milestone input)
        {
            var context = await LoadForTabAsync(id, needContribute: true);
            if (context.Result != null) return context.Result;

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "A milestone needs a name.";
                return RedirectToAction(nameof(Milestones), new { id });
            }

            Milestone milestone;
            if (input.Id == 0)
            {
                milestone = new Milestone
                {
                    ProjectId = id,
                    // New milestones land at the end of the plan. Spacing by 10 leaves room to drop
                    // something in between later without renumbering the whole list.
                    SortOrder = await NextMilestoneOrderAsync(id),
                    // The first committed date is the baseline slippage is measured from. Capture it
                    // now, because once the date has moved the original is unrecoverable.
                    BaselineDate = input.DueDate
                };
                _db.Milestones.Add(milestone);
            }
            else
            {
                var existing = await _db.Milestones
                    .FirstOrDefaultAsync(m => m.Id == input.Id && m.ProjectId == id);
                if (existing == null) return NotFound();
                milestone = existing;
            }

            milestone.Name = input.Name.Trim();
            milestone.DueDate = input.DueDate;
            milestone.OwnerId = input.OwnerId;
            milestone.Status = input.Status;
            milestone.RequiresClientApproval = input.RequiresClientApproval;

            // Achieving a milestone stamps the date it happened; reopening one clears it, so the
            // variance figure cannot keep quoting a completion that was undone.
            if (input.Status == MilestoneStatus.Achieved)
                milestone.AchievedDate ??= DateTime.Today;
            else
                milestone.AchievedDate = null;

            await _db.SaveChangesAsync();

            TempData["Success"] = input.Id == 0 ? "Milestone added." : "Milestone updated.";
            return RedirectToAction(nameof(Milestones), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveMilestone(int id, int milestoneId, string direction)
        {
            var context = await LoadForTabAsync(id, needContribute: true);
            if (context.Result != null) return context.Result;

            var ordered = await _db.Milestones
                .Where(m => m.ProjectId == id)
                .OrderBy(m => m.SortOrder).ThenBy(m => m.DueDate).ThenBy(m => m.Id)
                .ToListAsync();

            var index = ordered.FindIndex(m => m.Id == milestoneId);
            if (index < 0) return NotFound();

            var target = direction == "up" ? index - 1 : index + 1;
            if (target < 0 || target >= ordered.Count)
                return RedirectToAction(nameof(Milestones), new { id });

            (ordered[index], ordered[target]) = (ordered[target], ordered[index]);

            // Renumber the whole list from the swapped order. Rewriting every row is cheap at this
            // scale and leaves a clean sequence, rather than trading two values that may both be
            // zero on milestones created before ordering existed.
            for (var i = 0; i < ordered.Count; i++)
                ordered[i].SortOrder = (i + 1) * 10;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Milestones), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMilestone(int id, int milestoneId)
        {
            var context = await LoadForTabAsync(id, needContribute: true);
            if (context.Result != null) return context.Result;

            var milestone = await _db.Milestones
                .FirstOrDefaultAsync(m => m.Id == milestoneId && m.ProjectId == id);
            if (milestone == null) return NotFound();

            // Tasks outlive the milestone they were grouped under. Deleting a stage of the plan
            // must not delete the work, so those tasks fall back to sitting on the project itself.
            var orphaned = await _db.ProjectTasks
                .Where(t => t.MilestoneId == milestoneId).ToListAsync();
            foreach (var task in orphaned) task.MilestoneId = null;

            _db.Milestones.Remove(milestone);
            await _db.SaveChangesAsync();

            TempData["Success"] = orphaned.Count == 0
                ? "Milestone deleted."
                : $"Milestone deleted. {orphaned.Count} task(s) now sit directly on the project.";
            return RedirectToAction(nameof(Milestones), new { id });
        }

        private async Task<int> NextMilestoneOrderAsync(int projectId)
        {
            var highest = await _db.Milestones
                .Where(m => m.ProjectId == projectId)
                .Select(m => (int?)m.SortOrder)
                .MaxAsync() ?? 0;
            return highest + 10;
        }

        /// <summary>Who can own a milestone or task: the active team, plus the manager and sponsor.</summary>
        private async Task<List<User>> TeamAndOwnersAsync(int projectId)
        {
            var ids = await _db.ProjectTeamMembers
                .Where(m => m.ProjectId == projectId && m.IsActive)
                .Select(m => m.UserId)
                .ToListAsync();

            var owners = await _db.Projects.AsNoTracking()
                .Where(p => p.Id == projectId)
                .Select(p => new { p.ProjectManagerId, p.SponsorId })
                .FirstOrDefaultAsync();

            if (owners?.ProjectManagerId != null) ids.Add(owners.ProjectManagerId.Value);
            if (owners?.SponsorId != null) ids.Add(owners.SponsorId.Value);

            return await _db.Users.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .ToListAsync();
        }

        // ── tasks ────────────────────────────────────────────────────────────────

        public async Task<IActionResult> Tasks(int id, string view = "list", int? milestoneId = null,
            int? assigneeId = null, bool openOnly = false)
        {
            var context = await LoadForTabAsync(id);
            if (context.Result != null) return context.Result;

            IQueryable<ProjectTask> query = _db.ProjectTasks
                .Include(t => t.AssignedTo)
                .Include(t => t.Milestone)
                .Where(t => t.ProjectId == id);

            if (milestoneId.HasValue)
                query = query.Where(t => t.MilestoneId == milestoneId.Value);

            if (assigneeId.HasValue)
                query = query.Where(t => t.AssignedToId == assigneeId.Value);

            if (openOnly)
                query = query.Where(t => t.Status != ProjectTaskStatus.Completed &&
                                         t.Status != ProjectTaskStatus.Cancelled);

            var tasks = await query
                // Board position first so the lanes read in the order someone arranged them;
                // the list view inherits that same sequence rather than inventing its own.
                .OrderBy(t => t.BoardOrder).ThenBy(t => t.DueDate ?? DateTime.MaxValue).ThenBy(t => t.Id)
                .ToListAsync();

            ViewBag.Owners = await TeamAndOwnersAsync(id);
            ViewBag.Milestones = await _db.Milestones.AsNoTracking()
                .Where(m => m.ProjectId == id)
                .OrderBy(m => m.SortOrder).ThenBy(m => m.DueDate)
                .ToListAsync();

            // Every task in the project is a candidate predecessor, whatever the filter shows.
            var allTasks = await _db.ProjectTasks.AsNoTracking()
                .Where(t => t.ProjectId == id && t.Status != ProjectTaskStatus.Cancelled)
                .Select(t => new { t.Id, t.Name })
                .OrderBy(t => t.Name)
                .ToListAsync();
            ViewBag.AllTasks = allTasks.Select(t => (t.Id, t.Name)).ToList();

            var allIds = allTasks.Select(t => t.Id).ToList();
            var names = allTasks.ToDictionary(t => t.Id, t => t.Name);
            var predecessors = await _db.TaskDependencies.AsNoTracking()
                .Where(d => allIds.Contains(d.TaskId))
                .Select(d => new { d.Id, d.TaskId, d.PredecessorTaskId })
                .ToListAsync();

            return View(new ProjectTasksVm
            {
                Project = context.Project!,
                Tasks = tasks,
                Predecessors = predecessors
                    .GroupBy(d => d.TaskId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(d => new TaskPredecessor(
                                d.Id, d.PredecessorTaskId,
                                names.TryGetValue(d.PredecessorTaskId, out var n) ? n : $"Task #{d.PredecessorTaskId}"))
                              .ToList()),
                CanContribute = context.CanContribute,
                View = view == "board" ? "board" : "list",
                MilestoneId = milestoneId,
                AssigneeId = assigneeId,
                OpenOnly = openOnly
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTask(int id, ProjectTask input, string? returnView = null)
        {
            var context = await LoadForTabAsync(id, needContribute: true);
            if (context.Result != null) return context.Result;

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "A task needs a name.";
                return RedirectToAction(nameof(Tasks), new { id, view = returnView });
            }

            ProjectTask task;
            if (input.Id == 0)
            {
                task = new ProjectTask
                {
                    ProjectId = id,
                    BoardOrder = await NextBoardOrderAsync(id)
                };
                _db.ProjectTasks.Add(task);
            }
            else
            {
                var existing = await _db.ProjectTasks
                    .FirstOrDefaultAsync(t => t.Id == input.Id && t.ProjectId == id);
                if (existing == null) return NotFound();
                task = existing;
            }

            task.Name = input.Name.Trim();
            task.MilestoneId = input.MilestoneId;
            task.AssignedToId = input.AssignedToId;
            task.Priority = input.Priority;
            task.DueDate = input.DueDate;
            task.EstimatedHours = input.EstimatedHours;

            ApplyStatus(task, input.Status);

            await _db.SaveChangesAsync();
            await RefreshProgressAsync(id);
            await _scheduling.RecalculateCriticalPathAsync(id);

            TempData["Success"] = input.Id == 0 ? "Task added." : "Task updated.";
            return RedirectToAction(nameof(Tasks), new { id, view = returnView });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveTask(int id, int taskId, KanbanColumn column)
        {
            var context = await LoadForTabAsync(id, needContribute: true);
            if (context.Result != null) return context.Result;

            var task = await _db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == id);
            if (task == null) return NotFound();

            task.Column = column;

            // The board lane and the status are separate fields so the board can be rearranged
            // freely, but dropping a card in Completed and leaving the status at In Progress makes
            // the two disagree in the reports. Moving to an end lane carries the status with it.
            if (column == KanbanColumn.Completed)
                ApplyStatus(task, ProjectTaskStatus.Completed);
            else if (task.Status == ProjectTaskStatus.Completed)
                ApplyStatus(task, ProjectTaskStatus.InProgress);

            await _db.SaveChangesAsync();
            await RefreshProgressAsync(id);

            return RedirectToAction(nameof(Tasks), new { id, view = "board" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTask(int id, int taskId, string? returnView = null)
        {
            var context = await LoadForTabAsync(id, needContribute: true);
            if (context.Result != null) return context.Result;

            var task = await _db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == id);
            if (task == null) return NotFound();

            // Subtasks would be orphaned by a bare delete and SQL Server will not cascade a
            // self-reference, so they are re-parented to whatever this task hung off.
            var children = await _db.ProjectTasks.Where(t => t.ParentTaskId == taskId).ToListAsync();
            foreach (var child in children) child.ParentTaskId = task.ParentTaskId;

            _db.ProjectTasks.Remove(task);
            await _db.SaveChangesAsync();
            await RefreshProgressAsync(id);
            await _scheduling.RecalculateCriticalPathAsync(id);

            TempData["Success"] = "Task deleted.";
            return RedirectToAction(nameof(Tasks), new { id, view = returnView });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDependency(int id, int taskId, int predecessorTaskId)
        {
            var context = await LoadForTabAsync(id, needContribute: true);
            if (context.Result != null) return context.Result;

            // Both ends must be this project's. A link across projects would let one plan hold
            // another hostage with no visibility from either side.
            var inProject = await _db.ProjectTasks
                .Where(t => t.ProjectId == id && (t.Id == taskId || t.Id == predecessorTaskId))
                .CountAsync();
            if (inProject != 2) return NotFound();

            if (await _db.TaskDependencies.AnyAsync(d => d.TaskId == taskId && d.PredecessorTaskId == predecessorTaskId))
            {
                TempData["Info"] = "That dependency already exists.";
                return RedirectToAction(nameof(Tasks), new { id });
            }

            // A cycle makes the plan unschedulable — nothing can ever start. Refused outright,
            // and the message says why rather than leaving the user to guess.
            if (await _scheduling.WouldCreateCycleAsync(taskId, predecessorTaskId))
            {
                TempData["Error"] = "That would create a loop — the predecessor already depends on this task, directly or through others.";
                return RedirectToAction(nameof(Tasks), new { id });
            }

            _db.TaskDependencies.Add(new TaskDependency
            {
                TaskId = taskId,
                PredecessorTaskId = predecessorTaskId,
                Type = DependencyType.FinishToStart
            });
            await _db.SaveChangesAsync();
            await _scheduling.RecalculateCriticalPathAsync(id);

            TempData["Success"] = "Dependency added.";
            return RedirectToAction(nameof(Tasks), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveDependency(int id, int dependencyId)
        {
            var context = await LoadForTabAsync(id, needContribute: true);
            if (context.Result != null) return context.Result;

            var dep = await _db.TaskDependencies
                .Include(d => d.Task)
                .FirstOrDefaultAsync(d => d.Id == dependencyId && d.Task!.ProjectId == id);
            if (dep == null) return NotFound();

            _db.TaskDependencies.Remove(dep);
            await _db.SaveChangesAsync();
            await _scheduling.RecalculateCriticalPathAsync(id);

            TempData["Success"] = "Dependency removed.";
            return RedirectToAction(nameof(Tasks), new { id });
        }

        /// <summary>
        /// Applies a status change and the dates that go with it. Completion stamps the date and
        /// forces the percentage to 100; reopening clears both, so a task cannot sit at "100% but
        /// In Progress" and quietly inflate the project roll-up.
        /// </summary>
        private static void ApplyStatus(ProjectTask task, ProjectTaskStatus status)
        {
            task.Status = status;

            if (status == ProjectTaskStatus.Completed)
            {
                task.CompletionDate ??= DateTime.Today;
                task.PercentComplete = 100;
                task.Column = KanbanColumn.Completed;
            }
            else
            {
                task.CompletionDate = null;
                if (task.PercentComplete == 100) task.PercentComplete = 0;
                if (task.Column == KanbanColumn.Completed) task.Column = KanbanColumn.InProgress;
            }
        }

        /// <summary>
        /// Rolls task completion up into the project percentage, for projects that asked for it.
        /// Kept here rather than in the view so the figure is the same wherever it is read.
        /// </summary>
        private async Task RefreshProgressAsync(int projectId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null || !project.AutoCalculateProgress) return;

            var total = await _db.ProjectTasks.CountAsync(t => t.ProjectId == projectId &&
                                                               t.Status != ProjectTaskStatus.Cancelled);
            if (total == 0) return;

            var done = await _db.ProjectTasks.CountAsync(t => t.ProjectId == projectId &&
                                                              t.Status == ProjectTaskStatus.Completed);

            project.ProgressPercent = done * 100 / total;
            project.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        private async Task<int> NextBoardOrderAsync(int projectId)
        {
            var highest = await _db.ProjectTasks
                .Where(t => t.ProjectId == projectId)
                .Select(t => (int?)t.BoardOrder)
                .MaxAsync() ?? 0;
            return highest + 10;
        }

        // ── schedule: overlaps, timelines, deadlines ─────────────────────────────

        /// <summary>
        /// One page for the three scheduling questions that usually need three tools: what is
        /// running at once, who is double-booked, and what falls due.
        ///
        /// Scoped to the projects the viewer may see, so it works for a team member looking at
        /// their own commitments as well as for a manager looking across the portfolio.
        /// </summary>
        public async Task<IActionResult> Schedule(int days = 60)
        {
            if (Roles.IsClient(Role)) return Denied();

            // Keep the window sane: a day is useless and five years is unreadable.
            days = Math.Clamp(days, 14, 365);

            var today = DateTime.Today;
            var windowStart = today.AddDays(-14);   // a fortnight back, so recent slippage is visible
            var windowEnd = today.AddDays(days);
            var totalDays = Math.Max(1, (windowEnd - windowStart).TotalDays);

            double Left(DateTime d) => Math.Clamp((d.Date - windowStart).TotalDays / totalDays * 100.0, 0, 100);
            double Width(DateTime from, DateTime to)
            {
                var a = from < windowStart ? windowStart : from;
                var b = to > windowEnd ? windowEnd : to;
                var span = Math.Max(1, (b.Date - a.Date).TotalDays + 1);
                return Math.Clamp(span / totalDays * 100.0, 0.4, 100);
            }

            var model = new ProjectScheduleVm
            {
                WindowDays = days,
                WindowStart = windowStart,
                WindowEnd = windowEnd,
                TodayPercent = Left(today)
            };

            var visibleIds = await Visible().Select(p => p.Id).ToListAsync();

            // ── Portfolio timeline: open projects that touch the window ──
            var projects = await _db.Projects.AsNoTracking()
                .Where(p => visibleIds.Contains(p.Id) &&
                            p.Status != ProjectStatus.Completed &&
                            p.Status != ProjectStatus.Cancelled &&
                            p.Status != ProjectStatus.Archived &&
                            p.StartDate != null && p.EndDate != null &&
                            p.StartDate <= windowEnd && p.EndDate >= windowStart)
                .OrderBy(p => p.StartDate)
                .ToListAsync();

            var index = 0;
            foreach (var p in projects)
            {
                model.ProjectBars.Add(new GanttRow
                {
                    Kind = GanttRowKind.Task,
                    Id = p.Id,
                    Name = p.Name,
                    Start = p.StartDate!.Value,
                    End = p.EndDate!.Value,
                    PercentComplete = p.ProgressPercent,
                    Status = p.Status.ToString(),
                    IsOverdue = p.IsOverdue,
                    IsCritical = p.Health == ProjectHealth.Red,
                    LeftPercent = Left(p.StartDate.Value),
                    WidthPercent = Width(p.StartDate.Value, p.EndDate.Value),
                    Index = index++
                });
            }

            // Contention in the portfolio itself: how many pairs of these run at the same time.
            for (var i = 0; i < projects.Count; i++)
                for (var j = i + 1; j < projects.Count; j++)
                    if (projects[i].StartDate <= projects[j].EndDate &&
                        projects[j].StartDate <= projects[i].EndDate)
                        model.OverlappingProjectPairs++;

            // ── Axis ──
            var cursor = new DateTime(windowStart.Year, windowStart.Month, 1);
            while (cursor <= windowEnd)
            {
                var monthEnd = cursor.AddMonths(1).AddDays(-1);
                var from = cursor < windowStart ? windowStart : cursor;
                var to = monthEnd > windowEnd ? windowEnd : monthEnd;
                model.Ticks.Add(new GanttTick
                {
                    Label = cursor.ToString("MMM yyyy"),
                    LeftPercent = Left(from),
                    WidthPercent = Math.Max(0, (to - from).TotalDays + 1) / totalDays * 100.0
                });
                cursor = cursor.AddMonths(1);
            }

            // ── Who is double-booked ──
            // Every open, dated task in view. The people panel below narrows this to the assigned
            // ones; the deadline list must not, because an unassigned task with a date looming is
            // the one most worth surfacing.
            var datedTasks = await _db.ProjectTasks.AsNoTracking()
                .Include(t => t.AssignedTo)
                .Include(t => t.Project)
                .Where(t => visibleIds.Contains(t.ProjectId) &&
                            t.Status != ProjectTaskStatus.Completed &&
                            t.Status != ProjectTaskStatus.Cancelled &&
                            (t.StartDate != null || t.DueDate != null))
                .ToListAsync();

            foreach (var group in datedTasks.Where(t => t.AssignedToId != null)
                                            .GroupBy(t => t.AssignedToId!.Value))
            {
                var user = group.First().AssignedTo;
                var row = new PersonLoadRow
                {
                    UserId = group.Key,
                    Name = user == null ? $"User #{group.Key}" : $"{user.FirstName} {user.LastName}".Trim()
                };

                foreach (var t in group)
                {
                    var from = (t.StartDate ?? t.DueDate!.Value).Date;
                    var to = (t.DueDate ?? t.StartDate!.Value).Date;
                    if (to < from) to = from;
                    if (to < windowStart || from > windowEnd) continue;

                    row.Spans.Add(new TaskSpan
                    {
                        TaskId = t.Id,
                        Name = t.Name,
                        ProjectId = t.ProjectId,
                        ProjectName = t.Project?.Name ?? string.Empty,
                        Start = from,
                        End = to,
                        IsOverdue = t.DueDate.HasValue && t.DueDate.Value.Date < today,
                        LeftPercent = Left(from),
                        WidthPercent = Width(from, to)
                    });
                }

                if (row.Spans.Count == 0) continue;

                // Sweep the interval starts and ends to find the busiest stretch. A person with six
                // tasks spread evenly is fine; three landing in one week is the problem.
                var events = row.Spans
                    .SelectMany(sp => new[] { (Date: sp.Start, Delta: 1), (Date: sp.End.AddDays(1), Delta: -1) })
                    .OrderBy(e => e.Date).ThenBy(e => e.Delta)
                    .ToList();

                var running = 0;
                var peak = 0;
                DateTime? peakFrom = null, peakTo = null;
                for (var i = 0; i < events.Count; i++)
                {
                    running += events[i].Delta;
                    if (running > peak)
                    {
                        peak = running;
                        peakFrom = events[i].Date;
                        peakTo = i + 1 < events.Count ? events[i + 1].Date.AddDays(-1) : events[i].Date;
                    }
                }

                row.PeakConcurrent = peak;
                row.PeakFrom = peakFrom;
                row.PeakTo = peakTo;
                model.People.Add(row);
            }

            model.People = model.People
                .OrderByDescending(p => p.PeakConcurrent)
                .ThenByDescending(p => p.OverdueCount)
                .ThenBy(p => p.Name)
                .ToList();

            // ── What falls due ──
            var deadlines = new List<DeadlineItem>();

            deadlines.AddRange(projects
                .Where(p => p.EndDate >= windowStart && p.EndDate <= windowEnd)
                .Select(p => new DeadlineItem
                {
                    Kind = DeadlineKind.Project,
                    Id = p.Id,
                    ProjectId = p.Id,
                    ProjectName = p.Name,
                    Name = $"{p.Name} ends",
                    Due = p.EndDate!.Value.Date,
                    IsOverdue = p.EndDate.Value.Date < today
                }));

            var milestones = await _db.Milestones.AsNoTracking()
                .Include(m => m.Project).Include(m => m.Owner)
                .Where(m => visibleIds.Contains(m.ProjectId) &&
                            m.Status != MilestoneStatus.Achieved &&
                            m.Status != MilestoneStatus.Cancelled &&
                            m.DueDate >= windowStart && m.DueDate <= windowEnd)
                .ToListAsync();

            deadlines.AddRange(milestones.Select(m => new DeadlineItem
            {
                Kind = DeadlineKind.Milestone,
                Id = m.Id,
                ProjectId = m.ProjectId,
                ProjectName = m.Project?.Name ?? string.Empty,
                Name = m.Name,
                Due = m.DueDate.Date,
                Owner = m.Owner == null ? null : $"{m.Owner.FirstName} {m.Owner.LastName}".Trim(),
                IsOverdue = m.DueDate.Date < today
            }));

            deadlines.AddRange(datedTasks
                .Where(t => t.DueDate != null && t.DueDate >= windowStart && t.DueDate <= windowEnd)
                .Select(t => new DeadlineItem
                {
                    Kind = DeadlineKind.Task,
                    Id = t.Id,
                    ProjectId = t.ProjectId,
                    ProjectName = t.Project?.Name ?? string.Empty,
                    Name = t.Name,
                    Due = t.DueDate!.Value.Date,
                    Owner = t.AssignedTo == null ? null : $"{t.AssignedTo.FirstName} {t.AssignedTo.LastName}".Trim(),
                    IsOverdue = t.DueDate.Value.Date < today
                }));

            model.Deadlines = deadlines
                .GroupBy(d => d.Due.Date)
                .OrderBy(g => g.Key)
                .Select(g => new DeadlineDay
                {
                    Date = g.Key,
                    Items = g.OrderBy(d => d.Kind).ThenBy(d => d.Name).ToList()
                })
                .ToList();

            return View(model);
        }

        // ── gantt ────────────────────────────────────────────────────────────────

        /// <summary>
        /// The project's schedule as a Gantt chart: milestones and tasks positioned on one time
        /// axis, with baselines, the critical path and dependency arrows.
        ///
        /// All the geometry is worked out here as percentages of the chart window, so the view does
        /// no date arithmetic and the bars, the axis ticks, the today line and the arrows are all
        /// derived from a single window that cannot drift between them.
        /// </summary>
        public async Task<IActionResult> Gantt(int id)
        {
            var context = await LoadForTabAsync(id);
            if (context.Result != null) return context.Result;
            var project = context.Project!;

            var milestones = await _db.Milestones.AsNoTracking()
                .Where(m => m.ProjectId == id)
                .OrderBy(m => m.SortOrder).ThenBy(m => m.DueDate).ThenBy(m => m.Id)
                .ToListAsync();

            var tasks = await _db.ProjectTasks.AsNoTracking()
                .Include(t => t.AssignedTo)
                .Where(t => t.ProjectId == id && t.Status != ProjectTaskStatus.Cancelled)
                .ToListAsync();

            var taskIds = tasks.Select(t => t.Id).ToList();
            var dependencies = await _db.TaskDependencies.AsNoTracking()
                .Where(d => taskIds.Contains(d.TaskId))
                .Select(d => new { d.TaskId, d.PredecessorTaskId })
                .ToListAsync();

            // A task needs both ends to be drawn. One date is enough to infer the other — a single
            // day — but neither means it cannot go on a timeline at all.
            var scheduled = tasks.Where(t => t.StartDate.HasValue || t.DueDate.HasValue).ToList();
            var model = new ProjectGanttVm
            {
                Project = project,
                CanContribute = context.CanContribute,
                Unscheduled = tasks.Where(t => !t.StartDate.HasValue && !t.DueDate.HasValue)
                                   .OrderBy(t => t.BoardOrder).ToList()
            };

            // ── The window: everything that has to fit, padded so bars do not touch the edges ──
            var dates = new List<DateTime>();
            foreach (var t in scheduled)
            {
                dates.Add(t.StartDate ?? t.DueDate!.Value);
                dates.Add(t.DueDate ?? t.StartDate!.Value);
                if (t.BaselineStartDate.HasValue) dates.Add(t.BaselineStartDate.Value);
                if (t.BaselineDueDate.HasValue) dates.Add(t.BaselineDueDate.Value);
            }
            foreach (var m in milestones) dates.Add(m.DueDate);
            if (project.StartDate.HasValue) dates.Add(project.StartDate.Value);
            if (project.EndDate.HasValue) dates.Add(project.EndDate.Value);
            dates.Add(DateTime.Today);

            var start = dates.Min().Date.AddDays(-3);
            var end = dates.Max().Date.AddDays(3);
            // A window narrower than a fortnight makes every bar a sliver; widen it.
            if ((end - start).TotalDays < 14) end = start.AddDays(14);

            var totalDays = Math.Max(1, (end - start).TotalDays);
            model.WindowStart = start;
            model.WindowEnd = end;
            model.WindowDays = (int)totalDays;

            double Left(DateTime d) => Math.Clamp((d.Date - start).TotalDays / totalDays * 100.0, 0, 100);
            double Width(DateTime from, DateTime to)
            {
                // Inclusive of the end day, so a one-day task is visible rather than zero-wide.
                var days = Math.Max(1, (to.Date - from.Date).TotalDays + 1);
                return Math.Clamp(days / totalDays * 100.0, 0.4, 100);
            }

            if (DateTime.Today >= start && DateTime.Today <= end)
                model.TodayPercent = Left(DateTime.Today);

            // ── Rows: each milestone, then the tasks under it, then unattached tasks ──
            var rows = new List<GanttRow>();

            void AddTasks(int? milestoneId)
            {
                foreach (var t in scheduled.Where(t => t.MilestoneId == milestoneId)
                                           .OrderBy(t => t.StartDate ?? t.DueDate)
                                           .ThenBy(t => t.Id))
                {
                    var from = (t.StartDate ?? t.DueDate!.Value).Date;
                    var to = (t.DueDate ?? t.StartDate!.Value).Date;
                    if (to < from) to = from;

                    var done = t.Status == ProjectTaskStatus.Completed;
                    var row = new GanttRow
                    {
                        Kind = GanttRowKind.Task,
                        Id = t.Id,
                        Name = t.Name,
                        Start = from,
                        End = to,
                        PercentComplete = done ? 100 : Math.Clamp(t.PercentComplete, 0, 100),
                        Status = t.Status.ToString(),
                        Assignee = t.AssignedTo == null ? null : t.AssignedTo.FirstName,
                        IsCritical = t.IsOnCriticalPath,
                        IsDone = done,
                        IsOverdue = !done && t.DueDate.HasValue && t.DueDate.Value.Date < DateTime.Today,
                        MilestoneId = t.MilestoneId,
                        LeftPercent = Left(from),
                        WidthPercent = Width(from, to),
                        PredecessorTaskIds = dependencies.Where(d => d.TaskId == t.Id)
                                                         .Select(d => d.PredecessorTaskId).ToList()
                    };

                    if (t.BaselineStartDate.HasValue && t.BaselineDueDate.HasValue)
                    {
                        row.BaselineStart = t.BaselineStartDate;
                        row.BaselineEnd = t.BaselineDueDate;
                        row.BaselineLeftPercent = Left(t.BaselineStartDate.Value);
                        row.BaselineWidthPercent = Width(t.BaselineStartDate.Value, t.BaselineDueDate.Value);
                    }

                    rows.Add(row);
                }
            }

            foreach (var m in milestones)
            {
                var achieved = m.Status == MilestoneStatus.Achieved;
                var at = (m.AchievedDate ?? m.DueDate).Date;
                rows.Add(new GanttRow
                {
                    Kind = GanttRowKind.Milestone,
                    Id = m.Id,
                    Name = m.Name,
                    Start = at,
                    End = at,
                    Status = m.Status.ToString(),
                    IsDone = achieved,
                    IsOverdue = m.IsOverdue,
                    PercentComplete = achieved ? 100 : 0,
                    LeftPercent = Left(at),
                    WidthPercent = Width(at, at),
                    BaselineStart = m.BaselineDate,
                    BaselineLeftPercent = m.BaselineDate.HasValue ? Left(m.BaselineDate.Value) : null
                });
                AddTasks(m.Id);
            }

            AddTasks(null);

            for (var i = 0; i < rows.Count; i++) rows[i].Index = i;
            model.Rows = rows;

            // ── Axis ticks: one per month the window covers ──
            var cursor = new DateTime(start.Year, start.Month, 1);
            while (cursor <= end)
            {
                var monthEnd = cursor.AddMonths(1).AddDays(-1);
                var visibleFrom = cursor < start ? start : cursor;
                var visibleTo = monthEnd > end ? end : monthEnd;

                model.Ticks.Add(new GanttTick
                {
                    Label = cursor.ToString("MMM yyyy"),
                    LeftPercent = Left(visibleFrom),
                    WidthPercent = Math.Max(0, (visibleTo - visibleFrom).TotalDays + 1) / totalDays * 100.0,
                    IsMonthStart = true
                });
                cursor = cursor.AddMonths(1);
            }

            return View(model);
        }

        // ── associations with the service desk ───────────────────────────────────

        public async Task<IActionResult> Associations(int id)
        {
            var context = await LoadForTabAsync(id);
            if (context.Result != null) return context.Result;

            var links = await _db.ProjectItsmLinks
                .Include(l => l.Ticket)
                .Include(l => l.ChangeRequest)
                .Include(l => l.Problem)
                .Include(l => l.Milestone)
                .Include(l => l.CreatedBy)
                .Where(l => l.ProjectId == id)
                .OrderBy(l => l.Relation).ThenByDescending(l => l.CreatedAt)
                .ToListAsync();

            // Candidates exclude what is already linked, so the same ticket cannot be attached
            // twice and the list shrinks as the project claims its work.
            var linkedTicketIds = links.Where(l => l.TicketId.HasValue)
                                       .Select(l => l.TicketId!.Value).ToList();

            var candidates = await _db.Tickets.AsNoTracking()
                .Where(t => !linkedTicketIds.Contains(t.Id))
                .OrderByDescending(t => t.CreatedAt)
                .Take(200)
                .Select(t => new TicketOption(t.Id, t.Reference, t.Title, t.Status.ToString()))
                .ToListAsync();

            return View(new ProjectAssociationsVm
            {
                Project = context.Project!,
                Links = links,
                TicketOptions = candidates,
                Milestones = await _db.Milestones.AsNoTracking()
                    .Where(m => m.ProjectId == id)
                    .OrderBy(m => m.SortOrder).ThenBy(m => m.DueDate)
                    .ToListAsync(),
                CanContribute = context.CanContribute
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LinkRecord(int id, int ticketId, ProjectLinkRelation relation,
            int? milestoneId, string? note)
        {
            var context = await LoadForTabAsync(id, needContribute: true);
            if (context.Result != null) return context.Result;

            var ticketExists = await _db.Tickets.AnyAsync(t => t.Id == ticketId);
            if (!ticketExists)
            {
                TempData["Error"] = "That ticket no longer exists.";
                return RedirectToAction(nameof(Associations), new { id });
            }

            // One ticket, one link. Re-linking the same ticket under a different relation is a
            // correction, not a second fact, so the existing row moves rather than multiplying.
            var existing = await _db.ProjectItsmLinks
                .FirstOrDefaultAsync(l => l.ProjectId == id && l.TicketId == ticketId);

            if (existing != null)
            {
                existing.Relation = relation;
                existing.MilestoneId = milestoneId;
                existing.Note = note;
                TempData["Success"] = "Association updated.";
            }
            else
            {
                _db.ProjectItsmLinks.Add(new ProjectItsmLink
                {
                    ProjectId = id,
                    TicketId = ticketId,
                    Relation = relation,
                    MilestoneId = milestoneId,
                    Note = note,
                    CreatedById = Uid
                });
                TempData["Success"] = "Ticket linked to the project.";
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Associations), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlinkRecord(int id, int linkId)
        {
            var context = await LoadForTabAsync(id, needContribute: true);
            if (context.Result != null) return context.Result;

            var link = await _db.ProjectItsmLinks
                .FirstOrDefaultAsync(l => l.Id == linkId && l.ProjectId == id);
            if (link == null) return NotFound();

            // Only the association goes. The ticket itself is the service desk's record and has
            // nothing to do with whether a project chose to claim it.
            _db.ProjectItsmLinks.Remove(link);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Association removed.";
            return RedirectToAction(nameof(Associations), new { id });
        }

        // ── tab plumbing ─────────────────────────────────────────────────────────

        /// <summary>
        /// Every tab needs the same three things: the project, whether this user may see it, and
        /// whether they may change anything on it. Resolving that once keeps the guard identical
        /// across tabs — a permission check re-typed per action is one that eventually differs.
        /// </summary>
        private async Task<(Project? Project, bool CanContribute, IActionResult? Result)> LoadForTabAsync(
            int id, bool needContribute = false)
        {
            var project = await _db.Projects
                .Include(p => p.ProjectManager)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null) return (null, false, NotFound());

            var team = await _db.ProjectTeamMembers
                .Where(m => m.ProjectId == id && m.IsActive)
                .Select(m => m.UserId)
                .ToListAsync();

            if (!PmAccess.CanView(project, Uid, Role, team)) return (null, false, Denied());

            var canContribute = PmAccess.CanContribute(project, Uid, Role, team);
            if (needContribute && !canContribute) return (null, false, Denied());

            return (project, canContribute, null);
        }

        // ── query helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// The projects this user may see. Roles with portfolio-wide oversight get everything;
        /// everyone else gets the projects they own, sponsor, created, or sit on the team of.
        /// Doing this in the query rather than per row keeps the register to one round trip.
        /// </summary>
        private IQueryable<Project> Visible()
        {
            IQueryable<Project> projects = _db.Projects.AsNoTracking();

            if (CanSeePortfolio()) return projects;

            var uid = Uid;
            return projects.Where(p =>
                p.ProjectManagerId == uid || p.SponsorId == uid || p.CreatedById == uid ||
                p.TeamMembers.Any(m => m.UserId == uid));
        }

        private static IQueryable<Project> Filtered(IQueryable<Project> projects, ProjectFilter filter, DateTime today)
        {
            if (!string.IsNullOrWhiteSpace(filter.Q))
            {
                var text = filter.Q.Trim();
                projects = projects.Where(p =>
                    p.Name.Contains(text) || p.Code.Contains(text) ||
                    (p.Client != null && p.Client.Contains(text)) ||
                    (p.Description != null && p.Description.Contains(text)));
            }

            if (filter.Status.HasValue)
                projects = projects.Where(p => p.Status == filter.Status.Value);

            if (filter.Category.HasValue)
                projects = projects.Where(p => p.Category == filter.Category.Value);

            if (filter.Health.HasValue)
                projects = projects.Where(p => p.Health == filter.Health.Value);

            if (!string.IsNullOrWhiteSpace(filter.Manager))
                projects = projects.Where(p => p.ProjectManager != null &&
                                               p.ProjectManager.FirstName + " " +
                                               p.ProjectManager.LastName == filter.Manager);

            if (filter.Overdue)
                projects = projects.Where(p =>
                    p.EndDate != null && p.EndDate < today &&
                    p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled &&
                    p.Status != ProjectStatus.Archived);

            return projects;
        }

        /// <summary>
        /// Roles with sight of the whole portfolio rather than just their own projects. Kept beside
        /// <see cref="Visible"/>, which grants the same set everything, so the two cannot drift.
        /// </summary>
        private bool CanSeePortfolio() =>
            Roles.IsFullAccess(Role) ||
            Role is Roles.GeneralManager or Roles.Finance or Roles.Procurement
                or Roles.Auditor or Roles.DepartmentManager or Roles.ProjectManager;

        /// <summary>Who may start a project. Team membership does not confer this.</summary>
        private bool CanCreate() =>
            Roles.IsFullAccess(Role) ||
            Role is Roles.GeneralManager or Roles.ProjectManager or Roles.DepartmentManager;
    }
}
