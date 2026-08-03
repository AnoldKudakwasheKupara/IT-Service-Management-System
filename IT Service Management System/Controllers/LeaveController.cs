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
    /// Leave management under the Labour Act [Chapter 28:01] — the employee's own leave, the
    /// manager's approval queue, HR's oversight, and the balances behind both.
    /// <para>
    /// Every role can reach their own leave; the queues and the register are gated further down at
    /// the action, because an employee applying for leave and HR administering it are different
    /// jobs with different rights.
    /// </para>
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "HR", "GeneralManager", "DepartmentManager",
                   "ProjectManager", "TeamLead", "Finance", "Procurement", "Employee",
                   "SupportAgent", "Development", "QualityManager")]
    public class LeaveController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly LeaveService _leave;
        private readonly StatutoryService _statutory;
        private readonly AuditService _audit;

        public LeaveController(ApplicationDbContext db, LeaveService leave,
            StatutoryService statutory, AuditService audit)
        {
            _db = db; _leave = leave; _statutory = statutory; _audit = audit;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool IsHr => Roles.IsFullAccess(Role) || Role == Roles.HR;
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        private const int PageSize = 25;

        /// <summary>The employee record behind the signed-in account, or null if they have none.</summary>
        private async Task<Employee?> MeAsync() =>
            await _db.Employees.AsNoTracking()
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.UserId == Uid);

        // ════════════════════════════════════════════════════════════════════════
        //  My leave
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Index(int? year)
        {
            var me = await MeAsync();
            if (me == null) return View("NoEmployeeRecord");

            var cycle = year ?? DateTime.Today.Year;

            ViewBag.Employee = me;
            ViewBag.Year = cycle;
            ViewBag.Balances = await BalancesForAsync(me.Id, cycle);
            ViewBag.Requests = await _db.LeaveRequests.AsNoTracking()
                .Include(r => r.LeaveType).Include(r => r.CoveringEmployee)
                .Where(r => r.EmployeeId == me.Id && r.StartDate.Year == cycle)
                .OrderByDescending(r => r.StartDate)
                .ToListAsync();

            // Who else on the team is away, so somebody can see a clash before booking.
            ViewBag.TeamAway = me.ManagerId == null
                ? new List<LeaveRequest>()
                : await _db.LeaveRequests.AsNoTracking()
                    .Include(r => r.Employee).Include(r => r.LeaveType)
                    .Where(r => r.Employee!.ManagerId == me.ManagerId
                             && r.EmployeeId != me.Id
                             && r.Status == LeaveRequestStatus.Approved
                             && r.EndDate >= DateTime.Today)
                    .OrderBy(r => r.StartDate).Take(10).ToListAsync();

            return View();
        }

        private async Task<List<LeaveBalance>> BalancesForAsync(int employeeId, int cycle)
        {
            var types = await _db.LeaveTypes.AsNoTracking()
                .Where(t => t.IsActive).OrderBy(t => t.DisplayOrder).ToListAsync();

            var balances = new List<LeaveBalance>();
            foreach (var type in types)
            {
                var balance = await _leave.GetOrCreateBalanceAsync(employeeId, type.Id, cycle);
                balance.LeaveType = type;
                balances.Add(balance);
            }
            return balances;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Applying
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Apply(int? employeeId)
        {
            // HR can raise leave on somebody's behalf — a paper form handed in, or an absence
            // recorded after the fact. Everybody else applies for themselves.
            var target = employeeId.HasValue && IsHr
                ? await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId)
                : await MeAsync();

            if (target == null) return View("NoEmployeeRecord");

            await PopulateApplyListsAsync(target);
            return View(new LeaveRequest
            {
                EmployeeId = target.Id,
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddDays(1)
            });
        }

        [HttpPost]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> Apply(LeaveRequest input, IFormFile? document, bool submit = true)
        {
            var target = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == input.EmployeeId);
            if (target == null) return NotFound();

            // Only HR may file leave for somebody else.
            var me = await MeAsync();
            if (!IsHr && (me == null || me.Id != target.Id)) return AccessDenied();

            var type = await _db.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == input.LeaveTypeId);
            if (type == null)
            {
                ModelState.AddModelError(nameof(input.LeaveTypeId), "Choose a leave type.");
                await PopulateApplyListsAsync(target);
                return View(input);
            }

            var days = await _leave.CalculateDaysAsync(type, input.StartDate, input.EndDate, input.IsHalfDay);
            var check = await _leave.CheckAsync(target, type, input.StartDate, input.EndDate, days);

            foreach (var error in check.Errors) ModelState.AddModelError(string.Empty, error);

            if (check.RequiresDocument && document == null && string.IsNullOrEmpty(input.DocumentPath))
                ModelState.AddModelError(string.Empty,
                    $"{type.Name} needs a medical certificate for an absence of "
                    + $"{type.CertificateRequiredAfterDays} day(s) or more.");

            if (!ModelState.IsValid)
            {
                ViewBag.Check = check;
                await PopulateApplyListsAsync(target);
                return View(input);
            }

            input.Days = days;
            input.FullPayDays = check.FullPayDays;
            input.HalfPayDays = check.HalfPayDays;
            input.UnpaidDays = check.UnpaidDays;
            input.SubmittedById = Uid;
            input.CreatedAt = DateTime.Now;
            input.Status = submit ? LeaveRequestStatus.Submitted : LeaveRequestStatus.Draft;

            if (document != null)
            {
                var saved = await SaveDocumentAsync(document);
                if (saved != null)
                {
                    input.DocumentFileName = saved.Value.Name;
                    input.DocumentPath = saved.Value.Path;
                }
            }

            _db.LeaveRequests.Add(input);
            await _db.SaveChangesAsync();

            if (submit) await _leave.ReserveAsync(input);

            await _audit.LogAsync(submit ? "Submitted" : "Created", nameof(LeaveRequest), input.Id,
                $"{input.Reference} — {type.Name}, {days:0.##} day(s) for {target.FullName} "
                + $"({input.StartDate:d MMM yyyy} to {input.EndDate:d MMM yyyy})");

            TempData["Success"] = submit
                ? $"{input.Reference} submitted — {days:0.##} day(s) of {type.Name}."
                : $"{input.Reference} saved as a draft.";

            foreach (var warning in check.Warnings) TempData["Warning"] = string.Join(" ", check.Warnings);

            return RedirectToAction(nameof(Details), new { id = input.Id });
        }

        /// <summary>
        /// Cost a request before it is submitted, so the form can show the day count and any
        /// warning as the dates are chosen.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Preview(int employeeId, int leaveTypeId,
            DateTime startDate, DateTime endDate, bool isHalfDay)
        {
            var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
            var type = await _db.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == leaveTypeId);
            if (employee == null || type == null) return Json(new { ok = false });

            var me = await MeAsync();
            if (!IsHr && (me == null || me.Id != employee.Id)) return Json(new { ok = false });

            var days = await _leave.CalculateDaysAsync(type, startDate, endDate, isHalfDay);
            var check = await _leave.CheckAsync(employee, type, startDate, endDate, days);

            return Json(new
            {
                ok = true,
                days,
                countsWorkingDaysOnly = type.CountsWorkingDaysOnly,
                available = check.Balance?.Available ?? 0,
                fullPay = check.FullPayDays,
                halfPay = check.HalfPayDays,
                unpaid = check.UnpaidDays,
                requiresDocument = check.RequiresDocument,
                errors = check.Errors,
                warnings = check.Warnings
            });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Detail and lifecycle
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Details(int id)
        {
            var request = await _db.LeaveRequests.AsNoTracking()
                .Include(r => r.Employee).ThenInclude(e => e!.Manager)
                .Include(r => r.LeaveType).Include(r => r.CoveringEmployee)
                .Include(r => r.ManagerApprovedBy).Include(r => r.HrApprovedBy)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            var me = await MeAsync();
            if (!await CanSeeAsync(request, me)) return AccessDenied();

            ViewBag.Balance = await _leave.GetOrCreateBalanceAsync(
                request.EmployeeId, request.LeaveTypeId, request.StartDate.Year);
            ViewBag.CanApproveAsManager = await CanApproveAsManagerAsync(request, me);
            ViewBag.CanApproveAsHr = IsHr;
            ViewBag.IsMine = me != null && me.Id == request.EmployeeId;

            return View(request);
        }

        private async Task<bool> CanSeeAsync(LeaveRequest request, Employee? me)
        {
            if (IsHr) return true;
            if (me == null) return false;
            if (me.Id == request.EmployeeId) return true;
            return await CanApproveAsManagerAsync(request, me);
        }

        /// <summary>The applicant's line manager, taken from the employee register.</summary>
        private async Task<bool> CanApproveAsManagerAsync(LeaveRequest request, Employee? me)
        {
            if (me == null) return false;
            var managerId = await _db.Employees.AsNoTracking()
                .Where(e => e.Id == request.EmployeeId).Select(e => e.ManagerId).FirstOrDefaultAsync();
            return managerId == me.Id;
        }

        [HttpPost]
        public async Task<IActionResult> Submit(int id)
        {
            var request = await _db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            var me = await MeAsync();
            if (!IsHr && (me == null || me.Id != request.EmployeeId)) return AccessDenied();

            if (request.Status != LeaveRequestStatus.Draft)
            {
                TempData["Error"] = "Only a draft can be submitted.";
                return RedirectToAction(nameof(Details), new { id });
            }

            request.Status = LeaveRequestStatus.Submitted;
            request.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _leave.ReserveAsync(request);

            await _audit.LogAsync("Submitted", nameof(LeaveRequest), id, $"{request.Reference} submitted");

            TempData["Success"] = $"{request.Reference} submitted for approval.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Approve or reject. The line manager decides first, then HR — HR is second because only
        /// HR sees the balance across the organisation and the statutory position behind it.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Decide(int id, bool approve, string? note, bool asHr = false)
        {
            var request = await _db.LeaveRequests
                .Include(r => r.LeaveType).Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            var me = await MeAsync();

            if (asHr && !IsHr) return AccessDenied();
            if (!asHr && !await CanApproveAsManagerAsync(request, me)) return AccessDenied();

            // Nobody approves their own leave, whatever hat they are wearing.
            if (me != null && me.Id == request.EmployeeId && !Roles.IsFullAccess(Role))
            {
                TempData["Error"] = "You cannot approve your own leave.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!approve && string.IsNullOrWhiteSpace(note))
            {
                TempData["Error"] = "Give a reason when rejecting a leave request.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!request.IsOpen)
            {
                TempData["Error"] = $"{request.Reference} has already been decided.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!approve)
            {
                request.Status = LeaveRequestStatus.Rejected;
                request.DecisionNote = note;
                request.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                await _leave.ReleaseAsync(request, wasApproved: false);

                await _audit.LogAsync("Rejected", nameof(LeaveRequest), id,
                    $"{request.Reference} rejected by {(asHr ? "HR" : "the line manager")}: {note}");

                TempData["Success"] = $"{request.Reference} rejected.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (asHr)
            {
                request.Status = LeaveRequestStatus.Approved;
                request.HrApprovedById = me?.Id;
                request.HrApprovedAt = DateTime.Now;
                request.DecisionNote = note;
                request.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                await _leave.CommitAsync(request);

                await _audit.LogAsync("Approved", nameof(LeaveRequest), id,
                    $"{request.Reference} approved by HR — {request.Days:0.##} day(s) of "
                    + $"{request.LeaveType?.Name} for {request.Employee?.FullName}");

                TempData["Success"] = $"{request.Reference} approved.";
            }
            else
            {
                request.Status = LeaveRequestStatus.ManagerApproved;
                request.ManagerApprovedById = me?.Id;
                request.ManagerApprovedAt = DateTime.Now;
                request.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();

                await _audit.LogAsync("ManagerApproved", nameof(LeaveRequest), id,
                    $"{request.Reference} approved by the line manager; awaiting HR");

                TempData["Success"] = $"{request.Reference} approved — now with HR.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id, string? reason)
        {
            var request = await _db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            var me = await MeAsync();
            if (!IsHr && (me == null || me.Id != request.EmployeeId)) return AccessDenied();

            if (request.Status == LeaveRequestStatus.Taken)
            {
                TempData["Error"] = "Leave that has already been taken cannot be cancelled. "
                                  + "Ask HR to adjust the balance instead.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var wasApproved = request.Status == LeaveRequestStatus.Approved;

            request.Status = LeaveRequestStatus.Cancelled;
            request.CancelledAt = DateTime.Now;
            request.CancellationReason = reason;
            request.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            // A draft never held anything against the balance, so there is nothing to release.
            if (request.Status != LeaveRequestStatus.Draft)
                await _leave.ReleaseAsync(request, wasApproved);

            await _audit.LogAsync("Cancelled", nameof(LeaveRequest), id,
                $"{request.Reference} cancelled: {reason}");

            TempData["Success"] = $"{request.Reference} cancelled and the days returned.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Queues
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Leave waiting on the signed-in manager, and on HR if they hold that role.</summary>
        public async Task<IActionResult> Queue()
        {
            var me = await MeAsync();

            ViewBag.AsManager = me == null
                ? new List<LeaveRequest>()
                : await _db.LeaveRequests.AsNoTracking()
                    .Include(r => r.Employee).Include(r => r.LeaveType)
                    .Where(r => r.Status == LeaveRequestStatus.Submitted
                             && r.Employee!.ManagerId == me.Id
                             && r.EmployeeId != me.Id)
                    .OrderBy(r => r.StartDate).ToListAsync();

            ViewBag.AsHr = IsHr
                ? await _db.LeaveRequests.AsNoTracking()
                    .Include(r => r.Employee).Include(r => r.LeaveType).Include(r => r.ManagerApprovedBy)
                    .Where(r => r.Status == LeaveRequestStatus.ManagerApproved)
                    .OrderBy(r => r.StartDate).ToListAsync()
                : new List<LeaveRequest>();

            ViewBag.IsHr = IsHr;
            return View();
        }

        /// <summary>The whole leave register, for HR.</summary>
        [RoleAuthorize("Admin", "SystemsAdmin", "HR")]
        public async Task<IActionResult> Register(string? q, int? leaveTypeId,
            LeaveRequestStatus? status, DateTime? from, DateTime? to, int page = 1)
        {
            IQueryable<LeaveRequest> query = _db.LeaveRequests.AsNoTracking()
                .Include(r => r.Employee).ThenInclude(e => e!.Department)
                .Include(r => r.LeaveType);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(r => r.Employee!.FirstName.Contains(term)
                                      || r.Employee.LastName.Contains(term)
                                      || r.Employee.EmployeeNumber.Contains(term));
            }

            if (leaveTypeId.HasValue) query = query.Where(r => r.LeaveTypeId == leaveTypeId.Value);
            if (status.HasValue) query = query.Where(r => r.Status == status.Value);
            if (from.HasValue) query = query.Where(r => r.EndDate >= from.Value);
            if (to.HasValue) query = query.Where(r => r.StartDate <= to.Value);

            var (items, paging) = await query
                .OrderByDescending(r => r.StartDate)
                .PageAsync(page, PageSize);

            ViewBag.Paging = paging;
            ViewBag.Q = q; ViewBag.LeaveTypeId = leaveTypeId; ViewBag.Status = status;
            ViewBag.From = from; ViewBag.To = to;
            ViewBag.LeaveTypes = await _db.LeaveTypes.AsNoTracking()
                .OrderBy(t => t.DisplayOrder).Select(t => new { t.Id, t.Name }).ToListAsync();

            return View(items);
        }

        /// <summary>Who is away, as a month calendar.</summary>
        public async Task<IActionResult> Calendar(int? year, int? month, int? departmentId)
        {
            var today = DateTime.Today;
            var anchor = new DateTime(year ?? today.Year, month ?? today.Month, 1);
            var from = anchor;
            var to = anchor.AddMonths(1).AddDays(-1);

            IQueryable<LeaveRequest> query = _db.LeaveRequests.AsNoTracking()
                .Include(r => r.Employee).Include(r => r.LeaveType)
                .Where(r => r.StartDate <= to && r.EndDate >= from
                         && (r.Status == LeaveRequestStatus.Approved || r.Status == LeaveRequestStatus.Taken));

            if (departmentId.HasValue)
                query = query.Where(r => r.Employee!.DepartmentId == departmentId.Value);

            ViewBag.Anchor = anchor;
            ViewBag.Leave = await query.OrderBy(r => r.StartDate).ToListAsync();
            ViewBag.Holidays = await _db.PublicHolidays.AsNoTracking()
                .Where(h => h.Date >= from && h.Date <= to)
                .ToListAsync();
            ViewBag.DepartmentId = departmentId;
            ViewBag.Departments = await _db.Departments.AsNoTracking()
                .OrderBy(d => d.Name).Select(d => new { d.Id, d.Name }).ToListAsync();

            return View();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Administration
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Balances across the organisation, with the ledger behind any one of them.</summary>
        [RoleAuthorize("Admin", "SystemsAdmin", "HR")]
        public async Task<IActionResult> Balances(int? year, int? departmentId, string? q, int page = 1)
        {
            var cycle = year ?? DateTime.Today.Year;

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

            var (people, paging) = await employees
                .OrderBy(e => e.LastName).PageAsync(page, PageSize);

            var ids = people.Select(p => p.Id).ToList();

            ViewBag.Paging = paging;
            ViewBag.Year = cycle;
            ViewBag.DepartmentId = departmentId; ViewBag.Q = q;
            ViewBag.Types = await _db.LeaveTypes.AsNoTracking()
                .Where(t => t.IsActive).OrderBy(t => t.DisplayOrder).ToListAsync();
            ViewBag.Balances = await _db.LeaveBalances.AsNoTracking()
                .Where(b => b.CycleYear == cycle && ids.Contains(b.EmployeeId))
                .ToListAsync();
            ViewBag.Departments = await _db.Departments.AsNoTracking()
                .OrderBy(d => d.Name).Select(d => new { d.Id, d.Name }).ToListAsync();

            return View(people);
        }

        /// <summary>Correct a balance by hand, with the reason recorded on the ledger.</summary>
        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "HR")]
        public async Task<IActionResult> Adjust(int employeeId, int leaveTypeId, int cycleYear,
            decimal days, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "An adjustment needs a reason — it is a change to someone's entitlement.";
                return RedirectToAction(nameof(Balances), new { year = cycleYear });
            }

            var balance = await _leave.GetOrCreateBalanceAsync(employeeId, leaveTypeId, cycleYear);
            balance.Adjustment += days;
            balance.AdjustmentReason = reason;
            balance.UpdatedAt = DateTime.Now;

            _db.LeaveLedgerEntries.Add(new LeaveLedgerEntry
            {
                EmployeeId = employeeId,
                LeaveTypeId = leaveTypeId,
                CycleYear = cycleYear,
                Kind = LeaveLedgerKind.Adjustment,
                Days = days,
                BalanceAfter = balance.Available,
                Narrative = reason,
                RecordedById = Uid
            });

            await _db.SaveChangesAsync();

            await _audit.LogAsync("BalanceAdjusted", nameof(LeaveBalance), balance.Id,
                $"{days:+0.##;-0.##} day(s) for employee #{employeeId}: {reason}");

            TempData["Success"] = $"Balance adjusted by {days:+0.##;-0.##} day(s).";
            return RedirectToAction(nameof(Balances), new { year = cycleYear });
        }

        /// <summary>The movement history behind one balance.</summary>
        public async Task<IActionResult> Ledger(int employeeId, int leaveTypeId, int? year)
        {
            var me = await MeAsync();
            if (!IsHr && (me == null || me.Id != employeeId)) return AccessDenied();

            var cycle = year ?? DateTime.Today.Year;

            ViewBag.Employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
            ViewBag.LeaveType = await _db.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == leaveTypeId);
            ViewBag.Balance = await _leave.GetOrCreateBalanceAsync(employeeId, leaveTypeId, cycle);
            ViewBag.Year = cycle;

            return View(await _db.LeaveLedgerEntries.AsNoTracking()
                .Include(l => l.RecordedBy).Include(l => l.LeaveRequest)
                .Where(l => l.EmployeeId == employeeId && l.LeaveTypeId == leaveTypeId && l.CycleYear == cycle)
                .OrderByDescending(l => l.At)
                .ToListAsync());
        }

        /// <summary>Bring accrual up to date. Safe to run repeatedly — it writes only the difference.</summary>
        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "HR")]
        public async Task<IActionResult> RunAccrual(int? year)
        {
            var cycle = year ?? DateTime.Today.Year;
            var updated = await _leave.RunAccrualAsync(cycle);
            var taken = await _leave.MarkTakenAsync();

            await _audit.LogAsync("AccrualRun", nameof(LeaveBalance), null,
                $"{updated} balance(s) accrued and {taken} request(s) marked taken for {cycle}");

            TempData["Success"] = $"{updated} balance(s) brought up to date"
                                + (taken > 0 ? $", and {taken} completed request(s) marked as taken." : ".");
            return RedirectToAction(nameof(Balances), new { year = cycle });
        }

        /// <summary>Close a cycle — carry over what the type allows and forfeit the rest.</summary>
        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "HR")]
        public async Task<IActionResult> CloseCycle(int year)
        {
            if (year >= DateTime.Today.Year)
            {
                TempData["Error"] = "A cycle can only be closed once it has ended.";
                return RedirectToAction(nameof(Balances), new { year });
            }

            var processed = await _leave.CloseCycleAsync(year);

            await _audit.LogAsync("CycleClosed", nameof(LeaveBalance), null,
                $"Leave cycle {year} closed; {processed} balance(s) carried over or forfeited");

            TempData["Success"] = $"{year} closed — {processed} balance(s) processed.";
            return RedirectToAction(nameof(Balances), new { year = year + 1 });
        }

        /// <summary>The leave types and the statutory basis for each.</summary>
        [RoleAuthorize("Admin", "SystemsAdmin", "HR")]
        public async Task<IActionResult> Types() =>
            View(await _db.LeaveTypes.AsNoTracking().OrderBy(t => t.DisplayOrder).ToListAsync());

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "HR")]
        public async Task<IActionResult> SaveType(LeaveType input)
        {
            if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Code))
            {
                TempData["Error"] = "A leave type needs a name and a code.";
                return RedirectToAction(nameof(Types));
            }

            if (input.Id == 0)
            {
                var clash = await _db.LeaveTypes.AnyAsync(t => t.Code == input.Code);
                if (clash)
                {
                    TempData["Error"] = $"The code {input.Code} is already in use.";
                    return RedirectToAction(nameof(Types));
                }
                _db.LeaveTypes.Add(input);
            }
            else
            {
                var type = await _db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == input.Id);
                if (type == null) return NotFound();

                type.Name = input.Name;
                type.Description = input.Description;
                type.Authority = input.Authority;
                type.AnnualEntitlementDays = input.AnnualEntitlementDays;
                type.AccrualPerMonth = input.AccrualPerMonth;
                type.IsPaid = input.IsPaid;
                type.HasHalfPayTier = input.HasHalfPayTier;
                type.HalfPayDays = input.HalfPayDays;
                type.MaxCarryOverDays = input.MaxCarryOverDays;
                type.QualifyingMonths = input.QualifyingMonths;
                type.RequiresMedicalCertificate = input.RequiresMedicalCertificate;
                type.CertificateRequiredAfterDays = input.CertificateRequiredAfterDays;
                type.RestrictedToGender = input.RestrictedToGender;
                type.CountsWorkingDaysOnly = input.CountsWorkingDaysOnly;
                type.NoticeDaysRequired = input.NoticeDaysRequired;
                type.PaidOutOnTermination = input.PaidOutOnTermination;
                type.Colour = input.Colour;
                type.IsActive = input.IsActive;
                type.DisplayOrder = input.DisplayOrder;
            }

            await _db.SaveChangesAsync();

            await _audit.LogAsync(input.Id == 0 ? "Created" : "Updated", nameof(LeaveType), input.Id,
                $"{input.Name} ({input.Code})");

            TempData["Success"] = "Leave type saved.";
            return RedirectToAction(nameof(Types));
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private async Task PopulateApplyListsAsync(Employee target)
        {
            ViewBag.Target = target;
            ViewBag.IsHr = IsHr;

            ViewBag.LeaveTypes = await _db.LeaveTypes.AsNoTracking()
                .Where(t => t.IsActive).OrderBy(t => t.DisplayOrder).ToListAsync();

            ViewBag.Balances = await BalancesForAsync(target.Id, DateTime.Today.Year);

            // Cover is offered from the same department, since that is who realistically picks up
            // the work.
            ViewBag.Colleagues = await _db.Employees.AsNoTracking()
                .Where(e => e.Id != target.Id
                         && e.DepartmentId == target.DepartmentId
                         && e.Status == EmploymentStatus.Active)
                .OrderBy(e => e.LastName)
                .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName })
                .ToListAsync();
        }

        /// <summary>
        /// Store a supporting document — normally a medical certificate. Kept to a small allowlist
        /// because these are health records and the upload path is reachable by every employee.
        /// </summary>
        private async Task<(string Name, string Path)?> SaveDocumentAsync(IFormFile file)
        {
            if (file.Length == 0 || file.Length > 10 * 1024 * 1024) return null;

            var allowed = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(extension)) return null;

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "hr", "leave");
            Directory.CreateDirectory(folder);

            // Generated name — the original never reaches the filesystem.
            var stored = $"{Guid.NewGuid():N}{extension}";
            await using (var stream = System.IO.File.Create(Path.Combine(folder, stored)))
                await file.CopyToAsync(stream);

            var displayName = Path.GetFileName(file.FileName);
            return (displayName.Length > 250 ? displayName[..250] : displayName,
                    $"/uploads/hr/leave/{stored}");
        }
    }
}
