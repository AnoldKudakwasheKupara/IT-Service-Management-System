using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models.Hr;
using IT_Service_Management_System.Services;
using IT_Service_Management_System.Services.Hr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Payroll under the Zimbabwean statutory regime — salary structures, pay components, the
    /// monthly run, payslips, and the returns owed to ZIMRA, NSSA and the levy funds.
    /// <para>
    /// Restricted to payroll staff. Employees reach their own payslips through
    /// <see cref="MyPayslips"/>, which is opened to every role but scoped to the signed-in person.
    /// </para>
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "HR", "Finance")]
    public class PayrollController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly PayrollService _payroll;
        private readonly StatutoryService _statutory;
        private readonly AuditService _audit;

        public PayrollController(ApplicationDbContext db, PayrollService payroll,
            StatutoryService statutory, AuditService audit)
        {
            _db = db; _payroll = payroll; _statutory = statutory; _audit = audit;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        private const int PageSize = 25;

        // ════════════════════════════════════════════════════════════════════════
        //  Runs
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Index(int? year)
        {
            var filter = year ?? DateTime.Today.Year;

            ViewBag.Year = filter;
            ViewBag.Runs = await _db.PayrollRuns.AsNoTracking()
                .Include(r => r.PreparedBy).Include(r => r.ApprovedBy)
                .Where(r => r.PeriodYear == filter)
                .OrderByDescending(r => r.PeriodMonth)
                .ToListAsync();

            // A payroll that runs with no tables loaded silently deducts nothing, so the readiness
            // of the statutory configuration is shown before anyone starts.
            var asAt = DateTime.Today;
            ViewBag.NssaCeiling = await _statutory.ValueAsync(StatutoryKeys.NssaInsurableEarningsCeiling, asAt);
            ViewBag.Currencies = await _db.SalaryStructures.AsNoTracking()
                .Select(s => s.Currency).Distinct().ToListAsync();
            ViewBag.BandCoverage = await _db.PayeTaxBands.AsNoTracking()
                .Where(b => b.EffectiveFrom <= asAt && (b.EffectiveTo == null || b.EffectiveTo >= asAt))
                .GroupBy(b => b.Currency)
                .Select(g => new { Currency = g.Key, Bands = g.Count() })
                .ToDictionaryAsync(x => x.Currency, x => x.Bands);
            ViewBag.OnPayroll = await _db.SalaryStructures.AsNoTracking()
                .Where(s => s.EffectiveTo == null || s.EffectiveTo >= DateTime.Today)
                .Select(s => s.EmployeeId).Distinct().CountAsync();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateRun(int periodYear, int periodMonth, string currency, DateTime? payDate)
        {
            if (periodMonth is < 1 or > 12)
            {
                TempData["Error"] = "Choose a month between 1 and 12.";
                return RedirectToAction(nameof(Index), new { year = periodYear });
            }

            var clash = await _db.PayrollRuns.AnyAsync(r =>
                r.PeriodYear == periodYear && r.PeriodMonth == periodMonth && r.Currency == currency);
            if (clash)
            {
                TempData["Error"] = $"A {currency} run already exists for "
                                  + $"{new DateTime(periodYear, periodMonth, 1):MMMM yyyy}.";
                return RedirectToAction(nameof(Index), new { year = periodYear });
            }

            var periodEnd = new DateTime(periodYear, periodMonth, 1).AddMonths(1).AddDays(-1);

            var run = new PayrollRun
            {
                PeriodYear = periodYear,
                PeriodMonth = periodMonth,
                Currency = currency,
                PayDate = payDate ?? periodEnd,
                // Statutory rates are read as at the end of the period, so a run prepared or
                // reworked later still applies the tables that were in force then.
                StatutoryAsAt = periodEnd,
                PreparedById = Uid,
                PreparedAt = DateTime.Now
            };

            _db.PayrollRuns.Add(run);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", nameof(PayrollRun), run.Id,
                $"{run.Reference} ({currency}) opened for {run.PeriodName}");

            TempData["Success"] = $"{run.Reference} created. Calculate it when the month's variable "
                                + "pay has been entered.";
            return RedirectToAction(nameof(Run), new { id = run.Id });
        }

        public async Task<IActionResult> Run(int id)
        {
            var run = await _db.PayrollRuns.AsNoTracking()
                .Include(r => r.PreparedBy).Include(r => r.ApprovedBy)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (run == null) return NotFound();

            ViewBag.Payslips = await _db.Payslips.AsNoTracking()
                .Include(p => p.Employee).ThenInclude(e => e!.Department)
                .Where(p => p.PayrollRunId == id)
                .OrderBy(p => p.Employee!.LastName)
                .ToListAsync();

            ViewBag.Returns = await _payroll.StatutoryReturnsAsync(id);

            return View(run);
        }

        [HttpPost]
        public async Task<IActionResult> Calculate(int id)
        {
            var result = await _payroll.CalculateAsync(id);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Run), new { id });
            }

            await _audit.LogAsync("Calculated", nameof(PayrollRun), id,
                $"{result.Payslips} payslip(s) calculated");

            TempData["Success"] = $"{result.Payslips} payslip(s) calculated.";
            if (result.Warnings.Count > 0) TempData["Warning"] = string.Join(" ", result.Warnings);

            return RedirectToAction(nameof(Run), new { id });
        }

        /// <summary>
        /// Approve a run. Separated from calculation and restricted to finance and administrators,
        /// because approval is what locks the figures — after this a payslip cannot change.
        /// </summary>
        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "Finance")]
        public async Task<IActionResult> Approve(int id)
        {
            var run = await _db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == id);
            if (run == null) return NotFound();

            if (run.Status != PayrollRunStatus.Calculated)
            {
                TempData["Error"] = "Only a calculated run can be approved.";
                return RedirectToAction(nameof(Run), new { id });
            }

            // Whoever prepared it should not also approve it.
            if (run.PreparedById == Uid && !Roles.IsFullAccess(Role))
            {
                TempData["Error"] = "A payroll run must be approved by someone other than the person "
                                  + "who prepared it.";
                return RedirectToAction(nameof(Run), new { id });
            }

            run.Status = PayrollRunStatus.Approved;
            run.ApprovedById = Uid;
            run.ApprovedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Approved", nameof(PayrollRun), id,
                $"{run.Reference} approved — {run.EmployeeCount} employee(s), "
                + $"net {run.Currency} {run.TotalNet:N2}, employer cost {run.TotalEmployerCost:N2}");

            TempData["Success"] = $"{run.Reference} approved and locked.";
            return RedirectToAction(nameof(Run), new { id });
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "Finance")]
        public async Task<IActionResult> MarkPaid(int id)
        {
            var run = await _db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == id);
            if (run == null) return NotFound();

            if (run.Status != PayrollRunStatus.Approved)
            {
                TempData["Error"] = "Only an approved run can be marked paid.";
                return RedirectToAction(nameof(Run), new { id });
            }

            run.Status = PayrollRunStatus.Paid;
            run.PaidAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Paid", nameof(PayrollRun), id,
                $"{run.Reference} marked paid — {run.Currency} {run.TotalNet:N2} to {run.EmployeeCount} employee(s)");

            TempData["Success"] = $"{run.Reference} marked paid.";
            return RedirectToAction(nameof(Run), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Payslips
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Payslip(int id)
        {
            var payslip = await _db.Payslips.AsNoTracking()
                .Include(p => p.Employee).ThenInclude(e => e!.Department)
                .Include(p => p.PayrollRun)
                .Include(p => p.Lines.OrderBy(l => l.DisplayOrder))
                .FirstOrDefaultAsync(p => p.Id == id);
            if (payslip == null) return NotFound();

            return View(payslip);
        }

        /// <summary>
        /// An employee's own payslips. Open to every role but scoped to the signed-in person, so
        /// nobody needs payroll rights to see what they were paid.
        /// </summary>
        [AllowAnyRole]
        public async Task<IActionResult> MyPayslips()
        {
            var me = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == Uid);
            if (me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");

            ViewBag.Employee = me;

            // Only from approved runs — a draft calculation is working material, not a payslip.
            return View(await _db.Payslips.AsNoTracking()
                .Include(p => p.PayrollRun)
                .Where(p => p.EmployeeId == me.Id
                         && (p.PayrollRun!.Status == PayrollRunStatus.Approved
                          || p.PayrollRun.Status == PayrollRunStatus.Paid))
                .OrderByDescending(p => p.PayrollRun!.PeriodYear)
                .ThenByDescending(p => p.PayrollRun!.PeriodMonth)
                .ToListAsync());
        }

        /// <summary>My own payslip. Checks ownership rather than relying on the id being unguessable.</summary>
        [AllowAnyRole]
        public async Task<IActionResult> MyPayslip(int id)
        {
            var me = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == Uid);
            if (me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");

            var payslip = await _db.Payslips.AsNoTracking()
                .Include(p => p.Employee).ThenInclude(e => e!.Department)
                .Include(p => p.PayrollRun)
                .Include(p => p.Lines.OrderBy(l => l.DisplayOrder))
                .FirstOrDefaultAsync(p => p.Id == id && p.EmployeeId == me.Id);

            if (payslip == null) return AccessDenied();
            if (payslip.PayrollRun?.Status is not (PayrollRunStatus.Approved or PayrollRunStatus.Paid))
                return AccessDenied();

            ViewBag.IsSelfService = true;
            return View("Payslip", payslip);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Salary structures and components
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Salaries(string? q, int? departmentId, int page = 1)
        {
            IQueryable<Employee> employees = _db.Employees.AsNoTracking()
                .Include(e => e.Department)
                .Where(e => e.Status == EmploymentStatus.Active
                         || e.Status == EmploymentStatus.OnProbation
                         || e.Status == EmploymentStatus.OnLeave);

            if (departmentId.HasValue) employees = employees.Where(e => e.DepartmentId == departmentId.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                employees = employees.Where(e => e.FirstName.Contains(term)
                                              || e.LastName.Contains(term)
                                              || e.EmployeeNumber.Contains(term));
            }

            var (people, paging) = await employees.OrderBy(e => e.LastName).PageAsync(page, PageSize);
            var ids = people.Select(p => p.Id).ToList();

            var structures = await _db.SalaryStructures.AsNoTracking()
                .Where(s => ids.Contains(s.EmployeeId)
                         && s.EffectiveFrom <= DateTime.Today
                         && (s.EffectiveTo == null || s.EffectiveTo >= DateTime.Today))
                .ToListAsync();

            // A back-dated increase can leave two overlapping; show the later.
            ViewBag.Structures = structures
                .GroupBy(s => s.EmployeeId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.EffectiveFrom).First());

            ViewBag.Paging = paging;
            ViewBag.Q = q; ViewBag.DepartmentId = departmentId;
            ViewBag.Departments = await _db.Departments.AsNoTracking()
                .OrderBy(d => d.Name).Select(d => new { d.Id, d.Name }).ToListAsync();

            return View(people);
        }

        public async Task<IActionResult> Employee(int id)
        {
            var employee = await _db.Employees.AsNoTracking()
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            ViewBag.Structures = await _db.SalaryStructures.AsNoTracking()
                .Where(s => s.EmployeeId == id)
                .OrderByDescending(s => s.EffectiveFrom)
                .ToListAsync();

            ViewBag.Components = await _db.PayComponents.AsNoTracking()
                .Where(c => c.EmployeeId == id)
                .OrderByDescending(c => c.IsActive).ThenBy(c => c.Type)
                .ToListAsync();

            ViewBag.Payslips = await _db.Payslips.AsNoTracking()
                .Include(p => p.PayrollRun)
                .Where(p => p.EmployeeId == id)
                .OrderByDescending(p => p.PayrollRun!.PeriodYear)
                .ThenByDescending(p => p.PayrollRun!.PeriodMonth)
                .Take(12)
                .ToListAsync();

            return View(employee);
        }

        [HttpPost]
        public async Task<IActionResult> SaveSalary(SalaryStructure input)
        {
            if (input.BasicSalary < 0)
            {
                TempData["Error"] = "Basic salary cannot be negative.";
                return RedirectToAction(nameof(Employee), new { id = input.EmployeeId });
            }

            // A new structure closes the one it supersedes rather than overwriting it, so previous
            // payslips keep the figure they were actually calculated on.
            var previous = await _db.SalaryStructures
                .Where(s => s.EmployeeId == input.EmployeeId && s.EffectiveTo == null)
                .ToListAsync();

            foreach (var old in previous.Where(o => o.EffectiveFrom < input.EffectiveFrom))
                old.EffectiveTo = input.EffectiveFrom.AddDays(-1);

            input.CreatedById = Uid;
            input.CreatedAt = DateTime.Now;
            _db.SalaryStructures.Add(input);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("SalarySet", nameof(SalaryStructure), input.Id,
                $"{input.Currency} {input.BasicSalary:N2} for employee #{input.EmployeeId} "
                + $"from {input.EffectiveFrom:d MMM yyyy}{(string.IsNullOrWhiteSpace(input.Reason) ? "" : $": {input.Reason}")}");

            TempData["Success"] = $"Salary set at {input.Currency} {input.BasicSalary:N2} "
                                + $"from {input.EffectiveFrom:d MMM yyyy}.";
            return RedirectToAction(nameof(Employee), new { id = input.EmployeeId });
        }

        [HttpPost]
        public async Task<IActionResult> SaveComponent(PayComponent input)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "A pay component needs a name.";
                return RedirectToAction(nameof(Employee), new { id = input.EmployeeId });
            }

            if (input.Id == 0)
            {
                _db.PayComponents.Add(input);
            }
            else
            {
                var component = await _db.PayComponents.FirstOrDefaultAsync(c => c.Id == input.Id);
                if (component == null) return NotFound();

                component.Name = input.Name;
                component.Type = input.Type;
                component.Amount = input.Amount;
                component.PercentageOfBasic = input.PercentageOfBasic;
                component.IsTaxable = input.IsTaxable;
                component.IsPensionable = input.IsPensionable;
                component.IsRecurring = input.IsRecurring;
                component.EffectiveFrom = input.EffectiveFrom;
                component.EffectiveTo = input.EffectiveTo;
                component.Notes = input.Notes;
                component.IsActive = input.IsActive;
            }

            await _db.SaveChangesAsync();

            await _audit.LogAsync(input.Id == 0 ? "Created" : "Updated", nameof(PayComponent), input.Id,
                $"{input.Name} — {input.Type}, {input.Amount:N2} for employee #{input.EmployeeId}");

            TempData["Success"] = "Pay component saved.";
            return RedirectToAction(nameof(Employee), new { id = input.EmployeeId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteComponent(int id, int employeeId)
        {
            var component = await _db.PayComponents.FirstOrDefaultAsync(c => c.Id == id);
            if (component != null)
            {
                // Deactivated rather than deleted, so a payslip that used it stays explicable.
                component.IsActive = false;
                component.EffectiveTo = DateTime.Today;
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Pay component ended.";
            return RedirectToAction(nameof(Employee), new { id = employeeId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Statutory configuration
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The rates, ceilings and PAYE bands payroll runs on. Editable, because every one of them
        /// changes with a Finance Act or a gazette notice.
        /// </summary>
        [RoleAuthorize("Admin", "SystemsAdmin", "Finance")]
        public async Task<IActionResult> Statutory(string? currency)
        {
            ViewBag.Parameters = await _db.StatutoryParameters.AsNoTracking()
                .Include(p => p.UpdatedBy)
                .OrderBy(p => p.Key).ThenByDescending(p => p.EffectiveFrom)
                .ToListAsync();

            ViewBag.Currency = currency ?? "USD";
            ViewBag.Bands = await _db.PayeTaxBands.AsNoTracking()
                .Where(b => b.Currency == (currency ?? "USD"))
                .OrderByDescending(b => b.EffectiveFrom).ThenBy(b => b.FromAmount)
                .ToListAsync();

            ViewBag.Currencies = await _db.PayeTaxBands.AsNoTracking()
                .Select(b => b.Currency).Distinct().ToListAsync();

            return View();
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "Finance")]
        public async Task<IActionResult> SaveParameter(int id, decimal value, DateTime effectiveFrom,
            string? authority, string? notes)
        {
            var existing = await _db.StatutoryParameters.FirstOrDefaultAsync(p => p.Id == id);
            if (existing == null) return NotFound();

            // A changed rate creates a new dated row and closes the old one, rather than
            // overwriting — otherwise correcting today's rate silently restates every past payroll.
            if (existing.Value != value || existing.EffectiveFrom != effectiveFrom)
            {
                existing.EffectiveTo = effectiveFrom.AddDays(-1);

                _db.StatutoryParameters.Add(new StatutoryParameter
                {
                    Key = existing.Key,
                    Name = existing.Name,
                    Value = value,
                    Kind = existing.Kind,
                    Currency = existing.Currency,
                    EffectiveFrom = effectiveFrom,
                    Authority = authority ?? existing.Authority,
                    Notes = notes ?? existing.Notes,
                    UpdatedById = Uid,
                    UpdatedAt = DateTime.Now
                });

                await _audit.LogAsync("StatutoryChanged", nameof(StatutoryParameter), existing.Id,
                    $"{existing.Key}: {existing.Value} → {value} from {effectiveFrom:d MMM yyyy} ({authority})");
            }
            else
            {
                existing.Authority = authority;
                existing.Notes = notes;
                existing.UpdatedById = Uid;
                existing.UpdatedAt = DateTime.Now;
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = $"{existing.Name} updated. Runs dated before "
                                + $"{effectiveFrom:d MMM yyyy} keep the previous value.";
            return RedirectToAction(nameof(Statutory));
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "Finance")]
        public async Task<IActionResult> SaveBand(PayeTaxBand input)
        {
            if (input.Rate < 0 || input.Rate > 100)
            {
                TempData["Error"] = "The rate must be a percentage between 0 and 100.";
                return RedirectToAction(nameof(Statutory), new { currency = input.Currency });
            }

            if (input.Id == 0) _db.PayeTaxBands.Add(input);
            else
            {
                var band = await _db.PayeTaxBands.FirstOrDefaultAsync(b => b.Id == input.Id);
                if (band == null) return NotFound();

                band.FromAmount = input.FromAmount;
                band.ToAmount = input.ToAmount;
                band.Rate = input.Rate;
                band.Deduction = input.Deduction;
                band.EffectiveFrom = input.EffectiveFrom;
                band.EffectiveTo = input.EffectiveTo;
                band.Authority = input.Authority;
            }

            await _db.SaveChangesAsync();

            await _audit.LogAsync(input.Id == 0 ? "Created" : "Updated", nameof(PayeTaxBand), input.Id,
                $"{input.Currency} {input.Period}: {input.FromAmount:N2}–{input.ToAmount?.ToString("N2") ?? "above"} "
                + $"at {input.Rate}% less {input.Deduction:N2}, from {input.EffectiveFrom:d MMM yyyy}");

            TempData["Success"] = "PAYE band saved.";
            return RedirectToAction(nameof(Statutory), new { currency = input.Currency });
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "Finance")]
        public async Task<IActionResult> DeleteBand(int id, string currency)
        {
            var band = await _db.PayeTaxBands.FirstOrDefaultAsync(b => b.Id == id);
            if (band != null) { _db.PayeTaxBands.Remove(band); await _db.SaveChangesAsync(); }

            TempData["Success"] = "PAYE band removed.";
            return RedirectToAction(nameof(Statutory), new { currency });
        }
    }
}
