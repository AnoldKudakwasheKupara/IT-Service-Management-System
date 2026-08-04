using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models.Hr;
using IT_Service_Management_System.Services;
using IT_Service_Management_System.Services.Hr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Onboarding — what has to happen when someone joins, and proof that it did.
    /// <para>
    /// Some of it is policy and some of it is law. Registration with NSSA, written particulars of
    /// employment, the code of conduct, and a safety induction are not optional, so those steps are
    /// marked statutory, cannot be closed without evidence, and are reported separately from an
    /// unissued laptop.
    /// </para>
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "HR", "GeneralManager", "DepartmentManager",
                   "ProjectManager", "TeamLead", "Finance", "Employee", "SupportAgent",
                   "Development", "QualityManager")]
    public class OnboardingController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly OnboardingService _onboarding;
        private readonly AuditService _audit;

        public OnboardingController(ApplicationDbContext db, OnboardingService onboarding, AuditService audit)
        {
            _db = db; _onboarding = onboarding; _audit = audit;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool IsHr => Roles.IsFullAccess(Role) || Role == Roles.HR;
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        private async Task<Employee?> MeAsync() =>
            await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == Uid);

        // ════════════════════════════════════════════════════════════════════════
        //  Overview
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Index(bool includeComplete = false)
        {
            if (!IsHr)
            {
                var me = await MeAsync();
                if (me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");
                return RedirectToAction(nameof(Mine));
            }

            var query = _db.OnboardingProgrammes.AsNoTracking()
                .Include(p => p.Employee).Include(p => p.Tasks)
                .AsQueryable();

            if (!includeComplete)
                query = query.Where(p => p.Status == OnboardingStatus.NotStarted
                                      || p.Status == OnboardingStatus.InProgress);

            ViewBag.Programmes = await query
                .OrderBy(p => p.Status).ThenBy(p => p.StartDate).ToListAsync();

            ViewBag.Overview = await _onboarding.OverviewAsync();
            ViewBag.IncludeComplete = includeComplete;

            // Anyone hired recently with no programme at all — the gap that matters most.
            var withProgramme = await _db.OnboardingProgrammes.AsNoTracking()
                .Select(p => p.EmployeeId).ToListAsync();

            ViewBag.Unstarted = await _db.Employees.AsNoTracking()
                .Where(e => !e.IsDeleted
                         && e.HireDate != null
                         && e.HireDate >= DateTime.Today.AddDays(-90)
                         && !withProgramme.Contains(e.Id))
                .OrderByDescending(e => e.HireDate)
                .ToListAsync();

            return View();
        }

        /// <summary>The joiner's own view of their programme.</summary>
        public async Task<IActionResult> Mine()
        {
            var me = await MeAsync();
            if (me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");

            var programme = await _db.OnboardingProgrammes.AsNoTracking()
                .Include(p => p.Tasks).Include(p => p.Buddy)
                .Where(p => p.EmployeeId == me.Id)
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();

            ViewBag.Employee = me;
            ViewBag.Programme = programme;
            return View();
        }

        public async Task<IActionResult> Details(int id)
        {
            var p = await _db.OnboardingProgrammes.AsNoTracking()
                .Include(x => x.Employee).Include(x => x.Buddy).Include(x => x.Template)
                .Include(x => x.Tasks).ThenInclude(t => t.CompletedBy)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null) return NotFound();

            if (!IsHr)
            {
                var me = await MeAsync();
                var managerId = await _db.Employees.AsNoTracking()
                    .Where(e => e.Id == p.EmployeeId).Select(e => e.ManagerId).FirstOrDefaultAsync();

                if (me == null || (me.Id != p.EmployeeId && me.Id != managerId && me.Id != p.BuddyId))
                    return AccessDenied();
            }

            ViewBag.Programme = p;
            ViewBag.IsHr = IsHr;
            return View();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Running a programme
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Start(int? employeeId)
        {
            if (!IsHr) return AccessDenied();

            var people = await _db.Employees.AsNoTracking()
                .Where(e => !e.IsDeleted)
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .Select(e => new { e.Id, Label = e.FirstName + " " + e.LastName + " · " + e.EmployeeNumber })
                .ToListAsync();

            ViewBag.EmployeeList = new SelectList(people, "Id", "Label", employeeId);
            ViewBag.BuddyList = new SelectList(people, "Id", "Label");

            ViewBag.TemplateList = new SelectList(
                await _db.OnboardingTemplates.AsNoTracking().Where(t => t.IsActive)
                    .OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name)
                    .Select(t => new { t.Id, t.Name }).ToListAsync(),
                "Id", "Name");

            ViewBag.EmployeeId = employeeId;

            if (employeeId.HasValue)
                ViewBag.Employee = await _db.Employees.AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == employeeId.Value);

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Start(int employeeId, int? templateId, DateTime? startDate, int? buddyId)
        {
            if (!IsHr) return AccessDenied();

            var result = await _onboarding.StartAsync(employeeId, templateId, startDate, buddyId);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;

            if (!result.Succeeded) return RedirectToAction(nameof(Start), new { employeeId });

            await _audit.LogAsync("Started", nameof(OnboardingProgramme), result.ProgrammeId,
                $"Onboarding started for employee {employeeId}");

            return RedirectToAction(nameof(Details), new { id = result.ProgrammeId });
        }

        [HttpPost]
        public async Task<IActionResult> Complete(int id, string? evidence)
        {
            var task = await _db.OnboardingTasks.AsNoTracking()
                .Include(t => t.Programme)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null) return NotFound();

            // The joiner may close their own steps; everything else is HR's or the manager's.
            if (!IsHr)
            {
                var me = await MeAsync();
                var isOwnStep = me != null && me.Id == task.Programme?.EmployeeId
                             && task.Owner == OnboardingOwner.Employee;

                var managerId = await _db.Employees.AsNoTracking()
                    .Where(e => e.Id == task.Programme!.EmployeeId)
                    .Select(e => e.ManagerId).FirstOrDefaultAsync();

                if (!isOwnStep && (me == null || me.Id != managerId)) return AccessDenied();
            }

            var result = await _onboarding.CompleteAsync(id, evidence, Uid);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;

            return RedirectToAction(nameof(Details), new { id = task.ProgrammeId });
        }

        [HttpPost]
        public async Task<IActionResult> Reopen(int id, string reason)
        {
            if (!IsHr) return AccessDenied();

            var task = await _db.OnboardingTasks.FindAsync(id);
            if (task == null) return NotFound();

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Say why the step is being reopened — the record already says it was done.";
                return RedirectToAction(nameof(Details), new { id = task.ProgrammeId });
            }

            task.IsComplete = false;
            task.CompletedAt = null;
            task.CompletedById = null;
            task.Evidence = $"{task.Evidence}\nReopened {DateTime.Today:d MMM yyyy}: {reason}".Trim();

            var programme = await _db.OnboardingProgrammes.FindAsync(task.ProgrammeId);
            if (programme is { Status: OnboardingStatus.Complete })
            {
                programme.Status = OnboardingStatus.InProgress;
                programme.CompletedAt = null;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Step reopened.";
            return RedirectToAction(nameof(Details), new { id = task.ProgrammeId });
        }

        [HttpPost]
        public async Task<IActionResult> AddTask(int programmeId, string title, string? detail,
            OnboardingCategory category, OnboardingOwner owner, DateTime dueDate)
        {
            if (!IsHr) return AccessDenied();

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] = "Name the step.";
                return RedirectToAction(nameof(Details), new { id = programmeId });
            }

            var next = await _db.OnboardingTasks.Where(t => t.ProgrammeId == programmeId)
                .MaxAsync(t => (int?)t.DisplayOrder) ?? 0;

            _db.OnboardingTasks.Add(new OnboardingTask
            {
                ProgrammeId = programmeId,
                Title = title.Trim(),
                Detail = detail,
                Category = category,
                Owner = owner,
                DueDate = dueDate,
                DisplayOrder = next + 1
            });

            await _db.SaveChangesAsync();
            TempData["Success"] = "Step added.";
            return RedirectToAction(nameof(Details), new { id = programmeId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Templates
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Templates()
        {
            if (!IsHr) return AccessDenied();

            ViewBag.Templates = await _db.OnboardingTemplates.AsNoTracking()
                .Include(t => t.Tasks)
                .OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name)
                .ToListAsync();

            ViewBag.UseCounts = await _db.OnboardingProgrammes.AsNoTracking()
                .Where(p => p.TemplateId != null)
                .GroupBy(p => p.TemplateId!.Value)
                .Select(g => new { TemplateId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TemplateId, x => x.Count);

            return View();
        }

        public async Task<IActionResult> Template(int id)
        {
            if (!IsHr) return AccessDenied();

            var t = await _db.OnboardingTemplates.AsNoTracking()
                .Include(x => x.Tasks)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (t == null) return NotFound();

            ViewBag.Template = t;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddTemplateTask(int templateId, string title, string? detail,
            OnboardingCategory category, OnboardingOwner owner, int dueDayOffset,
            bool isStatutory = false, string? authority = null)
        {
            if (!IsHr) return AccessDenied();

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] = "Name the step.";
                return RedirectToAction(nameof(Template), new { id = templateId });
            }

            if (isStatutory && string.IsNullOrWhiteSpace(authority))
            {
                TempData["Error"] = "Name the provision a statutory step comes from, so the requirement "
                                  + "can be checked rather than taken on trust.";
                return RedirectToAction(nameof(Template), new { id = templateId });
            }

            var next = await _db.OnboardingTaskTemplates.Where(t => t.TemplateId == templateId)
                .MaxAsync(t => (int?)t.DisplayOrder) ?? 0;

            _db.OnboardingTaskTemplates.Add(new OnboardingTaskTemplate
            {
                TemplateId = templateId,
                Title = title.Trim(),
                Detail = detail,
                Category = category,
                Owner = owner,
                DueDayOffset = dueDayOffset,
                IsStatutory = isStatutory,
                Authority = authority,
                DisplayOrder = next + 1
            });

            await _db.SaveChangesAsync();
            TempData["Success"] = "Step added to the template. Programmes already running are unaffected.";
            return RedirectToAction(nameof(Template), new { id = templateId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTemplateTask(int id)
        {
            if (!IsHr) return AccessDenied();

            var t = await _db.OnboardingTaskTemplates.FindAsync(id);
            if (t == null) return NotFound();

            var templateId = t.TemplateId;

            if (t.IsStatutory)
            {
                TempData["Error"] = $"\"{t.Title}\" is required by {t.Authority ?? "law"} and cannot be "
                                  + "removed from a template. If it does not apply to your organisation, "
                                  + "check the provision before deciding that.";
                return RedirectToAction(nameof(Template), new { id = templateId });
            }

            _db.OnboardingTaskTemplates.Remove(t);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Step removed.";
            return RedirectToAction(nameof(Template), new { id = templateId });
        }

        [HttpPost]
        public async Task<IActionResult> CreateTemplate(string name, string? description, EmploymentType? appliesTo)
        {
            if (!IsHr) return AccessDenied();

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Name the template.";
                return RedirectToAction(nameof(Templates));
            }

            var template = new OnboardingTemplate
            {
                Name = name.Trim(),
                Description = description,
                AppliesTo = appliesTo
            };

            _db.OnboardingTemplates.Add(template);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Template created. Add its steps — including the statutory ones, which "
                                + "apply whatever the employment type.";

            return RedirectToAction(nameof(Template), new { id = template.Id });
        }
    }
}
