using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Hr;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Hr
{
    /// <summary>
    /// Recruitment support: weighted scoring against the criteria set before applications were seen,
    /// and the conversion of an accepted offer into an employee record.
    /// <para>
    /// Section 5 of the Labour Act [Chapter 28:01] makes discrimination in recruitment unlawful. The
    /// practical defence is not a policy document but a decision trail — criteria fixed in advance,
    /// every candidate scored against the same ones, and a recorded reason for every rejection. The
    /// service computes and surfaces that trail; it does not rank anyone automatically, because a
    /// number is not a decision.
    /// </para>
    /// </summary>
    public class RecruitmentService
    {
        private readonly ApplicationDbContext _db;

        public RecruitmentService(ApplicationDbContext db) { _db = db; }

        /// <summary>A candidate's weighted score against a vacancy's criteria, and what is missing.</summary>
        public record CandidateResult(
            int ApplicationId,
            string Name,
            bool IsInternal,
            decimal WeightedScore,
            decimal MaxScore,
            decimal Percentage,
            int Scored,
            int TotalCriteria,
            List<string> EssentialGaps);

        /// <summary>
        /// Score the whole field for a vacancy against its own criteria.
        /// <para>
        /// A criterion that has not been scored is left out of the maximum rather than counted as
        /// zero — otherwise a candidate the panel has not reached yet looks worse than one they
        /// scored badly, which is the opposite of the truth.
        /// </para>
        /// </summary>
        public async Task<List<CandidateResult>> ScoreVacancyAsync(int vacancyId)
        {
            var criteria = await _db.SelectionCriteria.AsNoTracking()
                .Where(c => c.VacancyId == vacancyId)
                .OrderBy(c => c.DisplayOrder).ToListAsync();

            var applications = await _db.JobApplications.AsNoTracking()
                .Where(a => a.VacancyId == vacancyId)
                .ToListAsync();

            var scores = await _db.CandidateScores.AsNoTracking()
                .Where(s => s.Application!.VacancyId == vacancyId)
                .ToListAsync();

            var results = new List<CandidateResult>();

            foreach (var app in applications)
            {
                var mine = scores.Where(s => s.ApplicationId == app.Id).ToList();

                decimal weighted = 0, max = 0;
                var gaps = new List<string>();

                foreach (var criterion in criteria)
                {
                    // Where a candidate was scored more than once on a criterion — screening then
                    // interview — the most recent score is the one that counts.
                    var latest = mine.Where(s => s.CriterionId == criterion.Id)
                        .OrderByDescending(s => s.ScoredAt).FirstOrDefault();

                    if (latest == null)
                    {
                        if (criterion.IsEssential) gaps.Add($"{criterion.Name}: not yet scored");
                        continue;
                    }

                    weighted += latest.Score * criterion.Weight;
                    max += 5m * criterion.Weight;

                    if (criterion.IsEssential && latest.Score == 0)
                        gaps.Add($"{criterion.Name}: scored zero on an essential requirement");
                }

                results.Add(new CandidateResult(
                    app.Id,
                    app.FullName,
                    app.IsInternal,
                    Math.Round(weighted, 2),
                    Math.Round(max, 2),
                    max == 0 ? 0 : Math.Round(weighted * 100m / max, 1),
                    mine.Select(s => s.CriterionId).Distinct().Count(),
                    criteria.Count,
                    gaps));
            }

            return results
                .OrderByDescending(r => r.Percentage)
                .ThenByDescending(r => r.Scored)
                .ToList();
        }

        /// <summary>
        /// Record a score, replacing any earlier score by the same person on the same criterion in
        /// the same interview rather than adding a second one.
        /// </summary>
        public async Task ScoreAsync(int applicationId, int criterionId, int? interviewId,
            int score, string? comment, int userId)
        {
            var existing = await _db.CandidateScores
                .FirstOrDefaultAsync(s => s.ApplicationId == applicationId
                                       && s.CriterionId == criterionId
                                       && s.InterviewId == interviewId);

            if (existing == null)
            {
                _db.CandidateScores.Add(new CandidateScore
                {
                    ApplicationId = applicationId,
                    CriterionId = criterionId,
                    InterviewId = interviewId,
                    Score = Math.Clamp(score, 0, 5),
                    Comment = comment,
                    ScoredById = userId,
                    ScoredAt = DateTime.Now
                });
            }
            else
            {
                existing.Score = Math.Clamp(score, 0, 5);
                existing.Comment = comment;
                existing.ScoredById = userId;
                existing.ScoredAt = DateTime.Now;
            }
        }

        public record PipelineCounts(int Received, int Shortlisted, int Interviewed, int Offered, int Hired, int Rejected);

        public async Task<PipelineCounts> PipelineAsync(int? vacancyId = null)
        {
            var query = _db.JobApplications.AsNoTracking().AsQueryable();
            if (vacancyId.HasValue) query = query.Where(a => a.VacancyId == vacancyId.Value);

            var byStatus = await query.GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            int C(params ApplicationStatus[] statuses) =>
                statuses.Sum(s => byStatus.GetValueOrDefault(s));

            return new PipelineCounts(
                C(ApplicationStatus.Received, ApplicationStatus.Screened),
                C(ApplicationStatus.Shortlisted),
                C(ApplicationStatus.Interviewed, ApplicationStatus.ReferenceCheck),
                C(ApplicationStatus.OfferMade, ApplicationStatus.OfferAccepted),
                C(ApplicationStatus.Hired),
                C(ApplicationStatus.NotShortlisted, ApplicationStatus.Unsuccessful,
                  ApplicationStatus.Withdrawn, ApplicationStatus.OfferDeclined));
        }

        public record HireResult(bool Succeeded, string Message, int? EmployeeId = null);

        /// <summary>
        /// Turn an accepted offer into an employee record.
        /// <para>
        /// The offer terms become the employment terms — that is the point of writing them down at
        /// offer stage. No login account is created here: HR owns employment data, administrators own
        /// credentials, and conflating the two is how HR ends up able to grant itself a role.
        /// </para>
        /// </summary>
        public async Task<HireResult> HireAsync(int offerId, string employeeNumber, int userId)
        {
            var offer = await _db.JobOffers
                .Include(o => o.Application).ThenInclude(a => a!.Vacancy).ThenInclude(v => v!.Requisition)
                .FirstOrDefaultAsync(o => o.Id == offerId);

            if (offer == null) return new HireResult(false, "Offer not found.");
            if (offer.Status != OfferStatus.Accepted)
                return new HireResult(false, "The offer has to be accepted before the employee record is created.");
            if (offer.CreatedEmployeeId.HasValue)
                return new HireResult(false, "An employee record has already been created from this offer.");

            employeeNumber = employeeNumber.Trim();
            if (string.IsNullOrWhiteSpace(employeeNumber))
                return new HireResult(false, "An employee number is required.");

            if (await _db.Employees.IgnoreQueryFilters().AnyAsync(e => e.EmployeeNumber == employeeNumber))
                return new HireResult(false, $"Employee number {employeeNumber} is already in use.");

            var application = offer.Application!;

            // An internal appointment moves the existing record rather than creating a second one.
            if (application.EmployeeId.HasValue)
            {
                var existing = await _db.Employees.FirstOrDefaultAsync(e => e.Id == application.EmployeeId.Value);
                if (existing != null)
                {
                    existing.JobTitle = offer.JobTitle;
                    existing.Grade = offer.Grade;
                    existing.Location = offer.Location;
                    existing.EmploymentType = offer.EmploymentType;
                    existing.ContractEndDate = offer.ContractEndDate;

                    offer.CreatedEmployeeId = existing.Id;
                    offer.Status = OfferStatus.Accepted;
                    application.Status = ApplicationStatus.Hired;

                    await _db.SaveChangesAsync();
                    return new HireResult(true,
                        $"{existing.DisplayName} moved to {offer.JobTitle}. The existing employee record was "
                        + "updated rather than duplicated.", existing.Id);
                }
            }

            var employee = new Employee
            {
                EmployeeNumber = employeeNumber,
                FirstName = application.FirstName,
                LastName = application.LastName,
                PersonalEmail = application.Email,
                MobileNumber = application.Phone,
                NationalId = application.NationalId,
                JobTitle = offer.JobTitle,
                Grade = offer.Grade,
                Location = offer.Location,
                DepartmentId = offer.Application?.Vacancy?.Requisition?.DepartmentId,
                ManagerId = offer.Application?.Vacancy?.Requisition?.ReportsToEmployeeId,
                EmploymentType = offer.EmploymentType,
                Status = offer.ProbationMonths > 0
                    ? EmploymentStatus.OnProbation
                    : EmploymentStatus.Active,
                HireDate = offer.StartDate,
                ProbationEndDate = offer.ProbationEndDate,
                ContractEndDate = offer.ContractEndDate
            };

            _db.Employees.Add(employee);
            await _db.SaveChangesAsync();

            // The offered salary becomes the first salary structure, effective from the start date.
            _db.SalaryStructures.Add(new SalaryStructure
            {
                EmployeeId = employee.Id,
                BasicSalary = offer.BasicSalary,
                Currency = offer.Currency,
                EffectiveFrom = offer.StartDate,
                Reason = $"On appointment, from offer {offer.Reference}.",
                CreatedById = userId
            });

            offer.CreatedEmployeeId = employee.Id;
            application.Status = ApplicationStatus.Hired;
            application.UpdatedAt = DateTime.Now;

            if (offer.Application?.Vacancy is { } vacancy)
            {
                var hired = await _db.JobApplications
                    .CountAsync(a => a.VacancyId == vacancy.Id && a.Status == ApplicationStatus.Hired);
                var wanted = vacancy.Requisition?.Positions ?? 1;
                if (hired + 1 >= wanted) vacancy.Status = VacancyStatus.Filled;
            }

            await _db.SaveChangesAsync();

            return new HireResult(true,
                $"{employee.DisplayName} added to the employee register as {employeeNumber}, starting "
                + $"{offer.StartDate:d MMM yyyy}. No login account was created — that is an administrator's "
                + "to grant.", employee.Id);
        }
    }
}
