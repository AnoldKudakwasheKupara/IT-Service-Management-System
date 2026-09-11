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
