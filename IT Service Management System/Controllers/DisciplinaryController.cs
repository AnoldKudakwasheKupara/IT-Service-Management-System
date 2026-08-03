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
    /// Disciplinary cases, run to the National Employment Code of Conduct in SI 15 of 2006 (or the
    /// employer's own registered code where it has one).
    /// <para>
    /// The controller walks a case through the stages in the order fairness requires — charge,
    /// notice, hearing, finding, penalty, appeal — and refuses to let a stage be recorded before the
    /// one it depends on. An unfair-dismissal claim turns on procedure far more often than on the
    /// facts, so the order is enforced rather than suggested.
    /// </para>
    /// <para>
    /// Case files are confidential. HR and full-access roles see the register; a line manager sees
    /// cases for their own reports; an employee sees only their own.
    /// </para>
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "HR", "GeneralManager", "DepartmentManager",
                   "ProjectManager", "TeamLead", "Finance", "Procurement", "Employee",
                   "SupportAgent", "Development", "QualityManager")]
    public class DisciplinaryController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly DisciplinaryService _discipline;
        private readonly AuditService _audit;

        public DisciplinaryController(ApplicationDbContext db, DisciplinaryService discipline, AuditService audit)
        {
            _db = db; _discipline = discipline; _audit = audit;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool IsHr => Roles.IsFullAccess(Role) || Role == Roles.HR;
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        private const int PageSize = 20;

        private async Task<Employee?> MeAsync() =>
            await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == Uid);

        /// <summary>
        /// Who may open a given case file. Deliberately narrow: a disciplinary record follows someone
        /// for the rest of their employment, and there is no good reason for a colleague to read it.
        /// </summary>
        private async Task<bool> CanSeeAsync(DisciplinaryCase c, Employee? me)
        {
            if (IsHr) return true;
            if (me == null) return false;
            if (c.EmployeeId == me.Id) return true;                 // the employee themselves
            if (c.ChairpersonId == me.Id) return true;              // chaired the hearing
            if (c.AppealHeardById == me.Id) return true;            // heard the appeal
            if (c.RaisedById == Uid) return true;                   // raised the allegation

            var managerId = await _db.Employees.AsNoTracking()
                .Where(e => e.Id == c.EmployeeId).Select(e => e.ManagerId).FirstOrDefaultAsync();
            return managerId == me.Id;
        }

        private async Task LoadOffencesAsync(int? selected = null)
        {
            var offences = await _db.DisciplinaryOffences.AsNoTracking()
                .Where(o => o.IsActive)
                .OrderBy(o => o.DisplayOrder).ThenBy(o => o.Name)
                .ToListAsync();

            ViewBag.Offences = offences;
            ViewBag.OffenceList = new SelectList(offences, "Id", "Name", selected);
        }

        private async Task LoadEmployeesAsync(int? selected = null)
        {
            var people = await _db.Employees.AsNoTracking()
                .Where(e => !e.IsDeleted)
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .Select(e => new { e.Id, Label = e.FirstName + " " + e.LastName + " · " + e.EmployeeNumber })
                .ToListAsync();

            ViewBag.EmployeeList = new SelectList(people, "Id", "Label", selected);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Register
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Index(string? q, DisciplinaryStatus? status, int? employeeId,
            bool openOnly = false, int page = 1)
        {
            var me = await MeAsync();
            if (!IsHr && me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");

            var query = _db.DisciplinaryCases.AsNoTracking()
                .Include(c => c.Employee).Include(c => c.Offence)
                .AsQueryable();

            // Non-HR sees their own cases and their reports' cases, nothing else.
            if (!IsHr)
            {
                var reportIds = await _db.Employees.AsNoTracking()
                    .Where(e => e.ManagerId == me!.Id).Select(e => e.Id).ToListAsync();
                reportIds.Add(me!.Id);
                query = query.Where(c => reportIds.Contains(c.EmployeeId));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(c => c.Title.Contains(term)
                                      || c.Particulars.Contains(term)
                                      || c.Employee!.FirstName.Contains(term)
                                      || c.Employee!.LastName.Contains(term)
                                      || c.Employee!.EmployeeNumber.Contains(term));
            }

            if (status.HasValue) query = query.Where(c => c.Status == status.Value);
            if (employeeId.HasValue) query = query.Where(c => c.EmployeeId == employeeId.Value);
            if (openOnly)
                query = query.Where(c => c.Status != DisciplinaryStatus.Closed
                                      && c.Status != DisciplinaryStatus.Withdrawn);

            var total = await query.CountAsync();
            if (page < 1) page = 1;

            ViewBag.Cases = await query
                .OrderByDescending(c => c.Status != DisciplinaryStatus.Closed
                                     && c.Status != DisciplinaryStatus.Withdrawn)
                .ThenByDescending(c => c.ReportedDate).ThenByDescending(c => c.Id)
                .Skip((page - 1) * PageSize).Take(PageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.PageSize = PageSize;
            ViewBag.Total = total;
            ViewBag.Query = q;
            ViewBag.Status = status;
            ViewBag.EmployeeId = employeeId;
            ViewBag.OpenOnly = openOnly;
            ViewBag.IsHr = IsHr;

            ViewBag.OpenCount = await query.CountAsync(c => c.Status != DisciplinaryStatus.Closed
                                                         && c.Status != DisciplinaryStatus.Withdrawn);
            ViewBag.AwaitingHearing = await query.CountAsync(c => c.Status == DisciplinaryStatus.ChargeServed
                                                               || c.Status == DisciplinaryStatus.HearingScheduled);
            ViewBag.UnderAppeal = await query.CountAsync(c => c.Status == DisciplinaryStatus.UnderAppeal);

            await LoadEmployeesAsync(employeeId);
            return View();
        }

        /// <summary>An employee's own record — what is on file about them, and nothing about anyone else.</summary>
        public async Task<IActionResult> MyRecord()
        {
            var me = await MeAsync();
            if (me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");

            ViewBag.Employee = me;
            ViewBag.Cases = await _db.DisciplinaryCases.AsNoTracking()
                .Include(c => c.Offence)
                .Where(c => c.EmployeeId == me.Id)
                .OrderByDescending(c => c.ReportedDate).ThenByDescending(c => c.Id)
                .ToListAsync();

            ViewBag.LiveWarnings = await _discipline.LiveWarningsAsync(me.Id);
            return View();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Case file
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Details(int id)
        {
            var c = await _db.DisciplinaryCases.AsNoTracking()
                .Include(x => x.Employee).Include(x => x.Offence)
                .Include(x => x.Chairperson).Include(x => x.AppealHeardBy)
                .Include(x => x.RaisedBy)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (c == null) return NotFound();

            var me = await MeAsync();
            if (!await CanSeeAsync(c, me)) return AccessDenied();

            ViewBag.Case = c;
            ViewBag.IsHr = IsHr;
            ViewBag.IsSubject = me != null && me.Id == c.EmployeeId;

            ViewBag.Events = await _db.DisciplinaryEvents.AsNoTracking()
                .Include(e => e.RecordedBy)
                .Where(e => e.CaseId == id)
                .OrderByDescending(e => e.At).ThenByDescending(e => e.Id)
                .ToListAsync();

            ViewBag.LiveWarnings = await _discipline.LiveWarningsAsync(c.EmployeeId, id);

            if (c.OffenceId.HasValue)
                ViewBag.Guidance = await _discipline.SuggestPenaltyAsync(c.EmployeeId, c.OffenceId.Value, id);

            if (c.Penalty is DisciplinaryPenalty.DismissalOnNotice or DisciplinaryPenalty.SummaryDismissal)
                ViewBag.Cost = await _discipline.EstimateTerminationCostAsync(c.EmployeeId, c.Penalty);

            await LoadOffencesAsync(c.OffenceId);
            await LoadEmployeesAsync();
            return View();
        }

        // ── Raise ────────────────────────────────────────────────────────────────

        public async Task<IActionResult> Create(int? employeeId)
        {
            if (!IsHr)
            {
                // A line manager may raise a case, but only against someone who reports to them.
                var me = await MeAsync();
                if (me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");
            }

            await LoadOffencesAsync();
            await LoadEmployeesAsync(employeeId);

            return View(new DisciplinaryCase
            {
                EmployeeId = employeeId ?? 0,
                IncidentDate = DateTime.Today,
                ReportedDate = DateTime.Today
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create(DisciplinaryCase model)
        {
            var me = await MeAsync();

            if (!IsHr)
            {
                var managerId = await _db.Employees.AsNoTracking()
                    .Where(e => e.Id == model.EmployeeId).Select(e => e.ManagerId).FirstOrDefaultAsync();
                if (me == null || managerId != me.Id)
                {
                    TempData["Error"] = "You can only raise a disciplinary matter for someone who reports to you.";
                    return AccessDenied();
                }
            }

            if (model.EmployeeId == 0)
                ModelState.AddModelError(nameof(model.EmployeeId), "Select the employee the allegation concerns.");

            if (model.IncidentDate > DateTime.Today)
                ModelState.AddModelError(nameof(model.IncidentDate), "The incident cannot be in the future.");

            if (!ModelState.IsValid)
            {
                await LoadOffencesAsync(model.OffenceId);
                await LoadEmployeesAsync(model.EmployeeId);
                return View(model);
            }

            model.RaisedById = Uid;
            model.Status = DisciplinaryStatus.Reported;
            model.Finding = DisciplinaryFinding.Pending;
            model.Penalty = DisciplinaryPenalty.None;
            model.CreatedAt = DateTime.Now;

            _db.DisciplinaryCases.Add(model);
            await _db.SaveChangesAsync();

            _discipline.RecordEvent(model, "Allegation reported", model.Title, Uid);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Raised", nameof(DisciplinaryCase), model.Id,
                $"Disciplinary case {model.Reference} raised");

            TempData["Success"] = $"Case {model.Reference} opened. Serve the charge next — nothing else "
                                + "should happen before the employee knows what is alleged.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // ── Investigation and charge ─────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> StartInvestigation(int id, string? note)
        {
            var c = await _db.DisciplinaryCases.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();
            if (!IsHr && c.RaisedById != Uid) return AccessDenied();

            c.Status = DisciplinaryStatus.UnderInvestigation;
            c.UpdatedAt = DateTime.Now;
            _discipline.RecordEvent(c, "Investigation opened", note, Uid);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Investigation opened.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> ServeCharge(int id, DateTime servedDate, int? offenceId,
            string? particulars, bool suspend = false, bool suspensionOnFullPay = true)
        {
            if (!IsHr) return AccessDenied();

            var c = await _db.DisciplinaryCases.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            if (servedDate > DateTime.Today)
            {
                TempData["Error"] = "A charge cannot be recorded as served on a future date.";
                return RedirectToAction(nameof(Details), new { id });
            }

            c.ChargeServedDate = servedDate;
            if (offenceId.HasValue) c.OffenceId = offenceId;
            if (!string.IsNullOrWhiteSpace(particulars)) c.Particulars = particulars;

            c.IsSuspended = suspend;
            if (suspend)
            {
                c.SuspensionOnFullPay = suspensionOnFullPay;
                c.SuspensionFrom ??= servedDate;
            }

            c.Status = DisciplinaryStatus.ChargeServed;
            c.UpdatedAt = DateTime.Now;

            _discipline.RecordEvent(c, "Written charge served",
                suspend
                    ? $"Charge served; employee suspended {(suspensionOnFullPay ? "on full pay" : "without pay")} "
                      + "pending the hearing."
                    : "Charge served in writing.", Uid);

            await _db.SaveChangesAsync();
            await _audit.LogAsync("ChargeServed", nameof(DisciplinaryCase), c.Id,
                $"Charge served on case {c.Reference}");

            TempData["Success"] = suspend && !suspensionOnFullPay
                ? "Charge served. Note that suspension without pay before anything is proven is itself "
                + "open to challenge — the usual position is suspension on full pay."
                : "Charge served. Allow at least two clear days before the hearing so the employee can prepare.";

            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Hearing ──────────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> ScheduleHearing(int id, DateTime hearingDate, string? venue,
            int? chairpersonId, bool representationOffered = false)
        {
            if (!IsHr) return AccessDenied();

            var c = await _db.DisciplinaryCases.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            if (c.ChargeServedDate == null)
            {
                TempData["Error"] = "Serve the charge before scheduling the hearing. The employee has to "
                                  + "know the allegation before being asked to answer it.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (chairpersonId.HasValue && chairpersonId == c.EmployeeId)
            {
                TempData["Error"] = "The employee facing the charge cannot chair their own hearing.";
                return RedirectToAction(nameof(Details), new { id });
            }

            c.HearingDate = hearingDate;
            c.HearingVenue = venue;
            c.ChairpersonId = chairpersonId;
            c.RepresentationOffered = representationOffered;
            c.Status = DisciplinaryStatus.HearingScheduled;
            c.UpdatedAt = DateTime.Now;

            var notice = (hearingDate.Date - c.ChargeServedDate.Value.Date).TotalDays;
            _discipline.RecordEvent(c, "Hearing scheduled",
                $"Set for {hearingDate:d MMM yyyy HH:mm}{(string.IsNullOrWhiteSpace(venue) ? "" : $" at {venue}")}. "
                + $"{notice:0} day(s) notice from service of the charge.", Uid);

            await _db.SaveChangesAsync();

            TempData[notice < 2 ? "Warning" : "Success"] = notice < 2
                ? $"Hearing scheduled, but only {notice:0} day(s) after the charge was served. A hearing held "
                + "at that notice is hard to defend as fair unless the employee agreed to it."
                : "Hearing scheduled.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> RecordHearing(int id, bool employeeAttended, string? absenceExplanation,
            string? representedBy, string? employeeResponse, string? minutes, bool representationOffered = false)
        {
            var c = await _db.DisciplinaryCases.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            var me = await MeAsync();
            if (!IsHr && !(me != null && c.ChairpersonId == me.Id)) return AccessDenied();

            if (c.HearingDate == null)
            {
                TempData["Error"] = "Schedule the hearing before recording it.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!employeeAttended && string.IsNullOrWhiteSpace(absenceExplanation))
            {
                TempData["Error"] = "Record why the hearing went ahead without the employee. A hearing held "
                                  + "in someone's absence with no recorded reason will not stand.";
                return RedirectToAction(nameof(Details), new { id });
            }

            c.EmployeeAttended = employeeAttended;
            c.AbsenceExplanation = absenceExplanation;
            c.RepresentedBy = representedBy;
            c.EmployeeResponse = employeeResponse;
            c.HearingMinutes = minutes;
            if (representationOffered) c.RepresentationOffered = true;
            c.Status = DisciplinaryStatus.HearingHeld;
            c.UpdatedAt = DateTime.Now;

            _discipline.RecordEvent(c, "Hearing held",
                employeeAttended
                    ? $"Employee attended{(string.IsNullOrWhiteSpace(representedBy) ? "" : $", represented by {representedBy}")}."
                    : "Held in the employee's absence.", Uid);

            await _db.SaveChangesAsync();

            TempData["Success"] = "Hearing recorded.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Finding and penalty ──────────────────────────────────────────────────

        /// <summary>
        /// Live guidance for the penalty screen — what the code points to, and why. Advisory only.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Guidance(int employeeId, int offenceId, int? caseId)
        {
            if (!IsHr) return AccessDenied();

            var g = await _discipline.SuggestPenaltyAsync(employeeId, offenceId, caseId);
            return Json(new
            {
                suggested = g.Suggested.ToString(),
                suggestedValue = (int)g.Suggested,
                summary = g.Summary,
                reasoning = g.Reasoning
            });
        }

        [HttpGet]
        public async Task<IActionResult> Cost(int employeeId, DisciplinaryPenalty penalty)
        {
            if (!IsHr) return AccessDenied();

            var cost = await _discipline.EstimateTerminationCostAsync(employeeId, penalty);
            return Json(new
            {
                currency = cost.Currency,
                noticePeriod = cost.NoticePeriod,
                noticeAuthority = cost.NoticeAuthority,
                noticePay = cost.NoticePay,
                leaveDays = cost.LeaveDays,
                leavePayout = cost.LeavePayout,
                total = cost.Total
            });
        }

        [HttpPost]
        public async Task<IActionResult> RecordOutcome(int id, DisciplinaryFinding finding, string? findingReasons,
            DisciplinaryPenalty penalty, string? penaltyReasons, string? mitigation,
            DateTime? penaltyDate, bool appealRightExplained = false, int appealDays = 5)
        {
            if (!IsHr) return AccessDenied();

            var c = await _db.DisciplinaryCases.Include(x => x.Offence).FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            if (c.Status < DisciplinaryStatus.HearingHeld)
            {
                TempData["Error"] = "Record the hearing before the finding. A finding made without a hearing "
                                  + "is the single most common reason a dismissal is overturned.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (finding == DisciplinaryFinding.Pending)
            {
                TempData["Error"] = "Select a finding.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(findingReasons))
            {
                TempData["Error"] = "Give reasons for the finding. A conclusion without reasons cannot be "
                                  + "defended on appeal.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (finding == DisciplinaryFinding.NotProven && penalty != DisciplinaryPenalty.None)
            {
                TempData["Error"] = "A penalty cannot follow a finding that the allegation was not proven.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var stamp = (penaltyDate ?? DateTime.Today).Date;

            c.Finding = finding;
            c.FindingReasons = findingReasons;
            c.Penalty = penalty;
            c.PenaltyReasons = penaltyReasons;
            c.MitigationConsidered = mitigation;
            c.PenaltyDate = penalty == DisciplinaryPenalty.None ? null : stamp;
            c.AppealRightExplained = appealRightExplained;
            c.WarningExpiryDate = DisciplinaryService.WarningExpiry(
                penalty, stamp, c.Offence?.WarningValidityMonths ?? 12);

            if (penalty != DisciplinaryPenalty.None && appealDays > 0)
                c.AppealDeadline = stamp.AddDays(appealDays);

            c.Status = finding == DisciplinaryFinding.NotProven
                ? DisciplinaryStatus.Closed
                : DisciplinaryStatus.PenaltyImposed;

            if (c.Status == DisciplinaryStatus.Closed) c.ClosedAt = DateTime.Now;

            // A suspension pending the hearing ends when the outcome is given, whatever the outcome.
            if (c.IsSuspended)
            {
                c.IsSuspended = false;
                if (c.SuspensionFrom != null) c.SuspensionTo ??= stamp;
            }

            c.UpdatedAt = DateTime.Now;

            _discipline.RecordEvent(c, $"Finding: {finding}",
                penalty == DisciplinaryPenalty.None
                    ? findingReasons
                    : $"Penalty imposed: {penalty}. {penaltyReasons}", Uid);

            await _db.SaveChangesAsync();
            await _audit.LogAsync("Outcome", nameof(DisciplinaryCase), c.Id,
                $"Case {c.Reference}: {finding}, penalty {penalty}");

            TempData["Success"] = string.IsNullOrWhiteSpace(mitigation) && penalty != DisciplinaryPenalty.None
                ? "Outcome recorded — but no mitigation was noted. A penalty set without weighing length of "
                + "service, record and circumstances is vulnerable on review."
                : "Outcome recorded.";

            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Appeal ───────────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> LodgeAppeal(int id, string grounds)
        {
            var c = await _db.DisciplinaryCases.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            var me = await MeAsync();
            var isSubject = me != null && me.Id == c.EmployeeId;
            if (!isSubject && !IsHr) return AccessDenied();

            if (c.Penalty == DisciplinaryPenalty.None)
            {
                TempData["Error"] = "There is no penalty to appeal against.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(grounds))
            {
                TempData["Error"] = "Set out the grounds of appeal.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // A late appeal is recorded rather than blocked — whether to condone it is a decision for
            // the appeal authority, not for the software.
            var late = c.AppealDeadline.HasValue && DateTime.Today > c.AppealDeadline.Value.Date;

            c.AppealLodged = true;
            c.AppealLodgedDate = DateTime.Today;
            c.AppealGrounds = grounds;
            c.Status = DisciplinaryStatus.UnderAppeal;
            c.UpdatedAt = DateTime.Now;

            _discipline.RecordEvent(c, "Appeal lodged",
                late ? $"Lodged after the deadline of {c.AppealDeadline:d MMM yyyy}. Whether to condone the "
                     + "delay is for the appeal authority."
                     : "Lodged within the appeal period.", Uid);

            await _db.SaveChangesAsync();

            TempData[late ? "Warning" : "Success"] = late
                ? "Appeal recorded, but lodged after the deadline. Condonation is a matter for the appeal authority."
                : "Appeal recorded.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> DecideAppeal(int id, AppealOutcome outcome, string decision,
            int? heardById, DateTime? heardDate, DisciplinaryPenalty? substitutedPenalty)
        {
            if (!IsHr) return AccessDenied();

            var c = await _db.DisciplinaryCases.Include(x => x.Offence).FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            if (!c.AppealLodged)
            {
                TempData["Error"] = "No appeal has been lodged on this case.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(decision))
            {
                TempData["Error"] = "Give the appeal decision and the reasons for it.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (heardById.HasValue && heardById == c.ChairpersonId)
            {
                TempData["Error"] = "The appeal cannot be heard by the person who chaired the original hearing.";
                return RedirectToAction(nameof(Details), new { id });
            }

            c.AppealOutcome = outcome;
            c.AppealDecision = decision;
            c.AppealHeardById = heardById;
            c.AppealHeardDate = heardDate ?? DateTime.Today;
            c.UpdatedAt = DateTime.Now;

            switch (outcome)
            {
                case Models.Hr.AppealOutcome.Upheld:
                    // The appeal succeeded: the penalty falls away.
                    c.SubstitutedPenalty = DisciplinaryPenalty.None;
                    c.WarningExpiryDate = null;
                    c.Status = DisciplinaryStatus.Closed;
                    c.ClosedAt = DateTime.Now;
                    break;

                case Models.Hr.AppealOutcome.PenaltyReduced:
                    c.SubstitutedPenalty = substitutedPenalty ?? c.Penalty;
                    c.WarningExpiryDate = DisciplinaryService.WarningExpiry(
                        c.SubstitutedPenalty.Value, c.AppealHeardDate.Value,
                        c.Offence?.WarningValidityMonths ?? 12);
                    c.Status = DisciplinaryStatus.Closed;
                    c.ClosedAt = DateTime.Now;
                    break;

                case Models.Hr.AppealOutcome.RemittedForRehearing:
                    // Back to a fresh hearing — the finding and penalty no longer stand.
                    c.Finding = DisciplinaryFinding.Pending;
                    c.Penalty = DisciplinaryPenalty.None;
                    c.SubstitutedPenalty = null;
                    c.PenaltyDate = null;
                    c.WarningExpiryDate = null;
                    c.HearingDate = null;
                    c.HearingMinutes = null;
                    c.Status = DisciplinaryStatus.ChargeServed;
                    break;

                default: // Dismissed — the original penalty stands.
                    c.Status = DisciplinaryStatus.Closed;
                    c.ClosedAt = DateTime.Now;
                    break;
            }

            _discipline.RecordEvent(c, $"Appeal {outcome}", decision, Uid);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("AppealDecided", nameof(DisciplinaryCase), c.Id,
                $"Case {c.Reference}: appeal {outcome}");

            TempData["Success"] = $"Appeal decision recorded ({outcome}).";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Closing ──────────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> Close(int id, string? note)
        {
            if (!IsHr) return AccessDenied();

            var c = await _db.DisciplinaryCases.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            if (c.Status == DisciplinaryStatus.UnderAppeal)
            {
                TempData["Error"] = "Decide the appeal before closing the case.";
                return RedirectToAction(nameof(Details), new { id });
            }

            c.Status = DisciplinaryStatus.Closed;
            c.ClosedAt = DateTime.Now;
            c.IsSuspended = false;
            c.UpdatedAt = DateTime.Now;

            _discipline.RecordEvent(c, "Case closed", note, Uid);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Case closed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Withdraw(int id, string reason)
        {
            if (!IsHr) return AccessDenied();

            var c = await _db.DisciplinaryCases.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Record why the allegation is not being pursued.";
                return RedirectToAction(nameof(Details), new { id });
            }

            c.Status = DisciplinaryStatus.Withdrawn;
            c.Finding = DisciplinaryFinding.Pending;
            c.Penalty = DisciplinaryPenalty.None;
            c.WarningExpiryDate = null;
            c.IsSuspended = false;
            c.ClosedAt = DateTime.Now;
            c.UpdatedAt = DateTime.Now;

            _discipline.RecordEvent(c, "Allegation withdrawn", reason, Uid);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Withdrawn", nameof(DisciplinaryCase), c.Id,
                $"Case {c.Reference} withdrawn");

            TempData["Success"] = "Allegation withdrawn. The file stays on record showing it was not pursued.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Code of conduct
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Offences()
        {
            if (!IsHr) return AccessDenied();

            ViewBag.Offences = await _db.DisciplinaryOffences.AsNoTracking()
                .OrderBy(o => o.DisplayOrder).ThenBy(o => o.Name)
                .ToListAsync();

            ViewBag.UseCounts = await _db.DisciplinaryCases.AsNoTracking()
                .Where(c => c.OffenceId != null)
                .GroupBy(c => c.OffenceId!.Value)
                .Select(g => new { OffenceId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OffenceId, x => x.Count);

            return View();
        }

        public async Task<IActionResult> EditOffence(int? id)
        {
            if (!IsHr) return AccessDenied();

            if (id == null) return View(new DisciplinaryOffence());

            var o = await _db.DisciplinaryOffences.FindAsync(id.Value);
            if (o == null) return NotFound();
            return View(o);
        }

        [HttpPost]
        public async Task<IActionResult> EditOffence(DisciplinaryOffence model)
        {
            if (!IsHr) return AccessDenied();
            if (!ModelState.IsValid) return View(model);

            var code = model.Code.Trim().ToUpperInvariant();
            if (await _db.DisciplinaryOffences.AnyAsync(o => o.Code == code && o.Id != model.Id))
            {
                ModelState.AddModelError(nameof(model.Code), "Another offence already uses that code.");
                return View(model);
            }

            model.Code = code;

            if (model.Id == 0)
            {
                _db.DisciplinaryOffences.Add(model);
            }
            else
            {
                var existing = await _db.DisciplinaryOffences.FindAsync(model.Id);
                if (existing == null) return NotFound();

                existing.Code = model.Code;
                existing.Name = model.Name;
                existing.Description = model.Description;
                existing.Authority = model.Authority;
                existing.Seriousness = model.Seriousness;
                existing.DismissableFirstOffence = model.DismissableFirstOffence;
                existing.DefaultFirstPenalty = model.DefaultFirstPenalty;
                existing.WarningValidityMonths = model.WarningValidityMonths;
                existing.IsActive = model.IsActive;
                existing.DisplayOrder = model.DisplayOrder;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Offence saved.";
            return RedirectToAction(nameof(Offences));
        }
    }
}
