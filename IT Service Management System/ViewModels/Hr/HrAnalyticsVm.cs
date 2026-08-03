using IT_Service_Management_System.Models.Hr;

namespace IT_Service_Management_System.ViewModels.Hr
{
    /// <summary>
    /// The HR analytics page. Everything here comes from data the module already collected but
    /// never aggregated — thirty rated dimensions per stay interview, a driver analysis per exit
    /// interview, and a 9-box placement per talent record.
    /// </summary>
    public class HrAnalyticsVm
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        // ── Headcount ────────────────────────────────────────────────────────────
        public int Headcount { get; set; }
        public int StartedInPeriod { get; set; }
        public int LeftInPeriod { get; set; }
        public int NetChange => StartedInPeriod - LeftInPeriod;
        public int OnProbation { get; set; }
        public int ProbationDue { get; set; }
        public int ContractsExpiring { get; set; }
        public int WithoutManager { get; set; }
        public int MissingEmergencyContact { get; set; }

        /// <summary>
        /// Leavers over the period as a percentage of average headcount — the standard turnover
        /// formula. Average headcount, not closing, or a shrinking organisation flatters itself.
        /// </summary>
        public double TurnoverPercent { get; set; }

        /// <summary>Turnover counting only resignations, which is the number that is actionable.</summary>
        public double VoluntaryTurnoverPercent { get; set; }

        public double AverageTenureYears { get; set; }
        public double AverageLeaverTenureYears { get; set; }

        /// <summary>Leavers inside their first year — the sharpest signal that hiring or onboarding is wrong.</summary>
        public int FirstYearLeavers { get; set; }

        // ── Breakdowns ───────────────────────────────────────────────────────────
        public Dictionary<string, int> HeadcountByDepartment { get; set; } = new();
        public Dictionary<string, int> HeadcountByEmploymentType { get; set; } = new();
        public Dictionary<string, int> TenureBands { get; set; } = new();
        public Dictionary<string, int> LeaversByStatus { get; set; } = new();
        public List<MonthPoint> StartersByMonth { get; set; } = new();
        public List<MonthPoint> LeaversByMonth { get; set; } = new();

        // ── Exit interviews ──────────────────────────────────────────────────────
        public int ExitInterviewsHeld { get; set; }
        public int ExitInterviewCoveragePercent { get; set; }
        public Dictionary<string, int> ReasonsForLeaving { get; set; } = new();
        public List<RatedDimension> ExitRatings { get; set; } = new();
        public int WouldReturnPercent { get; set; }
        public int WouldRecommendPercent { get; set; }

        // ── Stay interviews ──────────────────────────────────────────────────────
        public int StayInterviewsHeld { get; set; }
        public Dictionary<string, int> EngagementStatusBreakdown { get; set; } = new();
        public List<RatedDimension> EngagementRatings { get; set; } = new();

        // ── Talent ───────────────────────────────────────────────────────────────
        public int TalentAssessments { get; set; }
        public Dictionary<string, int> NineBoxDistribution { get; set; } = new();
        public Dictionary<string, int> FlightRisk { get; set; } = new();
        public Dictionary<string, int> Readiness { get; set; } = new();
        public List<FlightRiskRow> FlightRiskWatchlist { get; set; } = new();
    }

    public class MonthPoint
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    /// <summary>
    /// A rated question averaged across the responses to it. <see cref="Average"/> is on the
    /// scale the question used; <see cref="Percent"/> normalises it so dimensions with different
    /// scales can be compared on one chart.
    /// </summary>
    public class RatedDimension
    {
        public string Name { get; set; } = string.Empty;
        public double Average { get; set; }
        public int Max { get; set; } = 5;
        public int Responses { get; set; }
        public int Percent => Max <= 0 ? 0 : (int)Math.Round(Average / Max * 100);

        /// <summary>Bottom third is a concern, top third is a strength.</summary>
        public string Band => Percent >= 67 ? "good" : Percent >= 34 ? "fair" : "poor";
    }

    public class FlightRiskRow
    {
        public int? EmployeeId { get; set; }
        public int AssessmentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
        public string NineBox { get; set; } = string.Empty;
        public DateTime AssessedOn { get; set; }
    }
}
