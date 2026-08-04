using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models.Hr;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Performance appraisal and improvement plans.
    /// <para>
    /// Section 12B of the Labour Act [Chapter 28:01] treats a dismissal for poor performance as
    /// unfair unless the employee was told the standard required, given a reasonable opportunity to
    /// meet it, and failed to do so. Everything here is arranged around being able to show those
    /// three things: objectives set at the start of the cycle rather than at review time, the
    /// employee's own account kept alongside the manager's, and improvement plans that require a
    /// written standard and a real review date.
    /// </para>
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "HR", "GeneralManager", "DepartmentManager",
                   "ProjectManager", "TeamLead", "Finance", "Procurement", "Employee",
                   "SupportAgent", "Development", "QualityManager")]
    public class AppraisalsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public AppraisalsController(ApplicationDbContext db, AuditService audit)
        {
            _db = db; _audit = audit;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool IsHr => Roles.IsFullAccess(Role) || Role == Roles.HR;
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        private async Task<Employee?> MeAsync() =>
            await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == Uid);

        private async Task<bool> ManagesAsync(int employeeId, Employee? me)
        {
            if (me == null) return false;
            var managerId = await _db.Employees.AsNoTracking()
                .Where(e => e.Id == employeeId).Select(e => e.ManagerId).FirstOrDefaultAsync();
            return managerId == me.Id;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  My appraisals
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Index()
        {
            var me = await MeAsync();
            if (me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");

            ViewBag.Employee = me;

            ViewBag.Mine = await _db.Appraisals.AsNoTracking()
                .Include(a => a.Cycle).Include(a => a.Reviewer).Include(a => a.Objectives)
                .Where(a => a.EmployeeId == me.Id)
                .OrderByDescending(a => a.Cycle!.PeriodEnd)
                .ToListAsync();

            // What this person owes as a reviewer.
            var reportIds = await _db.Employees.AsNoTracking()
                .Where(e => e.ManagerId == me.Id && !e.IsDeleted).Select(e => e.Id).ToListAsync();

            ViewBag.ToReview = await _db.Appraisals.AsNoTracking()
                .Include(a => a.Cycle).Include(a => a.Employee).Include(a => a.Objectives)
                .Where(a => reportIds.Contains(a.EmployeeId) && a.Status != AppraisalStatus.Closed)
                .OrderBy(a => a.Cycle!.ManagerReviewDue)
                .ToListAsync();

            ViewBag.MyPips = await _db.PerformanceImprovementPlans.AsNoTracking()
                .Include(p => p.Manager)
                .Where(p => p.EmployeeId == me.Id)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            ViewBag.IsHr = IsHr;
            return View();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Cycles
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Cycles()
        {
            if (!IsHr) return AccessDenied();

            ViewBag.Cycles = await _db.AppraisalCycles.AsNoTracking()
                .Include(c => c.Appraisals)
                .OrderByDescending(c => c.PeriodEnd)
                .ToListAsync();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateCycle(string name, DateTime periodStart, DateTime periodEnd,
            DateTime? selfAssessmentDue, DateTime? managerReviewDue, bool isProbationReview = false)
        {
            if (!IsHr) return AccessDenied();

            if (string.IsNullOrWhiteSpace(name) || periodEnd <= periodStart)
            {
                TempData["Error"] = "A cycle needs a name and a period that ends after it starts.";
                return RedirectToAction(nameof(Cycles));
            }

            var cycle = new AppraisalCycle
            {
                Name = name.Trim(),
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                SelfAssessmentDue = selfAssessmentDue,
                ManagerReviewDue = managerReviewDue,
                IsProbationReview = isProbationReview,
                Status = AppraisalCycleStatus.ObjectiveSetting
            };

            _db.AppraisalCycles.Add(cycle);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Cycle created. Enrol people and set their objectives now — an objective "
                                + "invented at review time is not an objective, it is a justification.";

            return RedirectToAction(nameof(Cycle), new { id = cycle.Id });
        }

        public async Task<IActionResult> Cycle(int id)
        {
            if (!IsHr) return AccessDenied();

            var cycle = await _db.AppraisalCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (cycle == null) return NotFound();

            ViewBag.Cycle = cycle;
            ViewBag.Appraisals = await _db.Appraisals.AsNoTracking()
                .Include(a => a.Employee).Include(a => a.Reviewer).Include(a => a.Objectives)
                .Where(a => a.CycleId == id)
                .OrderBy(a => a.Employee!.LastName)
                .ToListAsync();

            var enrolled = await _db.Appraisals.AsNoTracking()
                .Where(a => a.CycleId == id).Select(a => a.EmployeeId).ToListAsync();

            ViewBag.NotEnrolled = await _db.Employees.AsNoTracking()
                .Where(e => !e.IsDeleted && !enrolled.Contains(e.Id))
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .ToListAsync();

            // The rating spread, so moderation has something to look at. A distribution where
            // everyone is outstanding tells you about the process, not the people.
            ViewBag.Distribution = await _db.Appraisals.AsNoTracking()
                .Where(a => a.CycleId == id && a.OverallRating != null)
                .GroupBy(a => a.ModeratedRating ?? a.OverallRating!.Value)
                .Select(g => new { Rating = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Rating, x => x.Count);

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Enrol(int cycleId, int[] employeeIds, bool everyone = false)
        {
            if (!IsHr) return AccessDenied();

            var targets = everyone
                ? await _db.Employees.AsNoTracking().Where(e => !e.IsDeleted).Select(e => e.Id).ToListAsync()
                : employeeIds.ToList();

            var already = await _db.Appraisals.Where(a => a.CycleId == cycleId)
                .Select(a => a.EmployeeId).ToListAsync();

            var managers = await _db.Employees.AsNoTracking()
                .Where(e => targets.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.ManagerId);

            var added = 0;
            foreach (var employeeId in targets.Except(already))
            {
                _db.Appraisals.Add(new Appraisal
                {
                    CycleId = cycleId,
                    EmployeeId = employeeId,
                    ReviewerId = managers.GetValueOrDefault(employeeId),
                    Status = AppraisalStatus.NotStarted
                });
                added++;
            }

            await _db.SaveChangesAsync();

            var noReviewer = targets.Except(already).Count(id => managers.GetValueOrDefault(id) == null);

            TempData[noReviewer > 0 ? "Warning" : "Success"] = noReviewer > 0
                ? $"{added} enrolled, but {noReviewer} of them have no line manager on the register, so "
                + "nobody is set to review them. Fix the reporting line or set a reviewer by hand."
                : $"{added} enrolled.";

            return RedirectToAction(nameof(Cycle), new { id = cycleId });
        }

        [HttpPost]
        public async Task<IActionResult> SetCycleStatus(int id, AppraisalCycleStatus status)
        {
            if (!IsHr) return AccessDenied();

            var cycle = await _db.AppraisalCycles.FindAsync(id);
            if (cycle == null) return NotFound();

            if (status == AppraisalCycleStatus.Closed)
            {
                var unfinished = await _db.Appraisals
                    .CountAsync(a => a.CycleId == id && a.Status != AppraisalStatus.Closed);

                if (unfinished > 0)
                {
                    TempData["Error"] = $"{unfinished} appraisal(s) are not finished. Closing the cycle "
                                      + "now would leave them permanently half-written.";
                    return RedirectToAction(nameof(Cycle), new { id });
                }
            }

            cycle.Status = status;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Cycle moved to {status}.";
            return RedirectToAction(nameof(Cycle), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  An appraisal
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Details(int id)
        {
            var a = await _db.Appraisals.AsNoTracking()
                .Include(x => x.Cycle).Include(x => x.Employee).Include(x => x.Reviewer)
                .Include(x => x.ModeratedBy)
                .Include(x => x.Objectives)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (a == null) return NotFound();

            var me = await MeAsync();
            var isSubject = me != null && me.Id == a.EmployeeId;
            var isReviewer = me != null && (me.Id == a.ReviewerId || await ManagesAsync(a.EmployeeId, me));

            if (!IsHr && !isSubject && !isReviewer) return AccessDenied();

            ViewBag.Appraisal = a;
            ViewBag.IsHr = IsHr;
            ViewBag.IsSubject = isSubject;
            ViewBag.IsReviewer = isReviewer;

            ViewBag.Pips = await _db.PerformanceImprovementPlans.AsNoTracking()
                .Where(p => p.EmployeeId == a.EmployeeId)
                .OrderByDescending(p => p.StartDate).ToListAsync();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddObjective(int appraisalId, string title, string? successMeasure,
            decimal weight, DateTime? targetDate)
        {
            var a = await _db.Appraisals.Include(x => x.Cycle).FirstOrDefaultAsync(x => x.Id == appraisalId);
            if (a == null) return NotFound();

            var me = await MeAsync();
            if (!IsHr && !(me != null && (me.Id == a.ReviewerId || await ManagesAsync(a.EmployeeId, me))))
                return AccessDenied();

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] = "Name the objective.";
                return RedirectToAction(nameof(Details), new { id = appraisalId });
            }

            // An objective added after the period has started is recorded as such rather than being
            // presented as if it had been agreed up front.
            var upFront = a.Cycle == null || DateTime.Today <= a.Cycle.PeriodStart.AddDays(45);

            var next = await _db.AppraisalObjectives.Where(o => o.AppraisalId == appraisalId)
                .MaxAsync(o => (int?)o.DisplayOrder) ?? 0;

            _db.AppraisalObjectives.Add(new AppraisalObjective
            {
                AppraisalId = appraisalId,
                Title = title.Trim(),
                SuccessMeasure = successMeasure,
                Weight = Math.Clamp(weight, 0, 100),
                TargetDate = targetDate,
                AgreedUpFront = upFront,
                DisplayOrder = next + 1
            });

            await _db.SaveChangesAsync();

            TempData[upFront ? "Success" : "Warning"] = upFront
                ? "Objective added."
                : "Objective added, but flagged as set after the cycle began. An objective an employee "
                + "did not know about cannot fairly be used to mark them down.";

            return RedirectToAction(nameof(Details), new { id = appraisalId });
        }

        [HttpPost]
        public async Task<IActionResult> ScoreObjectives(int appraisalId, int[] objectiveIds,
            decimal?[] achievements, string[] evidence)
        {
            var a = await _db.Appraisals.FirstOrDefaultAsync(x => x.Id == appraisalId);
            if (a == null) return NotFound();

            var me = await MeAsync();
            if (!IsHr && !(me != null && (me.Id == a.ReviewerId || await ManagesAsync(a.EmployeeId, me))))
                return AccessDenied();

            var objectives = await _db.AppraisalObjectives
                .Where(o => o.AppraisalId == appraisalId).ToListAsync();

            for (var i = 0; i < objectiveIds.Length; i++)
            {
                var o = objectives.FirstOrDefault(x => x.Id == objectiveIds[i]);
                if (o == null) continue;

                if (i < achievements.Length) o.AchievementPercent = achievements[i];
                if (i < evidence.Length) o.Evidence = evidence[i];
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Objectives scored.";
            return RedirectToAction(nameof(Details), new { id = appraisalId });
        }

        [HttpPost]
        public async Task<IActionResult> SelfAssess(int id, string? achievements, string? challenges,
            string? developmentNeeds)
        {
            var a = await _db.Appraisals.FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();

            var me = await MeAsync();
            if (me == null || me.Id != a.EmployeeId)
            {
                TempData["Error"] = "A self-assessment is the employee's own account. Nobody else can write it.";
                return AccessDenied();
            }

            if (a.Status == AppraisalStatus.Closed)
            {
                TempData["Error"] = "This appraisal is closed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            a.SelfAchievements = achievements;
            a.SelfChallenges = challenges;
            a.SelfDevelopmentNeeds = developmentNeeds;
            a.SelfAssessedAt = DateTime.Now;

            if (a.Status is AppraisalStatus.NotStarted or AppraisalStatus.SelfAssessment)
                a.Status = AppraisalStatus.ReviewerAssessment;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Self-assessment saved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Review(int id, string? comments, string? developmentPlan,
            PerformanceRating rating, string? ratingReasons)
        {
            var a = await _db.Appraisals.Include(x => x.Objectives).FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();

            var me = await MeAsync();
            if (!IsHr && !(me != null && (me.Id == a.ReviewerId || await ManagesAsync(a.EmployeeId, me))))
                return AccessDenied();

            if (string.IsNullOrWhiteSpace(ratingReasons))
            {
                TempData["Error"] = "Give reasons for the rating. A number with nothing behind it is an "
                                  + "opinion, and it will not survive being questioned.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (a.SelfAssessedAt == null)
            {
                TempData["Warning"] = "Rated before the employee wrote their own account. It is still "
                                    + "recorded, but a review with no self-assessment reads as one-sided "
                                    + "if it is ever relied on.";
            }

            a.ReviewerComments = comments;
            a.DevelopmentPlan = developmentPlan;
            a.OverallRating = rating;
            a.RatingReasons = ratingReasons;
            a.ReviewedAt = DateTime.Now;
            a.ReviewerId ??= me?.Id;
            a.Status = AppraisalStatus.AwaitingModeration;

            await _db.SaveChangesAsync();
            await _audit.LogAsync("Reviewed", nameof(Appraisal), a.Id,
                $"Appraisal {a.Reference} rated {rating}");

            TempData["Success"] = "Review recorded.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Moderate(int id, PerformanceRating? moderatedRating, string? reasons)
        {
            if (!IsHr) return AccessDenied();

            var a = await _db.Appraisals.FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();

            if (a.OverallRating == null)
            {
                TempData["Error"] = "There is no rating to moderate yet.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (moderatedRating.HasValue && moderatedRating != a.OverallRating
                && string.IsNullOrWhiteSpace(reasons))
            {
                TempData["Error"] = "Changing a manager's rating needs a reason on the record. The "
                                  + "original rating is kept either way.";
                return RedirectToAction(nameof(Details), new { id });
            }

            a.ModeratedRating = moderatedRating;
            a.ModerationReasons = reasons;
            a.ModeratedById = Uid;
            a.ModeratedAt = DateTime.Now;
            a.Status = AppraisalStatus.AwaitingAcknowledgement;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Moderation recorded. The employee can now see it and respond.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Acknowledge(int id, string? comments, bool disagrees = false)
        {
            var a = await _db.Appraisals.FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();

            var me = await MeAsync();
            if (me == null || me.Id != a.EmployeeId) return AccessDenied();

            a.EmployeeAcknowledged = true;
            a.EmployeeComments = comments;
            a.EmployeeDisagrees = disagrees;
            a.AcknowledgedAt = DateTime.Now;
            a.Status = AppraisalStatus.Closed;

            await _db.SaveChangesAsync();

            TempData["Success"] = disagrees
                ? "Acknowledged, with your disagreement recorded. Signing to say you have seen a review "
                + "is not the same as agreeing with it, and the record now says so."
                : "Acknowledged.";

            return RedirectToAction(nameof(Details), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Improvement plans
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Plans(PipStatus? status)
        {
            if (!IsHr)
            {
                var me = await MeAsync();
                if (me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");
            }

            var query = _db.PerformanceImprovementPlans.AsNoTracking()
                .Include(p => p.Employee).Include(p => p.Manager)
                .AsQueryable();

            if (!IsHr)
            {
                var me = await MeAsync();
                var reportIds = await _db.Employees.AsNoTracking()
                    .Where(e => e.ManagerId == me!.Id).Select(e => e.Id).ToListAsync();
                reportIds.Add(me!.Id);
                query = query.Where(p => reportIds.Contains(p.EmployeeId));
            }

            if (status.HasValue) query = query.Where(p => p.Status == status.Value);

            ViewBag.Plans = await query.OrderByDescending(p => p.StartDate).ToListAsync();
            ViewBag.Status = status;

            ViewBag.EmployeeList = new SelectList(
                await _db.Employees.AsNoTracking().Where(e => !e.IsDeleted)
                    .OrderBy(e => e.LastName)
                    .Select(e => new { e.Id, Label = e.FirstName + " " + e.LastName + " · " + e.EmployeeNumber })
                    .ToListAsync(),
                "Id", "Label");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreatePlan(int employeeId, string concern, string requiredStandard,
            string? supportOffered, DateTime startDate, DateTime reviewDate, int? appraisalId)
        {
            var me = await MeAsync();
            if (!IsHr && !await ManagesAsync(employeeId, me)) return AccessDenied();

            if (string.IsNullOrWhiteSpace(concern) || string.IsNullOrWhiteSpace(requiredStandard))
            {
                TempData["Error"] = "A plan needs the shortfall and the standard required, both in writing. "
                                  + "Section 12B of the Labour Act turns on the employee having been told "
                                  + "the standard — a plan that does not state it proves nothing.";
                return RedirectToAction(nameof(Plans));
            }

            if (reviewDate <= startDate)
            {
                TempData["Error"] = "The review date has to be after the start date.";
                return RedirectToAction(nameof(Plans));
            }

            var plan = new PerformanceImprovementPlan
            {
                EmployeeId = employeeId,
                AppraisalId = appraisalId,
                ManagerId = me?.Id,
                Concern = concern,
                RequiredStandard = requiredStandard,
                SupportOffered = supportOffered,
                StartDate = startDate,
                ReviewDate = reviewDate,
                Status = PipStatus.Open
            };

            _db.PerformanceImprovementPlans.Add(plan);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Opened", nameof(PerformanceImprovementPlan), plan.Id,
                $"Improvement plan {plan.Reference} opened");

            TempData[plan.IsShortNotice ? "Warning" : "Success"] = plan.IsShortNotice
                ? $"Plan opened, but it allows only {plan.DaysAllowed} days. A short plan is not forbidden — "
                + "some shortfalls can be fixed in a fortnight — but if this is later relied on, the "
                + "period has to be defensible as a real opportunity to improve on these facts."
                : $"Plan {plan.Reference} opened, reviewed on {plan.ReviewDate:d MMM yyyy}.";

            return RedirectToAction(nameof(Plans));
        }

        [HttpPost]
        public async Task<IActionResult> ClosePlan(int id, PipStatus outcome, string reasons, DateTime? newReviewDate)
        {
            var plan = await _db.PerformanceImprovementPlans.FindAsync(id);
            if (plan == null) return NotFound();

            var me = await MeAsync();
            if (!IsHr && !await ManagesAsync(plan.EmployeeId, me)) return AccessDenied();

            if (string.IsNullOrWhiteSpace(reasons))
            {
                TempData["Error"] = "Record what was actually achieved against the standard, and why the "
                                  + "outcome follows from it.";
                return RedirectToAction(nameof(Plans));
            }

            if (outcome == PipStatus.Extended)
            {
                if (newReviewDate == null || newReviewDate <= plan.ReviewDate)
                {
                    TempData["Error"] = "An extension needs a new review date later than the current one.";
                    return RedirectToAction(nameof(Plans));
                }

                plan.ReviewDate = newReviewDate.Value;
                plan.Outcome = $"{plan.Outcome}\nExtended {DateTime.Today:d MMM yyyy}: {reasons}".Trim();
                plan.Status = PipStatus.Open;
            }
            else
            {
                plan.Status = outcome;
                plan.Outcome = reasons;
                plan.ClosedAt = DateTime.Now;
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = outcome switch
            {
                PipStatus.Met => "Recorded as met. That closes the matter — a spent concern should not "
                               + "resurface as background in a later decision.",
                PipStatus.NotMet => "Recorded as not met. If this leads to a dismissal, what has to be "
                                  + "shown is that the standard was stated, the opportunity was real, and "
                                  + "the employee still did not meet it. All three are now on the record.",
                PipStatus.Extended => "Extended.",
                _ => "Plan closed."
            };

            return RedirectToAction(nameof(Plans));
        }

        [HttpPost]
        public async Task<IActionResult> PlanComments(int id, string comments)
        {
            var plan = await _db.PerformanceImprovementPlans.FindAsync(id);
            if (plan == null) return NotFound();

            var me = await MeAsync();
            if (me == null || me.Id != plan.EmployeeId) return AccessDenied();

            plan.EmployeeComments = comments;
            plan.DiscussedWithEmployee = true;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Your comments are on the plan.";
            return RedirectToAction(nameof(Plans));
        }
    }
}
