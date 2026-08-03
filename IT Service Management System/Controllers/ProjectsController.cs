using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Helpers.Pm;
using IT_Service_Management_System.Models.Pm;
using IT_Service_Management_System.Services.Pm;
using IT_Service_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Project portfolio management — the executive dashboard, the project register, and everything
    /// that hangs directly off a project record: its team, attachments, lifecycle, closure and the
    /// assistive insight panel.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager", "TeamLead",
                   "DepartmentManager", "Finance", "Procurement", "Auditor", "Employee", "HR")]
    public class ProjectsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectMetricsService _metrics;
        private readonly ProjectActivityService _activity;
        private readonly ProjectApprovalService _approvals;
        private readonly ProjectSchedulingService _scheduling;
        private readonly ProjectIntelligenceService _intelligence;
        private readonly PmFileService _files;

        public ProjectsController(ApplicationDbContext db, ProjectMetricsService metrics,
            ProjectActivityService activity, ProjectApprovalService approvals,
            ProjectSchedulingService scheduling, ProjectIntelligenceService intelligence,
            PmFileService files)
        {
            _db = db; _metrics = metrics; _activity = activity;
            _approvals = approvals; _scheduling = scheduling; _intelligence = intelligence; _files = files;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");

        // ════════════════════════════════════════════════════════════════════════
        //  Dashboard
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>The executive overview — headline figures, health board and portfolio charts.</summary>
        public async Task<IActionResult> Index()
        {
            ViewBag.CanCreate = Roles.IsPmManager(Role);
            return View(await _metrics.BuildDashboardAsync());
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Portfolio register
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>The full project register, filterable by status, health, category and owner.</summary>
        public async Task<IActionResult> Portfolio(ProjectStatus? status, ProjectCategory? category,
            ProjectHealth? health, int? departmentId, int? managerId, string? q, bool mine = false)
        {
            IQueryable<Project> query = _db.Projects.AsNoTracking()
                .Include(p => p.Department).Include(p => p.ProjectManager).Include(p => p.Sponsor);

            if (status.HasValue) query = query.Where(p => p.Status == status.Value);
            if (category.HasValue) query = query.Where(p => p.Category == category.Value);
            if (health.HasValue) query = query.Where(p => p.Health == health.Value);
            if (departmentId.HasValue) query = query.Where(p => p.DepartmentId == departmentId.Value);
            if (managerId.HasValue) query = query.Where(p => p.ProjectManagerId == managerId.Value);

            if (mine)
            {
                var myProjectIds = await _db.ProjectTeamMembers.AsNoTracking()
                    .Where(m => m.UserId == Uid && m.IsActive).Select(m => m.ProjectId).ToListAsync();
                query = query.Where(p => p.ProjectManagerId == Uid || p.SponsorId == Uid || myProjectIds.Contains(p.Id));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(p => p.Name.Contains(term) || p.Code.Contains(term)
                    || (p.Client != null && p.Client.Contains(term))
                    || (p.Description != null && p.Description.Contains(term)));
            }

            var projects = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            ViewBag.Spend = await _metrics.SpendByProjectAsync();
            ViewBag.Status = status; ViewBag.Category = category; ViewBag.Health = health;
            ViewBag.DepartmentId = departmentId; ViewBag.ManagerId = managerId; ViewBag.Q = q; ViewBag.Mine = mine;
            ViewBag.CanCreate = Roles.IsPmManager(Role);
            await PopulateListsAsync();

            return View(projects);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Create / edit
        // ════════════════════════════════════════════════════════════════════════

        [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager")]
        public async Task<IActionResult> Create()
        {
            await PopulateListsAsync();
            ViewBag.Templates = await _db.ProjectTemplates.AsNoTracking()
                .Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();

            return View("Form", new Project
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(90),
                ProjectManagerId = Uid,
                Status = ProjectStatus.Draft
            });
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager")]
        public async Task<IActionResult> Create(Project input, int? templateId)
        {
            if (input.EndDate.HasValue && input.StartDate.HasValue && input.EndDate < input.StartDate)
                ModelState.AddModelError(nameof(input.EndDate), "The end date cannot be before the start date.");

            if (!ModelState.IsValid)
            {
                await PopulateListsAsync();
                ViewBag.Templates = await _db.ProjectTemplates.AsNoTracking().Where(t => t.IsActive).ToListAsync();
                return View("Form", input);
            }

            input.Code = string.IsNullOrWhiteSpace(input.Code) ? await NextCodeAsync() : input.Code.Trim();
            input.CreatedById = Uid;
            input.CreatedAt = DateTime.Now;
            input.Status = ProjectStatus.Draft;
            input.ProgressPercent = 0;
            input.EstimatedDurationDays ??= input.StartDate.HasValue && input.EndDate.HasValue
                ? (int)(input.EndDate.Value - input.StartDate.Value).TotalDays
                : null;

            _db.Projects.Add(input);
            await _db.SaveChangesAsync();

            // The creator is always on the team, so the project never starts with nobody on it.
            _db.ProjectTeamMembers.Add(new ProjectTeamMember
            {
                ProjectId = input.Id,
                UserId = input.ProjectManagerId ?? Uid,
                Role = TeamRole.Manager
            });

            _activity.Log(input.Id, nameof(Project), input.Id, "Created", $"Project {input.Reference} created");
            await _db.SaveChangesAsync();

            if (templateId is int tid && tid > 0)
            {
                await _scheduling.ApplyTemplateAsync(input.Id, tid, Uid);
                TempData["Success"] = $"Project {input.Reference} created from a template — the plan has been pre-populated.";
            }
            else
            {
                TempData["Success"] = $"Project {input.Reference} created.";
            }

            return RedirectToAction(nameof(Details), new { id = input.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return AccessDenied();

            await PopulateListsAsync();
            return View("Form", project);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Project input)
        {
            var project = await _db.Projects.FindAsync(input.Id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return AccessDenied();

            if (input.EndDate.HasValue && input.StartDate.HasValue && input.EndDate < input.StartDate)
                ModelState.AddModelError(nameof(input.EndDate), "The end date cannot be before the start date.");

            if (!ModelState.IsValid) { await PopulateListsAsync(); return View("Form", input); }

            _activity.LogChange(project.Id, nameof(Project), project.Id, "Name", project.Name, input.Name);
            _activity.LogChange(project.Id, nameof(Project), project.Id, "Budget", project.Budget, input.Budget);
            _activity.LogChange(project.Id, nameof(Project), project.Id, "End date", project.EndDate?.ToString("d"), input.EndDate?.ToString("d"));
            _activity.LogChange(project.Id, nameof(Project), project.Id, "Project manager", project.ProjectManagerId, input.ProjectManagerId);

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
            project.EstimatedDurationDays = input.EstimatedDurationDays;
            project.Budget = input.Budget;
            project.Currency = input.Currency;
            project.Location = input.Location;
            project.Tags = input.Tags;
            project.HealthNote = input.HealthNote;
            project.AutoCalculateProgress = input.AutoCalculateProgress;
            if (!input.AutoCalculateProgress) project.ProgressPercent = input.ProgressPercent;
            project.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            await _metrics.RefreshProjectAsync(project.Id);

            TempData["Success"] = $"Project {project.Reference} updated.";
            return RedirectToAction(nameof(Details), new { id = project.Id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Details
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>The project workspace — status, plan summary, team, registers and activity.</summary>
        public async Task<IActionResult> Details(int id)
        {
            var project = await _db.Projects
                .Include(p => p.Department).Include(p => p.ProjectManager)
                .Include(p => p.Sponsor).Include(p => p.CreatedBy).Include(p => p.ApprovedBy)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound();

            var team = await _db.ProjectTeamMembers.AsNoTracking()
                .Include(m => m.User)
                .Where(m => m.ProjectId == id)
                .OrderBy(m => m.Role).ToListAsync();

            if (!PmAccess.CanView(project, Uid, Role, team.Select(t => t.UserId))) return AccessDenied();

            await LoadProjectContextAsync(project, team);

            ViewBag.Milestones = await _db.Milestones.AsNoTracking()
                .Where(m => m.ProjectId == id).OrderBy(m => m.DueDate).Take(8).ToListAsync();
            ViewBag.RecentTasks = await _db.ProjectTasks.AsNoTracking()
                .Include(t => t.AssignedTo)
                .Where(t => t.ProjectId == id)
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt).Take(8).ToListAsync();
            ViewBag.TopRisks = (await _db.ProjectRisks.AsNoTracking()
                .Include(r => r.Owner)
                .Where(r => r.ProjectId == id && r.Status != PmRiskStatus.Closed).ToListAsync())
                .OrderByDescending(r => r.Score).Take(5).ToList();
            ViewBag.OpenIssues = await _db.ProjectIssues.AsNoTracking()
                .Include(i => i.AssignedTo)
                .Where(i => i.ProjectId == id && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed)
                .OrderByDescending(i => i.Severity).Take(5).ToListAsync();
            ViewBag.Attachments = await _db.ProjectAttachments.AsNoTracking()
                .Include(a => a.UploadedBy).Where(a => a.ProjectId == id)
                .OrderByDescending(a => a.UploadedAt).ToListAsync();
            ViewBag.Activity = await _db.ProjectActivityLogs.AsNoTracking()
                .Include(l => l.User).Where(l => l.ProjectId == id)
                .OrderByDescending(l => l.At).Take(15).ToListAsync();
            ViewBag.Dependencies = await _db.ProjectLinks.AsNoTracking()
                .Include(l => l.DependsOnProject).Where(l => l.ProjectId == id).ToListAsync();

            ViewBag.LinkableProjects = await _db.Projects.AsNoTracking()
                .Where(p => p.Id != id).OrderBy(p => p.Name)
                .Select(p => new { p.Id, Label = p.Code + " · " + p.Name }).ToListAsync();

            await PopulateListsAsync();
            return View(project);
        }

        /// <summary>Loads the figures and permission flags every project sub-page needs.</summary>
        private async Task LoadProjectContextAsync(Project project, List<ProjectTeamMember> team)
        {
            var teamIds = team.Select(t => t.UserId).ToList();

            ViewBag.Project = project;
            ViewBag.Team = team;
            ViewBag.CanEdit = PmAccess.CanEdit(project, Uid, Role);
            ViewBag.CanContribute = PmAccess.CanContribute(project, Uid, Role, teamIds);
            ViewBag.CanApprove = PmAccess.CanApprove(Role);
            ViewBag.CanDelete = PmAccess.CanDelete(project, Uid, Role);

            ViewBag.Spent = await _metrics.ActualSpendAsync(project.Id);
            ViewBag.Committed = await _metrics.CommittedSpendAsync(project.Id);

            ViewBag.TaskTotal = await _db.ProjectTasks.CountAsync(t => t.ProjectId == project.Id);
            ViewBag.TaskDone = await _db.ProjectTasks.CountAsync(t => t.ProjectId == project.Id && t.Status == ProjectTaskStatus.Completed);
            ViewBag.TaskOverdue = await _db.ProjectTasks.CountAsync(t => t.ProjectId == project.Id
                && t.DueDate < DateTime.Today && t.Status != ProjectTaskStatus.Completed && t.Status != ProjectTaskStatus.Cancelled);
            ViewBag.RiskCount = await _db.ProjectRisks.CountAsync(r => r.ProjectId == project.Id && r.Status != PmRiskStatus.Closed);
            ViewBag.IssueCount = await _db.ProjectIssues.CountAsync(i => i.ProjectId == project.Id
                && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);
            ViewBag.MilestoneCount = await _db.Milestones.CountAsync(m => m.ProjectId == project.Id);
            ViewBag.DocumentCount = await _db.ProjectDocuments.CountAsync(d => d.ProjectId == project.Id);
            ViewBag.HoursLogged = await _db.TimeEntries.Where(t => t.ProjectId == project.Id)
                .SumAsync(t => (decimal?)(t.Hours - t.BreakHours)) ?? 0m;
        }

        /// <summary>Shared by every project sub-page so the header and tab strip render identically.</summary>
        internal static async Task<(Project? Project, List<ProjectTeamMember> Team)> LoadAsync(ApplicationDbContext db, int projectId)
        {
            var project = await db.Projects
                .Include(p => p.ProjectManager).Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return (null, new List<ProjectTeamMember>());

            var team = await db.ProjectTeamMembers.AsNoTracking()
                .Include(m => m.User).Where(m => m.ProjectId == projectId).ToListAsync();
            return (project, team);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ════════════════════════════════════════════════════════════════════════

        [HttpPost]
        public async Task<IActionResult> ChangeStatus(int id, ProjectStatus status)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role) && !PmAccess.CanApprove(Role)) return AccessDenied();

            // Approval and archival are executive decisions, not the project manager's.
            if (status is ProjectStatus.Approved or ProjectStatus.Archived && !PmAccess.CanApprove(Role))
            {
                TempData["Error"] = "Only an administrator or executive can approve or archive a project.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var previous = project.Status;
            project.Status = status;
            project.UpdatedAt = DateTime.Now;

            if (status == ProjectStatus.Active && project.ActualStartDate == null)
                project.ActualStartDate = DateTime.Today;
            if (status == ProjectStatus.Completed)
            {
                project.ActualEndDate ??= DateTime.Today;
                project.ProgressPercent = 100;
                project.AutoCalculateProgress = false;
            }
            if (status == ProjectStatus.Approved)
            {
                project.ApprovedAt = DateTime.Now;
                project.ApprovedById = Uid;
                // Freeze the plan at approval so slippage is measured against what was committed.
                project.BaselineEndDate = project.EndDate;
            }

            _activity.LogChange(id, nameof(Project), id, "Status", previous, status);
            await _db.SaveChangesAsync();

            if (status == ProjectStatus.Approved) await _scheduling.SetBaselineAsync(id);

            _activity.NotifyMany(await _activity.ProjectAudienceAsync(id), PmNotificationType.StatusChanged,
                $"{project.Reference} is now {status}", $"{project.Name} moved from {previous} to {status}.",
                Url.Action(nameof(Details), "Projects", new { id }), id);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Project status set to {status}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>Send the project for executive approval.</summary>
        [HttpPost]
        public async Task<IActionResult> SubmitForApproval(int id)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return AccessDenied();

            var steps = await _approvals.RequestAsync(ApprovalSubject.Project, id,
                $"{project.Reference} — {project.Name}", id, Uid, project.TotalBudget);

            if (steps == 0)
            {
                TempData["Error"] = "No approver could be found. Check that an executive or administrator account is active.";
                return RedirectToAction(nameof(Details), new { id });
            }

            project.Status = ProjectStatus.Planning;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Sent for approval — {steps} approval step(s) raised.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Team
        // ════════════════════════════════════════════════════════════════════════

        [HttpPost]
        public async Task<IActionResult> AddTeamMember(int id, int userId, TeamRole role, int allocationPercent = 100)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return AccessDenied();

            var existing = await _db.ProjectTeamMembers.FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == userId);
            if (existing != null)
            {
                // Re-adding somebody who rolled off reactivates them rather than duplicating the row.
                existing.IsActive = true;
                existing.Role = role;
                existing.AllocationPercent = Math.Clamp(allocationPercent, 0, 100);
            }
            else
            {
                _db.ProjectTeamMembers.Add(new ProjectTeamMember
                {
                    ProjectId = id,
                    UserId = userId,
                    Role = role,
                    AllocationPercent = Math.Clamp(allocationPercent, 0, 100)
                });
                _activity.Notify(userId, PmNotificationType.StatusChanged,
                    $"You have been added to {project.Name}", $"Role: {role}",
                    Url.Action(nameof(Details), "Projects", new { id }), id);
            }

            _activity.Log(id, nameof(ProjectTeamMember), userId, "TeamMemberAdded", $"Added as {role}");
            await _db.SaveChangesAsync();

            TempData["Success"] = "Team member added.";
            return RedirectToAction(nameof(Details), new { id, tab = "team" });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveTeamMember(int id, int memberId)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return AccessDenied();

            var member = await _db.ProjectTeamMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.ProjectId == id);
            if (member != null)
            {
                // Deactivate rather than delete — their time entries must stay attributable.
                member.IsActive = false;
                member.ToDate = DateTime.Today;
                _activity.Log(id, nameof(ProjectTeamMember), member.UserId, "TeamMemberRemoved", "Rolled off the project");
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Team member rolled off.";
            return RedirectToAction(nameof(Details), new { id, tab = "team" });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Dependencies & attachments
        // ════════════════════════════════════════════════════════════════════════

        [HttpPost]
        public async Task<IActionResult> AddDependency(int id, int dependsOnProjectId, DependencyType type, string? note)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return AccessDenied();

            if (dependsOnProjectId == id)
            {
                TempData["Error"] = "A project cannot depend on itself.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var duplicate = await _db.ProjectLinks.AnyAsync(l => l.ProjectId == id && l.DependsOnProjectId == dependsOnProjectId);
            if (!duplicate)
            {
                _db.ProjectLinks.Add(new ProjectLink { ProjectId = id, DependsOnProjectId = dependsOnProjectId, Type = type, Note = note });
                _activity.Log(id, nameof(ProjectLink), dependsOnProjectId, "DependencyAdded", note);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Dependency recorded.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveDependency(int id, int linkId)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return AccessDenied();

            var link = await _db.ProjectLinks.FirstOrDefaultAsync(l => l.Id == linkId && l.ProjectId == id);
            if (link != null) { _db.ProjectLinks.Remove(link); await _db.SaveChangesAsync(); }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> UploadAttachment(int id, IFormFile? file)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();

            var team = await _db.ProjectTeamMembers.Where(m => m.ProjectId == id).Select(m => m.UserId).ToListAsync();
            if (!PmAccess.CanContribute(project, Uid, Role, team)) return AccessDenied();

            var saved = await _files.SaveAsync(file, "projects", id);
            if (saved == null)
            {
                TempData["Error"] = _files.LastError ?? "The file could not be attached.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _db.ProjectAttachments.Add(new ProjectAttachment
            {
                ProjectId = id,
                FileName = saved.OriginalName,
                StoredPath = saved.RelativePath,
                ContentType = saved.ContentType,
                SizeBytes = saved.Size,
                UploadedById = Uid
            });
            _activity.Log(id, nameof(ProjectAttachment), null, "AttachmentUploaded", saved.OriginalName);
            await _db.SaveChangesAsync();

            TempData["Success"] = "File attached.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAttachment(int id, int attachmentId)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return AccessDenied();

            var attachment = await _db.ProjectAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.ProjectId == id);
            if (attachment != null)
            {
                _files.Delete(attachment.StoredPath);
                _db.ProjectAttachments.Remove(attachment);
                _activity.Log(id, nameof(ProjectAttachment), attachmentId, "AttachmentDeleted", attachment.FileName);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Insights (derived from the organisation's own data — no external model)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Forecasts, predicted risks and suggested priorities for one project.</summary>
        public async Task<IActionResult> Insights(int id)
        {
            var (project, team) = await LoadAsync(_db, id);
            if (project == null) return NotFound();
            if (!PmAccess.CanView(project, Uid, Role, team.Select(t => t.UserId))) return AccessDenied();

            await LoadProjectContextAsync(project, team);

            ViewBag.ScheduleForecast = await _intelligence.ForecastCompletionAsync(id);
            ViewBag.BudgetForecast = await _intelligence.ForecastBudgetAsync(id);
            ViewBag.PredictedRisks = await _intelligence.PredictRisksAsync(id);
            ViewBag.Priorities = await _intelligence.PrioritiseTasksAsync(id);
            ViewBag.Allocation = await _intelligence.SuggestAssigneesAsync(id, null);
            ViewBag.Summary = await _intelligence.ExecutiveSummaryAsync(id);

            return View(project);
        }

        /// <summary>Plain-English portfolio search — "projects delayed by more than two weeks".</summary>
        public async Task<IActionResult> Search(string? q)
        {
            var result = await _intelligence.SearchAsync(q);
            ViewBag.Spend = await _metrics.SpendByProjectAsync();
            return View(result);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Closure
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>The closure checklist: deliverables, acceptance, finances, lessons learned.</summary>
        public async Task<IActionResult> Closure(int id)
        {
            var (project, team) = await LoadAsync(_db, id);
            if (project == null) return NotFound();
            if (!PmAccess.CanView(project, Uid, Role, team.Select(t => t.UserId))) return AccessDenied();

            await LoadProjectContextAsync(project, team);

            var closure = await _db.ProjectClosures.FirstOrDefaultAsync(c => c.ProjectId == id);
            if (closure == null)
            {
                closure = new ProjectClosure
                {
                    ProjectId = id,
                    FinalBudget = project.TotalBudget,
                    FinalActualSpend = await _metrics.ActualSpendAsync(id),
                    FinalActualHours = await _db.TimeEntries.Where(t => t.ProjectId == id)
                        .SumAsync(t => (decimal?)(t.Hours - t.BreakHours)) ?? 0m
                };
                _db.ProjectClosures.Add(closure);
                await _db.SaveChangesAsync();
            }

            ViewBag.Deliverables = await _db.Deliverables.AsNoTracking()
                .Include(d => d.AcceptedBy)
                .Where(d => d.ProjectId == id).OrderBy(d => d.DueDate).ToListAsync();
            ViewBag.Lessons = await _db.LessonsLearned.AsNoTracking()
                .Include(l => l.RaisedBy).Where(l => l.ProjectId == id)
                .OrderBy(l => l.Category).ToListAsync();
            ViewBag.OutstandingIssues = await _db.ProjectIssues.AsNoTracking()
                .Where(i => i.ProjectId == id && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed)
                .ToListAsync();
            ViewBag.OutstandingAssets = await _db.ProjectAssets.AsNoTracking()
                .Include(a => a.IssuedTo)
                .Where(a => a.ProjectId == id && a.ReturnedDate == null).ToListAsync();

            return View(closure);
        }

        [HttpPost]
        public async Task<IActionResult> SaveClosure(ProjectClosure input)
        {
            var project = await _db.Projects.FindAsync(input.ProjectId);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role)) return AccessDenied();

            var closure = await _db.ProjectClosures.FirstOrDefaultAsync(c => c.ProjectId == input.ProjectId);
            if (closure == null) return NotFound();

            closure.OutstandingIssues = input.OutstandingIssues;
            closure.PostImplementationReview = input.PostImplementationReview;
            closure.ClientAccepted = input.ClientAccepted;
            closure.ClientAcceptedBy = input.ClientAcceptedBy;
            closure.ClientAcceptedDate = input.ClientAcceptedDate;
            closure.ClientAcceptanceNotes = input.ClientAcceptanceNotes;
            closure.DeliverablesSignedOff = input.DeliverablesSignedOff;
            closure.ResourcesReleased = input.ResourcesReleased;
            closure.AssetsReturned = input.AssetsReturned;
            closure.DocumentationArchived = input.DocumentationArchived;
            closure.FinancesReconciled = input.FinancesReconciled;

            // Refresh the financial snapshot every save so the summary reflects the latest position.
            closure.FinalBudget = project.TotalBudget;
            closure.FinalActualSpend = await _metrics.ActualSpendAsync(project.Id);
            closure.FinalActualHours = await _db.TimeEntries.Where(t => t.ProjectId == project.Id)
                .SumAsync(t => (decimal?)(t.Hours - t.BreakHours)) ?? 0m;
            closure.Status = closure.ChecklistCompletePercent >= 100 ? ClosureStatus.AwaitingAcceptance : ClosureStatus.InProgress;

            _activity.Log(project.Id, nameof(ProjectClosure), closure.Id, "Updated", "Closure checklist updated");
            await _db.SaveChangesAsync();

            TempData["Success"] = "Closure record saved.";
            return RedirectToAction(nameof(Closure), new { id = project.Id });
        }

        /// <summary>Final close-out. Requires the checklist to be complete and every issue settled.</summary>
        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager")]
        public async Task<IActionResult> CloseProject(int id)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();
            if (!PmAccess.CanEdit(project, Uid, Role) && !PmAccess.CanApprove(Role)) return AccessDenied();

            var closure = await _db.ProjectClosures.FirstOrDefaultAsync(c => c.ProjectId == id);
            if (closure == null || closure.ChecklistCompletePercent < 100)
            {
                TempData["Error"] = "Every item on the closure checklist must be ticked before the project can be closed.";
                return RedirectToAction(nameof(Closure), new { id });
            }

            var openIssues = await _db.ProjectIssues.CountAsync(i => i.ProjectId == id
                && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);
            if (openIssues > 0)
            {
                TempData["Error"] = $"{openIssues} issue(s) are still open. Resolve or defer them before closing.";
                return RedirectToAction(nameof(Closure), new { id });
            }

            closure.Status = ClosureStatus.Closed;
            closure.ClosedById = Uid;
            closure.ClosedAt = DateTime.Now;

            project.Status = ProjectStatus.Completed;
            project.ProgressPercent = 100;
            project.AutoCalculateProgress = false;
            project.ActualEndDate ??= DateTime.Today;
            project.UpdatedAt = DateTime.Now;

            // Release the team and any resource bookings that run past today.
            foreach (var member in await _db.ProjectTeamMembers.Where(m => m.ProjectId == id && m.IsActive).ToListAsync())
            {
                member.IsActive = false;
                member.ToDate = DateTime.Today;
            }
            foreach (var assignment in await _db.ResourceAssignments.Where(a => a.ProjectId == id && a.ToDate > DateTime.Today).ToListAsync())
                assignment.ToDate = DateTime.Today;

            _activity.Log(id, nameof(Project), id, "Closed", $"Project {project.Reference} closed out");
            _activity.NotifyMany(await _activity.ProjectAudienceAsync(id), PmNotificationType.StatusChanged,
                $"{project.Reference} has been closed", project.Name,
                Url.Action(nameof(Details), "Projects", new { id }), id);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Project {project.Reference} closed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> AddLesson(int id, LessonLearned input)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();

            var team = await _db.ProjectTeamMembers.Where(m => m.ProjectId == id).Select(m => m.UserId).ToListAsync();
            if (!PmAccess.CanContribute(project, Uid, Role, team)) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Description))
            {
                TempData["Error"] = "A lesson needs both a title and a description.";
                return RedirectToAction(nameof(Closure), new { id });
            }

            input.ProjectId = id;
            input.RaisedById = Uid;
            input.CreatedAt = DateTime.Now;
            _db.LessonsLearned.Add(input);
            _activity.Log(id, nameof(LessonLearned), null, "Created", input.Title);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Lesson recorded.";
            return RedirectToAction(nameof(Closure), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Audit trail & delete
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>The full audit trail for a project — who changed what, when, and from where.</summary>
        public async Task<IActionResult> Activity(int id, int page = 1)
        {
            var (project, team) = await LoadAsync(_db, id);
            if (project == null) return NotFound();
            if (!PmAccess.CanView(project, Uid, Role, team.Select(t => t.UserId))) return AccessDenied();

            await LoadProjectContextAsync(project, team);

            const int pageSize = 50;
            var query = _db.ProjectActivityLogs.AsNoTracking()
                .Include(l => l.User).Where(l => l.ProjectId == id).OrderByDescending(l => l.At);

            ViewBag.Total = await query.CountAsync();
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;

            return View(await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
        }

        public async Task<IActionResult> Delete(int id)
        {
            var project = await _db.Projects
                .Include(p => p.ProjectManager).Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound();
            if (!PmAccess.CanDelete(project, Uid, Role)) return AccessDenied();

            var vm = new DeleteConfirmationVm
            {
                EntityName = "Project",
                Icon = "fa-diagram-project",
                RecordTitle = project.Name,
                Reference = project.Reference,
                Id = project.Id,
                Controller = "Projects",
                CancelAction = "Details"
            };
            vm.Add("Status", project.Status.ToString());
            vm.Add("Project manager", project.ProjectManager == null ? null : $"{project.ProjectManager.FirstName} {project.ProjectManager.LastName}");
            vm.Add("Department", project.Department?.Name);
            vm.Add("Budget", $"{project.Currency} {project.TotalBudget:N2}");
            vm.Add("Progress", $"{project.ProgressPercent}%");

            var taskCount = await _db.ProjectTasks.CountAsync(t => t.ProjectId == id);
            var riskCount = await _db.ProjectRisks.CountAsync(r => r.ProjectId == id);
            var docCount = await _db.ProjectDocuments.CountAsync(d => d.ProjectId == id);
            var hours = await _db.TimeEntries.Where(t => t.ProjectId == id).SumAsync(t => (decimal?)t.Hours) ?? 0m;

            vm.Consequences.Add($"{taskCount} task(s) and all their comments, checklists and attachments");
            vm.Consequences.Add($"{riskCount} risk register entr(ies) and every issue and change request");
            vm.Consequences.Add($"{docCount} project document(s) and all stored versions");
            vm.Consequences.Add($"{hours:N1} hour(s) of recorded time, and the project's budget and expense history");
            vm.Consequences.Add("The plan — phases, WBS, milestones and deliverables");
            vm.Consequences.Add("The project is soft-deleted: it disappears from the portfolio but the rows remain recoverable by an administrator.");

            return View("DeleteConfirm", vm);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();
            if (!PmAccess.CanDelete(project, Uid, Role)) return AccessDenied();

            // Soft delete — the global query filter hides it, but nothing is destroyed.
            project.IsDeleted = true;
            project.UpdatedAt = DateTime.Now;
            _activity.Log(null, nameof(Project), id, "Deleted", $"Project {project.Reference} — {project.Name}");
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Project {project.Reference} deleted.";
            return RedirectToAction(nameof(Portfolio));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════════════

        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        /// <summary>Next sequential project code for the current year, e.g. PRJ-2026-014.</summary>
        private async Task<string> NextCodeAsync()
        {
            var prefix = $"PRJ-{DateTime.Today.Year}-";
            var lastNumber = await _db.Projects.IgnoreQueryFilters()
                .Where(p => p.Code.StartsWith(prefix))
                .Select(p => p.Code)
                .ToListAsync();

            var next = lastNumber
                .Select(c => int.TryParse(c[prefix.Length..], out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;

            return $"{prefix}{next:D3}";
        }

        private async Task PopulateListsAsync()
        {
            ViewBag.Departments = new SelectList(
                await _db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(), "Id", "Name");

            ViewBag.Users = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName })
                .ToListAsync();
        }
    }
}
