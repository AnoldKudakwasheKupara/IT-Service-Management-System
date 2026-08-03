using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Hr;
using IT_Service_Management_System.ViewModels.Hr;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Hr
{
    /// <summary>
    /// Aggregates the HR data the module already collects. The interview instruments capture
    /// roughly thirty rated dimensions each and a full driver analysis on departure; before the
    /// employee register existed none of it could be rolled up, because there was no reliable way
    /// to count people. This turns it into headcount, turnover, tenure and sentiment.
    /// </summary>
    public class HrAnalyticsService
    {
        private readonly ApplicationDbContext _db;

        public HrAnalyticsService(ApplicationDbContext db) => _db = db;

        public async Task<HrAnalyticsVm> BuildAsync(DateTime from, DateTime to)
        {
            var vm = new HrAnalyticsVm { From = from, To = to };

            var employees = await _db.Employees.AsNoTracking()
                .Include(e => e.Department)
                .ToListAsync();

            BuildHeadcount(vm, employees, from, to);
            await BuildExitInterviewsAsync(vm, from, to);
            await BuildStayInterviewsAsync(vm, from, to);
            await BuildTalentAsync(vm);

            return vm;
        }

        // ── Headcount, turnover and tenure ───────────────────────────────────────

        private static void BuildHeadcount(HrAnalyticsVm vm, List<Employee> employees, DateTime from, DateTime to)
        {
            var current = employees.Where(e => e.IsCurrentEmployee).ToList();

            vm.Headcount = current.Count;
            vm.OnProbation = current.Count(e => e.Status == EmploymentStatus.OnProbation);
            vm.ProbationDue = current.Count(e => e.ProbationDueSoon);
            vm.ContractsExpiring = current.Count(e => e.ContractExpiringSoon);
            vm.WithoutManager = current.Count(e => e.ManagerId == null);
            vm.MissingEmergencyContact = current.Count(e =>
                string.IsNullOrWhiteSpace(e.NextOfKinPhone) && string.IsNullOrWhiteSpace(e.EmergencyContactPhone));

            var starters = employees.Where(e => e.HireDate >= from && e.HireDate <= to).ToList();
            var leavers = employees.Where(e => e.TerminationDate >= from && e.TerminationDate <= to).ToList();

            vm.StartedInPeriod = starters.Count;
            vm.LeftInPeriod = leavers.Count;

            // Turnover against average headcount over the window. Closing headcount would flatter a
            // shrinking organisation and punish a growing one.
            var openingHeadcount = employees.Count(e =>
                e.HireDate <= from && (e.TerminationDate == null || e.TerminationDate > from));
            var averageHeadcount = (openingHeadcount + vm.Headcount) / 2.0;

            if (averageHeadcount > 0)
            {
                vm.TurnoverPercent = Math.Round(leavers.Count / averageHeadcount * 100, 1);
                vm.VoluntaryTurnoverPercent = Math.Round(
                    leavers.Count(e => e.Status == EmploymentStatus.Resigned) / averageHeadcount * 100, 1);
            }

            var tenures = current.Where(e => e.YearsOfService.HasValue).Select(e => e.YearsOfService!.Value).ToList();
            vm.AverageTenureYears = tenures.Count == 0 ? 0 : Math.Round(tenures.Average(), 1);

            var leaverTenures = leavers.Where(e => e.YearsOfService.HasValue).Select(e => e.YearsOfService!.Value).ToList();
            vm.AverageLeaverTenureYears = leaverTenures.Count == 0 ? 0 : Math.Round(leaverTenures.Average(), 1);

            vm.FirstYearLeavers = leaverTenures.Count(t => t < 1);

            vm.HeadcountByDepartment = current
                .GroupBy(e => e.Department?.Name ?? "Unassigned")
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            vm.HeadcountByEmploymentType = current
                .GroupBy(e => Spaced(e.EmploymentType.ToString()))
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            // Fixed bands rather than a computed histogram, so the shape is comparable month to month.
            vm.TenureBands = new Dictionary<string, int>
            {
                ["Under 1 year"] = current.Count(e => e.YearsOfService is < 1),
                ["1–2 years"] = current.Count(e => e.YearsOfService is >= 1 and < 3),
                ["3–5 years"] = current.Count(e => e.YearsOfService is >= 3 and < 6),
                ["6–10 years"] = current.Count(e => e.YearsOfService is >= 6 and < 11),
                ["Over 10 years"] = current.Count(e => e.YearsOfService is >= 11),
                ["Not recorded"] = current.Count(e => e.YearsOfService == null)
            };

            vm.LeaversByStatus = leavers
                .GroupBy(e => Spaced(e.Status.ToString()))
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            var months = MonthsBetween(from, to);
            vm.StartersByMonth = months.Select(m => new MonthPoint
            {
                Label = m.ToString("MMM yy"),
                Value = starters.Count(e => e.HireDate!.Value.Year == m.Year && e.HireDate.Value.Month == m.Month)
            }).ToList();

            vm.LeaversByMonth = months.Select(m => new MonthPoint
            {
                Label = m.ToString("MMM yy"),
                Value = leavers.Count(e => e.TerminationDate!.Value.Year == m.Year && e.TerminationDate.Value.Month == m.Month)
            }).ToList();
        }

        // ── Exit interviews ──────────────────────────────────────────────────────

        private async Task BuildExitInterviewsAsync(HrAnalyticsVm vm, DateTime from, DateTime to)
        {
            var exits = await _db.ExitInterviews.AsNoTracking()
                .Where(x => (x.InterviewDate ?? x.CreatedDate) >= from
                         && (x.InterviewDate ?? x.CreatedDate) <= to)
                .ToListAsync();

            vm.ExitInterviewsHeld = exits.Count;

            // What proportion of leavers were actually interviewed — the number that says whether
            // the reason data can be trusted at all.
            vm.ExitInterviewCoveragePercent = vm.LeftInPeriod == 0
                ? 0
                : (int)Math.Round(Math.Min(exits.Count, vm.LeftInPeriod) * 100.0 / vm.LeftInPeriod);

            vm.ReasonsForLeaving = exits
                .Where(x => !string.IsNullOrWhiteSpace(x.PrimaryReasonForDeparture))
                .GroupBy(x => x.PrimaryReasonForDeparture!)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            // The eight rated drivers. Each is a Rating enum, so a zero means "not answered" and is
            // excluded rather than dragging the average down.
            vm.ExitRatings = new List<RatedDimension>
            {
                Rate("Career growth", exits.Select(x => (int)x.CareerGrowthOpportunities)),
                Rate("Compensation and benefits", exits.Select(x => (int)x.CompensationAndBenefits)),
                Rate("Work–life balance", exits.Select(x => (int)x.WorkLifeBalanceRating)),
                Rate("Management and leadership", exits.Select(x => (int)x.ManagementLeadershipStyle)),
                Rate("Culture and environment", exits.Select(x => (int)x.CompanyCultureWorkEnvironment)),
                Rate("Job responsibilities", exits.Select(x => (int)x.JobResponsibilities)),
                Rate("Relationship with manager", exits.Select(x => (int)x.RelationshipWithManagerRating))
            }
            .Where(d => d.Responses > 0)
            .OrderBy(d => d.Percent)
            .ToList();

            var returnAnswers = exits.Where(x => x.WouldReturnToCompany.HasValue).ToList();
            vm.WouldReturnPercent = returnAnswers.Count == 0 ? 0
                : (int)Math.Round(returnAnswers.Count(x => x.WouldReturnToCompany!.Value) * 100.0 / returnAnswers.Count);

            var recommendAnswers = exits.Where(x => x.WouldRecommendCompany.HasValue).ToList();
            vm.WouldRecommendPercent = recommendAnswers.Count == 0 ? 0
                : (int)Math.Round(recommendAnswers.Count(x => x.WouldRecommendCompany!.Value) * 100.0 / recommendAnswers.Count);
        }

        // ── Stay interviews ──────────────────────────────────────────────────────

        private async Task BuildStayInterviewsAsync(HrAnalyticsVm vm, DateTime from, DateTime to)
        {
            var stays = await _db.EngagementStayInterviews.AsNoTracking()
                .Where(x => (x.DiscussionDate ?? x.CreatedDate) >= from
                         && (x.DiscussionDate ?? x.CreatedDate) <= to)
                .ToListAsync();

            vm.StayInterviewsHeld = stays.Count;

            vm.EngagementStatusBreakdown = stays
                .Where(x => x.OverallStatus != EngagementStatus.NotSelected)
                .GroupBy(x => Spaced(x.OverallStatus.ToString()))
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            // Fourteen rated dimensions on a 1–3 scale, ordered worst first so the page opens on
            // whatever needs attention.
            vm.EngagementRatings = new List<RatedDimension>
            {
                Rate("Wellbeing", stays.Select(x => (int)x.WellbeingRating), 3),
                Rate("Job satisfaction", stays.Select(x => (int)x.JobSatisfactionRating), 3),
                Rate("Career opportunities", stays.Select(x => (int)x.CareerOpportunitiesRating), 3),
                Rate("Leadership quality", stays.Select(x => (int)x.LeadershipQualityRating), 3),
                Rate("Relationship with manager", stays.Select(x => (int)x.ManagerRelationshipRating), 3),
                Rate("Relationship with team", stays.Select(x => (int)x.TeamRelationshipRating), 3),
                Rate("Performance system", stays.Select(x => (int)x.BSCSystemRating), 3),
                Rate("Reward for performance", stays.Select(x => (int)x.RewardForPerformanceRating), 3),
                Rate("Communication channels", stays.Select(x => (int)x.CommunicationChannelsRating), 3),
                Rate("Development opportunities", stays.Select(x => (int)x.DevelopmentOpportunitiesRating), 3),
                Rate("Pay and benefits", stays.Select(x => (int)x.PayAndBenefitsRating), 3),
                Rate("Working conditions", stays.Select(x => (int)x.WorkingConditionsRating), 3),
                Rate("The organisation generally", stays.Select(x => (int)x.OrganizationGeneralRating), 3)
            }
            .Where(d => d.Responses > 0)
            .OrderBy(d => d.Percent)
            .ToList();
        }

        // ── Talent ───────────────────────────────────────────────────────────────

        private async Task BuildTalentAsync(HrAnalyticsVm vm)
        {
            var talent = await _db.TalentIdentifications.AsNoTracking().ToListAsync();

            vm.TalentAssessments = talent.Count;

            vm.NineBoxDistribution = talent
                .Where(t => !string.IsNullOrWhiteSpace(t.NineBoxAssessment))
                .GroupBy(t => t.NineBoxAssessment)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            vm.FlightRisk = talent
                .GroupBy(t => Spaced(t.RiskOfLeaving.ToString()))
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            vm.Readiness = talent
                .GroupBy(t => Spaced(t.Readiness.ToString()))
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            // Anyone rated a high flight risk, newest assessment first. This is the list the module
            // was always capable of producing and never did.
            vm.FlightRiskWatchlist = talent
                .Where(t => t.RiskOfLeaving == Enums.RiskLevel.High)
                .OrderByDescending(t => t.CreatedDate)
                .Take(25)
                .Select(t => new FlightRiskRow
                {
                    EmployeeId = t.EmployeeId,
                    AssessmentId = t.Id,
                    Name = t.EmployeeName,
                    JobTitle = t.JobTitle,
                    Department = t.Department,
                    Risk = Spaced(t.RiskOfLeaving.ToString()),
                    NineBox = t.NineBoxAssessment,
                    AssessedOn = t.CreatedDate
                })
                .ToList();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Average a rated question, ignoring zero — every one of these enums uses 0 for
        /// "not selected", and counting that as the lowest score would understate the result.
        /// </summary>
        private static RatedDimension Rate(string name, IEnumerable<int> values, int max = 5)
        {
            var answered = values.Where(v => v > 0).ToList();
            return new RatedDimension
            {
                Name = name,
                Max = max,
                Responses = answered.Count,
                Average = answered.Count == 0 ? 0 : Math.Round(answered.Average(), 2)
            };
        }

        private static List<DateTime> MonthsBetween(DateTime from, DateTime to)
        {
            var months = new List<DateTime>();
            var cursor = new DateTime(from.Year, from.Month, 1);
            var end = new DateTime(to.Year, to.Month, 1);

            // Cap the series so a wide range cannot produce hundreds of columns.
            while (cursor <= end && months.Count < 36)
            {
                months.Add(cursor);
                cursor = cursor.AddMonths(1);
            }
            return months;
        }

        private static string Spaced(string value) =>
            System.Text.RegularExpressions.Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " ");
    }
}
