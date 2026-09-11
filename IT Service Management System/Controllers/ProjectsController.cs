using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Helpers.Pm;
using IT_Service_Management_System.Models.Pm;
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

        public ProjectsController(ApplicationDbContext db) => _db = db;

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
                CanApprove = PmAccess.CanApprove(Role)
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

        // ── query helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// The projects this user may see. Roles with portfolio-wide oversight get everything;
        /// everyone else gets the projects they own, sponsor, created, or sit on the team of.
        /// Doing this in the query rather than per row keeps the register to one round trip.
        /// </summary>
        private IQueryable<Project> Visible()
        {
            IQueryable<Project> projects = _db.Projects.AsNoTracking();

            if (Roles.IsFullAccess(Role)) return projects;

            if (Role is Roles.GeneralManager or Roles.Finance or Roles.Procurement
                or Roles.Auditor or Roles.DepartmentManager or Roles.ProjectManager)
                return projects;

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

        /// <summary>Who may start a project. Team membership does not confer this.</summary>
        private bool CanCreate() =>
            Roles.IsFullAccess(Role) ||
            Role is Roles.GeneralManager or Roles.ProjectManager or Roles.DepartmentManager;
    }
}
