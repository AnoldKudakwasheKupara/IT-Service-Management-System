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
    /// Employee benefits — medical aid, pension, funeral cover, allowances — and what each one does
    /// to pay.
    /// <para>
    /// The tax treatment is carried on the plan, because in Zimbabwe it decides what the employee
    /// actually receives: a benefit in kind is taxable in their hands, an approved-fund contribution
    /// comes off before PAYE, and a medical aid contribution attracts a credit against the tax
    /// rather than a deduction from income. The module reports the three separately and leaves
    /// posting them to payroll as a deliberate act.
    /// </para>
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "HR", "GeneralManager", "Finance",
                   "DepartmentManager", "ProjectManager", "TeamLead", "Employee",
                   "SupportAgent", "Development", "QualityManager", "Procurement")]
    public class BenefitsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly BenefitsService _benefits;
        private readonly AuditService _audit;

        public BenefitsController(ApplicationDbContext db, BenefitsService benefits, AuditService audit)
        {
            _db = db; _benefits = benefits; _audit = audit;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool IsHr => Roles.IsFullAccess(Role) || Role == Roles.HR;
        private bool IsFinance => Role == Roles.Finance;
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        private async Task<Employee?> MeAsync() =>
            await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == Uid);

        // ════════════════════════════════════════════════════════════════════════
        //  Mine
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Index()
        {
            var me = await MeAsync();
            if (me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");

            ViewBag.Employee = me;
            ViewBag.Costs = await _benefits.EmployeeCostsAsync(me.Id);
            ViewBag.Effect = await _benefits.PayrollEffectAsync(me.Id, DateTime.Today);

            ViewBag.Enrolments = await _db.BenefitEnrolments.AsNoTracking()
                .Include(e => e.Plan).Include(e => e.Dependants)
                .Where(e => e.EmployeeId == me.Id)
                .OrderByDescending(e => e.StartDate)
                .ToListAsync();

            // Plans they could join but have not — shown so an unclaimed benefit is visible.
            var mine = await _db.BenefitEnrolments.AsNoTracking()
                .Where(e => e.EmployeeId == me.Id && (e.EndDate == null || e.EndDate >= DateTime.Today))
                .Select(e => e.PlanId).ToListAsync();

            ViewBag.Available = await _db.BenefitPlans.AsNoTracking()
                .Where(p => p.IsActive && !mine.Contains(p.Id)
                         && (p.AvailableTo == null || p.AvailableTo == me.EmploymentType))
                .OrderBy(p => p.Category).ToListAsync();

            ViewBag.IsHr = IsHr;
            return View();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Plans
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Plans()
        {
            if (!IsHr && !IsFinance) return AccessDenied();

            ViewBag.Summaries = await _benefits.PlanSummariesAsync();
            ViewBag.IsHr = IsHr;

            var unconfigured = await _db.StatutoryParameters.AsNoTracking()
                .Where(p => p.Key == StatutoryKeys.MedicalAidCreditRate
                         && p.EffectiveFrom <= DateTime.Today
                         && (p.EffectiveTo == null || p.EffectiveTo >= DateTime.Today))
                .Select(p => p.Value).FirstOrDefaultAsync();

            ViewBag.MedicalCreditRate = unconfigured;
            ViewBag.HasMedicalPlans = await _db.BenefitPlans
                .AnyAsync(p => p.IsActive && p.TaxTreatment == BenefitTaxTreatment.MedicalAidCredit);

            return View();
        }

        public async Task<IActionResult> EditPlan(int? id)
        {
            if (!IsHr) return AccessDenied();

            if (id == null) return View(new BenefitPlan());

            var plan = await _db.BenefitPlans.FindAsync(id.Value);
            if (plan == null) return NotFound();

            ViewBag.Members = await _db.BenefitEnrolments
                .CountAsync(e => e.PlanId == plan.Id && (e.EndDate == null || e.EndDate >= DateTime.Today));

            return View(plan);
        }

        [HttpPost]
        public async Task<IActionResult> EditPlan(BenefitPlan model)
        {
            if (!IsHr) return AccessDenied();

            if (model.Basis == ContributionBasis.PercentOfBasic
                && model.EmployerRate == 0 && model.EmployeeRate == 0)
                ModelState.AddModelError(nameof(model.EmployerRate),
                    "A percentage plan needs at least one rate.");

            if (model.Basis == ContributionBasis.FixedAmount
                && model.EmployerAmount == 0 && model.EmployeeAmount == 0
                && model.Category != BenefitCategory.Loan)
                ModelState.AddModelError(nameof(model.EmployerAmount),
                    "A fixed-amount plan needs at least one contribution.");

            if (model.TaxTreatment != BenefitTaxTreatment.Exempt && string.IsNullOrWhiteSpace(model.TaxAuthority))
                ModelState.AddModelError(nameof(model.TaxAuthority),
                    "Name the provision the tax treatment comes from. Getting this wrong produces a "
                    + "payslip that looks right and is not, so it should be checkable.");

            if (!model.AllowsDependants) { model.MaxDependants = 0; model.CostPerDependant = 0; }

            if (!ModelState.IsValid)
            {
                ViewBag.Members = model.Id == 0 ? 0
                    : await _db.BenefitEnrolments.CountAsync(e => e.PlanId == model.Id);
                return View(model);
            }

            if (model.Id == 0)
            {
                _db.BenefitPlans.Add(model);
            }
            else
            {
                var existing = await _db.BenefitPlans.FindAsync(model.Id);
                if (existing == null) return NotFound();

                existing.Name = model.Name;
                existing.Description = model.Description;
                existing.Provider = model.Provider;
                existing.Category = model.Category;
                existing.TaxTreatment = model.TaxTreatment;
                existing.TaxAuthority = model.TaxAuthority;
                existing.Basis = model.Basis;
                existing.EmployerAmount = model.EmployerAmount;
                existing.EmployeeAmount = model.EmployeeAmount;
                existing.EmployerRate = model.EmployerRate;
                existing.EmployeeRate = model.EmployeeRate;
                existing.Currency = model.Currency;
                existing.QualifyingMonths = model.QualifyingMonths;
                existing.AvailableTo = model.AvailableTo;
                existing.AllowsDependants = model.AllowsDependants;
                existing.MaxDependants = model.MaxDependants;
                existing.CostPerDependant = model.CostPerDependant;
                existing.IsAutomatic = model.IsAutomatic;
                existing.IsActive = model.IsActive;
                existing.EffectiveFrom = model.EffectiveFrom;
                existing.EffectiveTo = model.EffectiveTo;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Plan saved. Members on it pick up the new figures from the next run, "
                                + "except where their own terms override them.";

            return RedirectToAction(nameof(Plans));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Members
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Members(int? planId)
        {
            if (!IsHr && !IsFinance) return AccessDenied();

            var query = _db.BenefitEnrolments.AsNoTracking()
                .Include(e => e.Plan).Include(e => e.Employee).Include(e => e.Dependants)
                .AsQueryable();

            if (planId.HasValue) query = query.Where(e => e.PlanId == planId.Value);

            ViewBag.Enrolments = await query
                .OrderBy(e => e.Employee!.LastName).ThenBy(e => e.Plan!.Name)
                .ToListAsync();

            ViewBag.PlanId = planId;
            ViewBag.IsHr = IsHr;

            ViewBag.PlanList = new SelectList(
                await _db.BenefitPlans.AsNoTracking().Where(p => p.IsActive)
                    .OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync(),
                "Id", "Name", planId);

            ViewBag.EmployeeList = new SelectList(
                await _db.Employees.AsNoTracking().Where(e => !e.IsDeleted)
                    .OrderBy(e => e.LastName)
                    .Select(e => new { e.Id, Label = e.FirstName + " " + e.LastName + " · " + e.EmployeeNumber })
                    .ToListAsync(),
                "Id", "Label");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Enrol(int employeeId, int planId, DateTime startDate, string? membershipNumber)
        {
            if (!IsHr) return AccessDenied();

            var result = await _benefits.EnrolAsync(employeeId, planId, startDate, membershipNumber);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;

            if (result.Succeeded)
                await _audit.LogAsync("Enrolled", nameof(BenefitEnrolment), result.EnrolmentId,
                    $"Employee {employeeId} enrolled on plan {planId}");

            return RedirectToAction(nameof(Members), new { planId });
        }

        [HttpPost]
        public async Task<IActionResult> EndEnrolment(int id, DateTime endDate, string reason)
        {
            if (!IsHr) return AccessDenied();

            var enrolment = await _db.BenefitEnrolments.FindAsync(id);
            if (enrolment == null) return NotFound();

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Record why the cover ended. Somebody asking later why they were "
                                  + "not covered deserves an answer.";
                return RedirectToAction(nameof(Members));
            }

            if (endDate < enrolment.StartDate)
            {
                TempData["Error"] = "Cover cannot end before it started.";
                return RedirectToAction(nameof(Members));
            }

            enrolment.EndDate = endDate;
            enrolment.EndReason = reason;

            // Dependants come off with the member, on the same date.
            var dependants = await _db.BenefitDependants
                .Where(d => d.EnrolmentId == id && d.RemovedOn == null).ToListAsync();
            foreach (var d in dependants) d.RemovedOn = endDate;

            await _db.SaveChangesAsync();
            TempData["Success"] = $"Cover ended {endDate:d MMM yyyy}. The enrolment stays on record — "
                                + "cover in March is a fact about March.";

            return RedirectToAction(nameof(Members));
        }

        [HttpPost]
        public async Task<IActionResult> SetOverride(int id, decimal? employeeAmount, decimal? employerAmount, string? notes)
        {
            if (!IsHr) return AccessDenied();

            var enrolment = await _db.BenefitEnrolments.FindAsync(id);
            if (enrolment == null) return NotFound();

            enrolment.EmployeeAmountOverride = employeeAmount;
            enrolment.EmployerAmountOverride = employerAmount;
            enrolment.Notes = notes;

            await _db.SaveChangesAsync();
            TempData["Success"] = employeeAmount == null && employerAmount == null
                ? "Overrides cleared — this member now follows the plan, so a change to the plan reaches them."
                : "Member's own terms recorded. These no longer follow changes to the plan.";

            return RedirectToAction(nameof(Members));
        }

        // ── Dependants ───────────────────────────────────────────────────────────

        public async Task<IActionResult> Dependants(int id)
        {
            var enrolment = await _db.BenefitEnrolments.AsNoTracking()
                .Include(e => e.Plan).Include(e => e.Employee).Include(e => e.Dependants)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (enrolment == null) return NotFound();

            var me = await MeAsync();
            if (!IsHr && (me == null || me.Id != enrolment.EmployeeId)) return AccessDenied();

            ViewBag.Enrolment = enrolment;
            ViewBag.IsHr = IsHr;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddDependant(int enrolmentId, string fullName, string relationship,
            DateTime? dateOfBirth, DateTime? addedOn)
        {
            var enrolment = await _db.BenefitEnrolments
                .Include(e => e.Plan).Include(e => e.Dependants)
                .FirstOrDefaultAsync(e => e.Id == enrolmentId);

            if (enrolment == null) return NotFound();

            var me = await MeAsync();
            if (!IsHr && (me == null || me.Id != enrolment.EmployeeId)) return AccessDenied();

            if (!enrolment.Plan!.AllowsDependants)
            {
                TempData["Error"] = $"{enrolment.Plan.Name} does not cover dependants.";
                return RedirectToAction(nameof(Dependants), new { id = enrolmentId });
            }

            var active = enrolment.Dependants.Count(d => d.IsActive);
            if (enrolment.Plan.MaxDependants > 0 && active >= enrolment.Plan.MaxDependants)
            {
                TempData["Error"] = $"{enrolment.Plan.Name} covers at most {enrolment.Plan.MaxDependants} "
                                  + "dependant(s), and that is already reached.";
                return RedirectToAction(nameof(Dependants), new { id = enrolmentId });
            }

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(relationship))
            {
                TempData["Error"] = "A dependant needs a name and a relationship.";
                return RedirectToAction(nameof(Dependants), new { id = enrolmentId });
            }

            _db.BenefitDependants.Add(new BenefitDependant
            {
                EnrolmentId = enrolmentId,
                FullName = fullName.Trim(),
                Relationship = relationship.Trim(),
                DateOfBirth = dateOfBirth,
                AddedOn = addedOn ?? DateTime.Today
            });

            await _db.SaveChangesAsync();
            TempData["Success"] = "Dependant added.";
            return RedirectToAction(nameof(Dependants), new { id = enrolmentId });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveDependant(int id, DateTime removedOn)
        {
            var dependant = await _db.BenefitDependants.Include(d => d.Enrolment)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (dependant == null) return NotFound();

            var me = await MeAsync();
            if (!IsHr && (me == null || me.Id != dependant.Enrolment?.EmployeeId)) return AccessDenied();

            dependant.RemovedOn = removedOn;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Dependant removed from cover. The record of the cover they had stays.";
            return RedirectToAction(nameof(Dependants), new { id = dependant.EnrolmentId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Payroll view
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// What benefits do to a given month's pay, per employee. Reported rather than posted: a
        /// payroll run should pick these up deliberately, not have them appear underneath it.
        /// </summary>
        public async Task<IActionResult> PayrollEffect(DateTime? month)
        {
            if (!IsHr && !IsFinance) return AccessDenied();

            var anchor = new DateTime((month ?? DateTime.Today).Year, (month ?? DateTime.Today).Month, 1);
            var asAt = anchor.AddMonths(1).AddDays(-1);

            var employeeIds = await _db.BenefitEnrolments.AsNoTracking()
                .Where(e => e.StartDate <= asAt && (e.EndDate == null || e.EndDate >= anchor))
                .Select(e => e.EmployeeId).Distinct().ToListAsync();

            var names = await _db.Employees.AsNoTracking()
                .Where(e => employeeIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => $"{e.FirstName} {e.LastName}");

            var rows = new List<(int EmployeeId, string Name, BenefitsService.PayrollEffect Effect)>();
            foreach (var id in employeeIds)
                rows.Add((id, names.GetValueOrDefault(id) ?? "", await _benefits.PayrollEffectAsync(id, asAt)));

            ViewBag.Rows = rows.OrderBy(r => r.Name).ToList();
            ViewBag.Anchor = anchor;
            return View();
        }
    }
}
