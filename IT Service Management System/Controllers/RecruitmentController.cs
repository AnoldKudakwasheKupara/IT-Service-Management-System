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
    /// Recruitment, from requisition through advert and selection to an accepted offer and an
    /// employee record.
    /// <para>
    /// Section 5 of the Labour Act [Chapter 28:01] makes discrimination in recruitment unlawful.
    /// What defends a hiring decision is a trail: criteria fixed before applications were seen,
    /// everyone scored against the same ones, and a recorded reason for every rejection. The
    /// controller requires that reason rather than asking for it.
    /// </para>
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "HR", "GeneralManager", "DepartmentManager")]
    public class RecruitmentController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly RecruitmentService _recruitment;
        private readonly AuditService _audit;

        public RecruitmentController(ApplicationDbContext db, RecruitmentService recruitment, AuditService audit)
        {
            _db = db; _recruitment = recruitment; _audit = audit;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool IsHr => Roles.IsFullAccess(Role) || Role == Roles.HR;
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        private const int PageSize = 20;

        private async Task LoadPickersAsync(int? departmentId = null, int? employeeId = null)
        {
            ViewBag.DepartmentList = new SelectList(
                await _db.Departments.AsNoTracking().OrderBy(d => d.Name)
                    .Select(d => new { d.Id, d.Name }).ToListAsync(),
                "Id", "Name", departmentId);

            ViewBag.EmployeeList = new SelectList(
                await _db.Employees.AsNoTracking().Where(e => !e.IsDeleted)
                    .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                    .Select(e => new { e.Id, Label = e.FirstName + " " + e.LastName + " · " + e.EmployeeNumber })
                    .ToListAsync(),
                "Id", "Label", employeeId);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Dashboard
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Index()
        {
            ViewBag.Pipeline = await _recruitment.PipelineAsync();

            ViewBag.OpenVacancies = await _db.Vacancies.AsNoTracking()
                .Include(v => v.Requisition)
                .Where(v => v.Status == VacancyStatus.Open
                         || v.Status == VacancyStatus.Shortlisting
                         || v.Status == VacancyStatus.Interviewing)
                .OrderBy(v => v.CloseDate)
                .ToListAsync();

            ViewBag.ApplicationCounts = await _db.JobApplications.AsNoTracking()
                .GroupBy(a => a.VacancyId)
                .Select(g => new { VacancyId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VacancyId, x => x.Count);

            ViewBag.PendingApproval = await _db.JobRequisitions.AsNoTracking()
                .Include(r => r.Department)
                .Where(r => r.Status == RequisitionStatus.PendingApproval)
                .OrderBy(r => r.RequiredByDate)
                .ToListAsync();

            ViewBag.UpcomingInterviews = await _db.CandidateInterviews.AsNoTracking()
                .Include(i => i.Application).ThenInclude(a => a!.Vacancy)
                .Where(i => !i.Held && i.ScheduledFor >= DateTime.Today)
                .OrderBy(i => i.ScheduledFor).Take(10)
                .ToListAsync();

            ViewBag.OutstandingOffers = await _db.JobOffers.AsNoTracking()
                .Include(o => o.Application)
                .Where(o => o.Status == OfferStatus.Issued)
                .OrderBy(o => o.ExpiryDate)
                .ToListAsync();

            return View();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Requisitions
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Requisitions(RequisitionStatus? status, int page = 1)
        {
            var query = _db.JobRequisitions.AsNoTracking()
                .Include(r => r.Department).Include(r => r.RaisedBy)
                .AsQueryable();

            if (status.HasValue) query = query.Where(r => r.Status == status.Value);

            var total = await query.CountAsync();
            if (page < 1) page = 1;

            ViewBag.Requisitions = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

            ViewBag.Paging = new PagingInfo { Page = page, PageSize = PageSize, TotalCount = total };
            ViewBag.Status = status;
            return View();
        }

        public async Task<IActionResult> EditRequisition(int? id)
        {
            if (id == null)
            {
                await LoadPickersAsync();
                return View(new JobRequisition { RequiredByDate = DateTime.Today.AddDays(45) });
            }

            var r = await _db.JobRequisitions.FindAsync(id.Value);
            if (r == null) return NotFound();

            if (r.Status is RequisitionStatus.Approved or RequisitionStatus.Advertised)
            {
                TempData["Warning"] = "This requisition is already approved. Changing the post now means "
                                    + "the advert and the approval no longer describe the same job.";
            }

            await LoadPickersAsync(r.DepartmentId, r.ReportsToEmployeeId);
            return View(r);
        }

        [HttpPost]
        public async Task<IActionResult> EditRequisition(JobRequisition model, bool submitForApproval = false)
        {
            if (model.EmploymentType == EmploymentType.FixedTerm && model.ContractEndDate == null)
                ModelState.AddModelError(nameof(model.ContractEndDate),
                    "A fixed-term contract needs an end date.");

            if (model.SalaryMin.HasValue && model.SalaryMax.HasValue && model.SalaryMin > model.SalaryMax)
                ModelState.AddModelError(nameof(model.SalaryMax),
                    "The maximum cannot be below the minimum.");

            if (!ModelState.IsValid)
            {
                await LoadPickersAsync(model.DepartmentId, model.ReportsToEmployeeId);
                return View(model);
            }

            if (model.Id == 0)
            {
                model.RaisedById = Uid;
                model.Status = submitForApproval ? RequisitionStatus.PendingApproval : RequisitionStatus.Draft;
                _db.JobRequisitions.Add(model);
            }
            else
            {
                var existing = await _db.JobRequisitions.FindAsync(model.Id);
                if (existing == null) return NotFound();

                existing.JobTitle = model.JobTitle;
                existing.DepartmentId = model.DepartmentId;
                existing.Grade = model.Grade;
                existing.Location = model.Location;
                existing.Positions = model.Positions;
                existing.EmploymentType = model.EmploymentType;
                existing.ContractEndDate = model.ContractEndDate;
                existing.ReplacingEmployeeId = model.ReplacingEmployeeId;
                existing.ReportsToEmployeeId = model.ReportsToEmployeeId;
                existing.Purpose = model.Purpose;
                existing.EssentialRequirements = model.EssentialRequirements;
                existing.DesirableRequirements = model.DesirableRequirements;
                existing.SalaryMin = model.SalaryMin;
                existing.SalaryMax = model.SalaryMax;
                existing.Currency = model.Currency;
                existing.RequiredByDate = model.RequiredByDate;
                existing.UpdatedAt = DateTime.Now;

                if (submitForApproval && existing.Status == RequisitionStatus.Draft)
                    existing.Status = RequisitionStatus.PendingApproval;

                model = existing;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = submitForApproval
                ? "Requisition submitted for approval. Nothing should be advertised until it is approved."
                : "Requisition saved.";

            return RedirectToAction(nameof(Requisitions));
        }

        [HttpPost]
        public async Task<IActionResult> DecideRequisition(int id, bool approve, string? note)
        {
            if (!IsHr) return AccessDenied();

            var r = await _db.JobRequisitions.FindAsync(id);
            if (r == null) return NotFound();

            if (r.RaisedById == Uid && approve)
            {
                TempData["Error"] = "You raised this requisition, so you cannot approve it. Headcount "
                                  + "approval means nothing if the person asking is the person agreeing.";
                return RedirectToAction(nameof(Requisitions));
            }

            if (!approve && string.IsNullOrWhiteSpace(note))
            {
                TempData["Error"] = "Give a reason for the rejection.";
                return RedirectToAction(nameof(Requisitions));
            }

            r.Status = approve ? RequisitionStatus.Approved : RequisitionStatus.Rejected;
            r.ApprovedById = Uid;
            r.ApprovedAt = DateTime.Now;
            r.DecisionNote = note;
            r.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            await _audit.LogAsync(approve ? "Approved" : "Rejected", nameof(JobRequisition), r.Id,
                $"Requisition {r.Reference} — {r.JobTitle}");

            TempData["Success"] = approve
                ? $"{r.Reference} approved. It can now be advertised."
                : $"{r.Reference} rejected.";

            return RedirectToAction(nameof(Requisitions));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Vacancies
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Vacancies(bool openOnly = true)
        {
            var query = _db.Vacancies.AsNoTracking().Include(v => v.Requisition).AsQueryable();

            if (openOnly)
                query = query.Where(v => v.Status != VacancyStatus.Filled
                                      && v.Status != VacancyStatus.Cancelled);

            ViewBag.Vacancies = await query.OrderByDescending(v => v.OpenDate).ToListAsync();
            ViewBag.Counts = await _db.JobApplications.AsNoTracking()
                .GroupBy(a => a.VacancyId)
                .Select(g => new { VacancyId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VacancyId, x => x.Count);
            ViewBag.OpenOnly = openOnly;
            return View();
        }

        public async Task<IActionResult> Advertise(int requisitionId)
        {
            var r = await _db.JobRequisitions.AsNoTracking()
                .Include(x => x.Department)
                .FirstOrDefaultAsync(x => x.Id == requisitionId);

            if (r == null) return NotFound();

            if (r.Status != RequisitionStatus.Approved)
            {
                TempData["Error"] = "Only an approved requisition can be advertised.";
                return RedirectToAction(nameof(Requisitions));
            }

            ViewBag.Requisition = r;

            // The advert is pre-filled from the requisition so the published wording and the approved
            // post start out saying the same thing.
            return View(new Vacancy
            {
                RequisitionId = r.Id,
                Title = r.JobTitle,
                AdvertText = string.Join("\n\n", new[]
                {
                    $"{r.JobTitle} — {r.Department?.Name ?? "Head office"}{(r.Location == null ? "" : $", {r.Location}")}",
                    r.Purpose,
                    string.IsNullOrWhiteSpace(r.EssentialRequirements) ? null
                        : "Essential requirements:\n" + r.EssentialRequirements,
                    string.IsNullOrWhiteSpace(r.DesirableRequirements) ? null
                        : "Desirable:\n" + r.DesirableRequirements,
                    "Applications are considered on merit against the requirements of the post. "
                    + "We do not discriminate on any ground listed in section 5 of the Labour Act."
                }.Where(s => !string.IsNullOrWhiteSpace(s)))
            });
        }

        [HttpPost]
        public async Task<IActionResult> Advertise(Vacancy model)
        {
            if (model.CloseDate <= model.OpenDate)
                ModelState.AddModelError(nameof(model.CloseDate), "The closing date must be after the opening date.");

            if (!ModelState.IsValid)
            {
                ViewBag.Requisition = await _db.JobRequisitions.AsNoTracking()
                    .Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == model.RequisitionId);
                return View(model);
            }

            model.Status = VacancyStatus.Open;
            _db.Vacancies.Add(model);

            var req = await _db.JobRequisitions.FindAsync(model.RequisitionId);
            if (req != null) req.Status = RequisitionStatus.Advertised;

            await _db.SaveChangesAsync();
            await _audit.LogAsync("Advertised", nameof(Vacancy), model.Id, $"{model.Title} advertised");

            TempData["Success"] = "Vacancy open. Set the selection criteria before the first application "
                                + "is read — criteria decided afterwards are criteria nobody can defend.";

            return RedirectToAction(nameof(Criteria), new { id = model.Id });
        }

        /// <summary>The scoring framework. Set before applications are seen, by design.</summary>
        public async Task<IActionResult> Criteria(int id)
        {
            var v = await _db.Vacancies.AsNoTracking()
                .Include(x => x.Requisition)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (v == null) return NotFound();

            ViewBag.Vacancy = v;
            ViewBag.Criteria = await _db.SelectionCriteria.AsNoTracking()
                .Where(c => c.VacancyId == id)
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id).ToListAsync();

            ViewBag.ApplicationCount = await _db.JobApplications.CountAsync(a => a.VacancyId == id);
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddCriterion(int vacancyId, string name, string? descriptor,
            int weight = 1, bool isEssential = true)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Name the criterion.";
                return RedirectToAction(nameof(Criteria), new { id = vacancyId });
            }

            var next = await _db.SelectionCriteria.Where(c => c.VacancyId == vacancyId)
                .MaxAsync(c => (int?)c.DisplayOrder) ?? 0;

            _db.SelectionCriteria.Add(new SelectionCriterion
            {
                VacancyId = vacancyId,
                Name = name.Trim(),
                Descriptor = descriptor,
                Weight = Math.Clamp(weight, 1, 10),
                IsEssential = isEssential,
                DisplayOrder = next + 1
            });

            await _db.SaveChangesAsync();
            TempData["Success"] = "Criterion added.";
            return RedirectToAction(nameof(Criteria), new { id = vacancyId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCriterion(int id)
        {
            var c = await _db.SelectionCriteria.FindAsync(id);
            if (c == null) return NotFound();

            var vacancyId = c.VacancyId;

            if (await _db.CandidateScores.AnyAsync(s => s.CriterionId == id))
            {
                TempData["Error"] = "Candidates have already been scored on this criterion. Removing it "
                                  + "now would change the basis on which they were compared.";
                return RedirectToAction(nameof(Criteria), new { id = vacancyId });
            }

            _db.SelectionCriteria.Remove(c);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Criterion removed.";
            return RedirectToAction(nameof(Criteria), new { id = vacancyId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Applications
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Applications(int id, ApplicationStatus? status)
        {
            var v = await _db.Vacancies.AsNoTracking()
                .Include(x => x.Requisition)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (v == null) return NotFound();

            var query = _db.JobApplications.AsNoTracking().Where(a => a.VacancyId == id);
            if (status.HasValue) query = query.Where(a => a.Status == status.Value);

            ViewBag.Vacancy = v;
            ViewBag.Applications = await query
                .OrderBy(a => a.LastName).ThenBy(a => a.FirstName).ToListAsync();
            ViewBag.Status = status;
            ViewBag.Results = (await _recruitment.ScoreVacancyAsync(id))
                .ToDictionary(r => r.ApplicationId);
            ViewBag.Criteria = await _db.SelectionCriteria.AsNoTracking()
                .Where(c => c.VacancyId == id).OrderBy(c => c.DisplayOrder).ToListAsync();
            ViewBag.Pipeline = await _recruitment.PipelineAsync(id);

            return View();
        }

        public async Task<IActionResult> AddApplication(int vacancyId)
        {
            var v = await _db.Vacancies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == vacancyId);
            if (v == null) return NotFound();

            ViewBag.Vacancy = v;
            await LoadPickersAsync();
            return View(new JobApplication { VacancyId = vacancyId });
        }

        [HttpPost]
        public async Task<IActionResult> AddApplication(JobApplication model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Vacancy = await _db.Vacancies.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == model.VacancyId);
                await LoadPickersAsync();
                return View(model);
            }

            var vacancy = await _db.Vacancies.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == model.VacancyId);

            // A late application is recorded rather than silently dropped, and flagged so the panel
            // decides consciously whether to consider it.
            var late = vacancy != null && DateTime.Today > vacancy.CloseDate;

            model.ReceivedAt = DateTime.Now;
            model.Status = ApplicationStatus.Received;
            _db.JobApplications.Add(model);
            await _db.SaveChangesAsync();

            TempData[late ? "Warning" : "Success"] = late
                ? $"{model.FullName} recorded, but the vacancy closed on {vacancy!.CloseDate:d MMM yyyy}. "
                + "Whether to consider a late application is a decision to take for all of them, not one."
                : $"{model.FullName} recorded.";

            return RedirectToAction(nameof(Applications), new { id = model.VacancyId });
        }

        public async Task<IActionResult> Candidate(int id)
        {
            var a = await _db.JobApplications.AsNoTracking()
                .Include(x => x.Vacancy).ThenInclude(v => v!.Requisition)
                .Include(x => x.Employee)
                .Include(x => x.Interviews)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (a == null) return NotFound();

            ViewBag.Application = a;
            ViewBag.Criteria = await _db.SelectionCriteria.AsNoTracking()
                .Where(c => c.VacancyId == a.VacancyId)
                .OrderBy(c => c.DisplayOrder).ToListAsync();

            ViewBag.Scores = await _db.CandidateScores.AsNoTracking()
                .Where(s => s.ApplicationId == id)
                .ToListAsync();

            ViewBag.Result = (await _recruitment.ScoreVacancyAsync(a.VacancyId))
                .FirstOrDefault(r => r.ApplicationId == id);

            ViewBag.Offer = await _db.JobOffers.AsNoTracking()
                .FirstOrDefaultAsync(o => o.ApplicationId == id);

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Score(int applicationId, int? interviewId,
            int[] criterionIds, int[] scores, string[] comments)
        {
            for (var i = 0; i < criterionIds.Length; i++)
            {
                await _recruitment.ScoreAsync(applicationId, criterionIds[i], interviewId,
                    i < scores.Length ? scores[i] : 0,
                    i < comments.Length ? comments[i] : null, Uid);
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Scores saved.";
            return RedirectToAction(nameof(Candidate), new { id = applicationId });
        }

        [HttpPost]
        public async Task<IActionResult> SetApplicationStatus(int id, ApplicationStatus status, string? reason)
        {
            var a = await _db.JobApplications.FindAsync(id);
            if (a == null) return NotFound();

            var isRejection = status is ApplicationStatus.NotShortlisted
                or ApplicationStatus.Unsuccessful;

            if (isRejection && string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Give the reason this application went no further. A rejection nobody "
                                  + "can explain is a rejection nobody can defend.";
                return RedirectToAction(nameof(Candidate), new { id });
            }

            a.Status = status;
            if (!string.IsNullOrWhiteSpace(reason)) a.DecisionReason = reason;
            a.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            TempData["Success"] = $"{a.FullName} marked {status}.";
            return RedirectToAction(nameof(Candidate), new { id });
        }

        // ── Interviews ───────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> ScheduleInterview(int applicationId, InterviewStage stage,
            DateTime scheduledFor, string? venue, string? panel)
        {
            var a = await _db.JobApplications.FindAsync(applicationId);
            if (a == null) return NotFound();

            _db.CandidateInterviews.Add(new CandidateInterview
            {
                ApplicationId = applicationId,
                Stage = stage,
                ScheduledFor = scheduledFor,
                Venue = venue,
                Panel = panel,
                ArrangedById = Uid
            });

            if (a.Status == ApplicationStatus.Received || a.Status == ApplicationStatus.Screened)
                a.Status = ApplicationStatus.Shortlisted;

            await _db.SaveChangesAsync();

            TempData[string.IsNullOrWhiteSpace(panel) ? "Warning" : "Success"] =
                string.IsNullOrWhiteSpace(panel)
                    ? "Interview scheduled, but no panel was recorded. A one-person interview is harder "
                    + "to defend than a panel, and impossible to corroborate."
                    : "Interview scheduled.";

            return RedirectToAction(nameof(Candidate), new { id = applicationId });
        }

        [HttpPost]
        public async Task<IActionResult> RecordInterview(int id, bool candidateAttended,
            string? notes, string? recommendation, InterviewOutcome outcome)
        {
            var interview = await _db.CandidateInterviews.FindAsync(id);
            if (interview == null) return NotFound();

            interview.Held = true;
            interview.CandidateAttended = candidateAttended;
            interview.Notes = notes;
            interview.Recommendation = recommendation;
            interview.Outcome = outcome;

            var a = await _db.JobApplications.FindAsync(interview.ApplicationId);
            if (a != null && a.Status == ApplicationStatus.Shortlisted)
                a.Status = ApplicationStatus.Interviewed;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Interview recorded.";
            return RedirectToAction(nameof(Candidate), new { id = interview.ApplicationId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Offers
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Offer(int applicationId)
        {
            if (!IsHr) return AccessDenied();

            var a = await _db.JobApplications.AsNoTracking()
                .Include(x => x.Vacancy).ThenInclude(v => v!.Requisition)
                .FirstOrDefaultAsync(x => x.Id == applicationId);

            if (a == null) return NotFound();

            var existing = await _db.JobOffers.FirstOrDefaultAsync(o => o.ApplicationId == applicationId);
            if (existing != null) return View(existing);

            var req = a.Vacancy?.Requisition;

            ViewBag.Application = a;
            return View(new JobOffer
            {
                ApplicationId = applicationId,
                JobTitle = req?.JobTitle ?? a.Vacancy?.Title ?? string.Empty,
                Grade = req?.Grade,
                Location = req?.Location,
                EmploymentType = req?.EmploymentType ?? EmploymentType.Permanent,
                ContractEndDate = req?.ContractEndDate,
                BasicSalary = req?.SalaryMin ?? 0m,
                Currency = req?.Currency ?? "USD",
                ExpiryDate = DateTime.Today.AddDays(7)
            });
        }

        [HttpPost]
        public async Task<IActionResult> Offer(JobOffer model, bool issue = false)
        {
            if (!IsHr) return AccessDenied();

            if (model.EmploymentType == EmploymentType.FixedTerm && model.ContractEndDate == null)
                ModelState.AddModelError(nameof(model.ContractEndDate),
                    "A fixed-term contract needs an end date.");

            if (model.ProbationMonths > 3 && model.EmploymentType == EmploymentType.Permanent)
                ModelState.AddModelError(nameof(model.ProbationMonths),
                    "Probation on a permanent contract is limited to three months, and may only be "
                    + "served once with the same employer for the same class of work.");

            if (!ModelState.IsValid)
            {
                ViewBag.Application = await _db.JobApplications.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == model.ApplicationId);
                return View(model);
            }

            JobOffer offer;
            if (model.Id == 0)
            {
                offer = model;
                _db.JobOffers.Add(offer);
            }
            else
            {
                offer = (await _db.JobOffers.FindAsync(model.Id))!;
                if (offer == null) return NotFound();

                offer.JobTitle = model.JobTitle;
                offer.Grade = model.Grade;
                offer.Location = model.Location;
                offer.EmploymentType = model.EmploymentType;
                offer.BasicSalary = model.BasicSalary;
                offer.Currency = model.Currency;
                offer.StartDate = model.StartDate;
                offer.ProbationMonths = model.ProbationMonths;
                offer.ContractEndDate = model.ContractEndDate;
                offer.OtherTerms = model.OtherTerms;
                offer.ExpiryDate = model.ExpiryDate;
            }

            if (issue)
            {
                offer.Status = OfferStatus.Issued;
                offer.IssuedDate = DateTime.Today;
                offer.IssuedById = Uid;

                var app = await _db.JobApplications.FindAsync(offer.ApplicationId);
                if (app != null) app.Status = ApplicationStatus.OfferMade;
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = issue
                ? "Offer issued. The terms recorded here become the written particulars of employment, "
                + "so they carry into the employee record on acceptance rather than being retyped."
                : "Offer saved as a draft.";

            return RedirectToAction(nameof(Candidate), new { id = offer.ApplicationId });
        }

        [HttpPost]
        public async Task<IActionResult> RespondToOffer(int id, bool accepted, string? note)
        {
            if (!IsHr) return AccessDenied();

            var offer = await _db.JobOffers.Include(o => o.Application).FirstOrDefaultAsync(o => o.Id == id);
            if (offer == null) return NotFound();

            offer.Status = accepted ? OfferStatus.Accepted : OfferStatus.Declined;
            offer.RespondedDate = DateTime.Today;
            offer.ResponseNote = note;

            if (offer.Application != null)
                offer.Application.Status = accepted
                    ? ApplicationStatus.OfferAccepted
                    : ApplicationStatus.OfferDeclined;

            await _db.SaveChangesAsync();

            TempData["Success"] = accepted
                ? "Offer accepted. Create the employee record next."
                : "Offer declined.";

            return RedirectToAction(nameof(Candidate), new { id = offer.ApplicationId });
        }

        [HttpPost]
        public async Task<IActionResult> Hire(int id, string employeeNumber)
        {
            if (!IsHr) return AccessDenied();

            var result = await _recruitment.HireAsync(id, employeeNumber, Uid);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;

            if (result.Succeeded)
                await _audit.LogAsync("Hired", nameof(Employee), result.EmployeeId,
                    $"Employee record created from offer {id}");

            var offer = await _db.JobOffers.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
            return RedirectToAction(nameof(Candidate), new { id = offer?.ApplicationId ?? 0 });
        }
    }
}
