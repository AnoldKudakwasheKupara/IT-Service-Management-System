using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models.Pm;
using IT_Service_Management_System.Services.Pm;
using IT_Service_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// The resource pool — people, equipment, vehicles, licences and rooms — together with
    /// capacity planning, project assignments and the leave/maintenance calendar.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager", "TeamLead",
                   "DepartmentManager", "HR", "Finance")]
    public class ProjectResourcesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectMetricsService _metrics;
        private readonly ProjectActivityService _activity;
        private readonly ProjectIntelligenceService _intelligence;

        public ProjectResourcesController(ApplicationDbContext db, ProjectMetricsService metrics,
            ProjectActivityService activity, ProjectIntelligenceService intelligence)
        {
            _db = db; _metrics = metrics; _activity = activity; _intelligence = intelligence;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool CanManage => Roles.IsPmManager(Role) || Role == Roles.HR;
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        // ════════════════════════════════════════════════════════════════════════
        //  Resource pool
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Index(ResourceType? type, ResourceStatus? status, int? departmentId, string? q)
        {
            IQueryable<Resource> query = _db.Resources.AsNoTracking()
                .Include(r => r.User).Include(r => r.Department);

            if (type.HasValue) query = query.Where(r => r.Type == type.Value);
            if (status.HasValue) query = query.Where(r => r.Status == status.Value);
            if (departmentId.HasValue) query = query.Where(r => r.DepartmentId == departmentId.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(r => r.Name.Contains(term)
                    || (r.Skills != null && r.Skills.Contains(term))
                    || (r.IdentifierCode != null && r.IdentifierCode.Contains(term)));
            }

            var resources = await query.OrderBy(r => r.Type).ThenBy(r => r.Name).ToListAsync();

            // Current utilisation for the next four weeks, so the list shows who is already busy.
            var workload = await _metrics.ResourceWorkloadAsync(DateTime.Today, DateTime.Today.AddDays(28), 500);
            ViewBag.Utilisation = workload.ToDictionary(w => w.ResourceId, w => w.UtilisationPercent);

            ViewBag.Type = type; ViewBag.Status = status; ViewBag.DepartmentId = departmentId; ViewBag.Q = q;
            ViewBag.CanManage = CanManage;
            ViewBag.TotalPeople = resources.Count(r => r.Type == ResourceType.Person);
            ViewBag.TotalEquipment = resources.Count(r => r.Type != ResourceType.Person);
            ViewBag.Overallocated = workload.Count(w => w.IsOverallocated);
            await PopulateListsAsync();

            return View(resources);
        }

        public async Task<IActionResult> Create()
        {
            if (!CanManage) return AccessDenied();
            await PopulateListsAsync();
            return View("Form", new Resource());
        }

        [HttpPost]
        public async Task<IActionResult> Create(Resource input)
        {
            if (!CanManage) return AccessDenied();
            if (!ModelState.IsValid) { await PopulateListsAsync(); return View("Form", input); }

            // A person resource should mirror the user account, so figures roll up per employee.
            if (input.Type == ResourceType.Person && input.UserId is int userId)
            {
                var duplicate = await _db.Resources.AnyAsync(r => r.UserId == userId);
                if (duplicate)
                {
                    ModelState.AddModelError(nameof(input.UserId), "That employee already has a resource record.");
                    await PopulateListsAsync();
                    return View("Form", input);
                }
            }

            input.CreatedAt = DateTime.Now;
            _db.Resources.Add(input);
            await _db.SaveChangesAsync();

            _activity.Log(null, nameof(Resource), input.Id, "Created", input.Name);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Resource “{input.Name}” added to the pool.";
            return RedirectToAction(nameof(Details), new { id = input.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!CanManage) return AccessDenied();
            var resource = await _db.Resources.FindAsync(id);
            if (resource == null) return NotFound();

            await PopulateListsAsync();
            return View("Form", resource);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Resource input)
        {
            if (!CanManage) return AccessDenied();
            var resource = await _db.Resources.FindAsync(input.Id);
            if (resource == null) return NotFound();
            if (!ModelState.IsValid) { await PopulateListsAsync(); return View("Form", input); }

            resource.Name = input.Name;
            resource.Type = input.Type;
            resource.Status = input.Status;
            resource.UserId = input.UserId;
            resource.DepartmentId = input.DepartmentId;
            resource.Description = input.Description;
            resource.Skills = input.Skills;
            resource.HourlyRate = input.HourlyRate;
            resource.BillableRate = input.BillableRate;
            resource.WeeklyCapacityHours = input.WeeklyCapacityHours;
            resource.Location = input.Location;
            resource.IdentifierCode = input.IdentifierCode;
            resource.IsActive = input.IsActive;

            _activity.Log(null, nameof(Resource), resource.Id, "Updated", resource.Name);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Resource updated.";
            return RedirectToAction(nameof(Details), new { id = resource.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            var resource = await _db.Resources.AsNoTracking()
                .Include(r => r.User).Include(r => r.Department)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (resource == null) return NotFound();

            ViewBag.Assignments = await _db.ResourceAssignments.AsNoTracking()
                .Include(a => a.Project).Include(a => a.Task)
                .Where(a => a.ResourceId == id)
                .OrderByDescending(a => a.FromDate).ToListAsync();

            ViewBag.Unavailability = await _db.ResourceUnavailabilities.AsNoTracking()
                .Where(u => u.ResourceId == id).OrderByDescending(u => u.FromDate).ToListAsync();

            var workload = await _metrics.ResourceWorkloadAsync(DateTime.Today, DateTime.Today.AddDays(28), 500);
            ViewBag.Workload = workload.FirstOrDefault(w => w.ResourceId == id);

            // Hours actually booked by this person, when the resource maps to an employee.
            if (resource.UserId is int userId)
            {
                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                ViewBag.HoursThisMonth = await _db.TimeEntries
                    .Where(t => t.UserId == userId && t.WorkDate >= monthStart)
                    .SumAsync(t => (decimal?)(t.Hours - t.BreakHours)) ?? 0m;
                ViewBag.BillableThisMonth = await _db.TimeEntries
                    .Where(t => t.UserId == userId && t.WorkDate >= monthStart && t.IsBillable)
                    .SumAsync(t => (decimal?)(t.Hours - t.BreakHours)) ?? 0m;
            }

            ViewBag.CanManage = CanManage;
            ViewBag.Projects = await _db.Projects.AsNoTracking()
                .Where(p => p.Status != ProjectStatus.Archived && p.Status != ProjectStatus.Cancelled)
                .OrderBy(p => p.Name).Select(p => new { p.Id, Name = p.Code + " · " + p.Name }).ToListAsync();

            return View(resource);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Capacity planning
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The workload chart — who is committed to what over a window, and who is over capacity.
        /// </summary>
        public async Task<IActionResult> Capacity(DateTime? from, DateTime? to, ResourceType? type)
        {
            var start = from ?? DateTime.Today;
            var end = to ?? DateTime.Today.AddDays(28);
            if (end < start) end = start.AddDays(28);

            var workload = await _metrics.ResourceWorkloadAsync(start, end, 200);

            if (type.HasValue)
            {
                var matching = await _db.Resources.AsNoTracking()
                    .Where(r => r.Type == type.Value).Select(r => r.Id).ToListAsync();
                workload = workload.Where(w => matching.Contains(w.ResourceId)).ToList();
            }

            ViewBag.From = start; ViewBag.To = end; ViewBag.Type = type;
            ViewBag.Overallocated = workload.Count(w => w.IsOverallocated);
            ViewBag.Underused = workload.Count(w => w.UtilisationPercent < 50);
            ViewBag.AverageUtilisation = workload.Count == 0 ? 0 : (int)Math.Round(workload.Average(w => (double)w.UtilisationPercent));
            ViewBag.TotalCapacity = workload.Sum(w => w.CapacityHours);
            ViewBag.TotalAllocated = workload.Sum(w => w.AllocatedHours);

            // Leave and maintenance windows overlapping the period, so gaps are explicable.
            ViewBag.Unavailability = await _db.ResourceUnavailabilities.AsNoTracking()
                .Include(u => u.Resource)
                .Where(u => u.FromDate <= end && u.ToDate >= start)
                .OrderBy(u => u.FromDate).ToListAsync();

            return View(workload);
        }

        /// <summary>Suggested owners for a piece of work, ranked by skills and spare capacity.</summary>
        public async Task<IActionResult> Suggest(int projectId, string? skills)
        {
            var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return NotFound();

            ViewBag.Project = project;
            ViewBag.Skills = skills;
            return View(await _intelligence.SuggestAssigneesAsync(projectId, skills, 12));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Assignments
        // ════════════════════════════════════════════════════════════════════════

        [HttpPost]
        public async Task<IActionResult> Assign(ResourceAssignment input)
        {
            if (!CanManage) return AccessDenied();

            if (input.ToDate < input.FromDate)
            {
                TempData["Error"] = "The assignment end date cannot be before its start date.";
                return RedirectToAction(nameof(Details), new { id = input.ResourceId });
            }

            // Warn — but do not block — when the booking pushes the resource over capacity: a
            // deliberate short-term overload is sometimes the right call.
            var existing = await _db.ResourceAssignments
                .Where(a => a.ResourceId == input.ResourceId && a.FromDate <= input.ToDate && a.ToDate >= input.FromDate)
                .SumAsync(a => (int?)a.AllocationPercent) ?? 0;

            input.CreatedAt = DateTime.Now;
            _db.ResourceAssignments.Add(input);
            _activity.Log(input.ProjectId, nameof(ResourceAssignment), input.ResourceId, "Assigned",
                $"{input.AllocationPercent}% from {input.FromDate:d} to {input.ToDate:d}");
            await _db.SaveChangesAsync();

            TempData[existing + input.AllocationPercent > 100 ? "Error" : "Success"] =
                existing + input.AllocationPercent > 100
                    ? $"Assigned — but this resource is now booked at {existing + input.AllocationPercent}% over that period."
                    : "Resource assigned.";

            return RedirectToAction(nameof(Details), new { id = input.ResourceId });
        }

        [HttpPost]
        public async Task<IActionResult> Unassign(int id, int assignmentId)
        {
            if (!CanManage) return AccessDenied();

            var assignment = await _db.ResourceAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId && a.ResourceId == id);
            if (assignment != null)
            {
                _db.ResourceAssignments.Remove(assignment);
                _activity.Log(assignment.ProjectId, nameof(ResourceAssignment), id, "Unassigned", null);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Assignment removed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Leave / maintenance windows
        // ════════════════════════════════════════════════════════════════════════

        [HttpPost]
        public async Task<IActionResult> AddUnavailability(ResourceUnavailability input)
        {
            if (!CanManage) return AccessDenied();

            if (input.ToDate < input.FromDate)
            {
                TempData["Error"] = "The end date cannot be before the start date.";
                return RedirectToAction(nameof(Details), new { id = input.ResourceId });
            }

            _db.ResourceUnavailabilities.Add(input);
            _activity.Log(null, nameof(ResourceUnavailability), input.ResourceId, "Created",
                $"{input.Reason} · {input.FromDate:d}–{input.ToDate:d}");
            await _db.SaveChangesAsync();

            TempData["Success"] = "Unavailability recorded — capacity has been reduced for that window.";
            return RedirectToAction(nameof(Details), new { id = input.ResourceId });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveUnavailability(int id, int entryId)
        {
            if (!CanManage) return AccessDenied();

            var entry = await _db.ResourceUnavailabilities.FirstOrDefaultAsync(u => u.Id == entryId && u.ResourceId == id);
            if (entry != null) { _db.ResourceUnavailabilities.Remove(entry); await _db.SaveChangesAsync(); }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Delete
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Delete(int id)
        {
            if (!CanManage) return AccessDenied();

            var resource = await _db.Resources.AsNoTracking()
                .Include(r => r.Department).FirstOrDefaultAsync(r => r.Id == id);
            if (resource == null) return NotFound();

            var vm = new DeleteConfirmationVm
            {
                EntityName = "Resource",
                Icon = "fa-users-gear",
                RecordTitle = resource.Name,
                Reference = resource.IdentifierCode,
                Id = resource.Id,
                Controller = "ProjectResources",
                CancelAction = "Details"
            };
            vm.Add("Type", resource.Type.ToString());
            vm.Add("Status", resource.Status.ToString());
            vm.Add("Department", resource.Department?.Name);
            vm.Add("Capacity", $"{resource.WeeklyCapacityHours:N1} h/week");

            var assignments = await _db.ResourceAssignments.CountAsync(a => a.ResourceId == id);
            vm.Consequences.Add($"{assignments} project assignment(s), which will free capacity on those projects");
            vm.Consequences.Add("Any recorded leave or maintenance windows");
            vm.Consequences.Add("Recorded time entries are unaffected — they belong to the employee, not the resource record.");
            vm.Consequences.Add("Deactivating the resource instead keeps its history while removing it from planning.");

            return View("DeleteConfirm", vm);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!CanManage) return AccessDenied();

            var resource = await _db.Resources.FindAsync(id);
            if (resource == null) return NotFound();

            _db.Resources.Remove(resource);
            _activity.Log(null, nameof(Resource), id, "Deleted", resource.Name);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Resource removed from the pool.";
            return RedirectToAction(nameof(Index));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════════════

        private async Task PopulateListsAsync()
        {
            ViewBag.Departments = await _db.Departments.AsNoTracking()
                .OrderBy(d => d.Name).Select(d => new { d.Id, d.Name }).ToListAsync();

            ViewBag.Users = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive).OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();
        }
    }
}
