namespace IT_Service_Management_System.ViewModels.Reports
{
    // ── SLA compliance ──────────────────────────────────────────────────────────
    public class SlaComplianceVM
    {
        public int MeasuredTickets { get; set; }
        public int ResponseMet { get; set; }
        public int ResponseBreached { get; set; }
        public int ResolutionMet { get; set; }
        public int ResolutionBreached { get; set; }
        public int OpenBreaching { get; set; }             // still open + past resolution target
        public double ResponseCompliance { get; set; }
        public double ResolutionCompliance { get; set; }
        public double AvgResponseHours { get; set; }
        public double AvgResolutionHours { get; set; }     // MTTR
        public List<SlaByPriority> ByPriority { get; set; } = new();
        public List<NameCount> BreachesByCategory { get; set; } = new();
    }

    public record SlaByPriority(string Priority, int Total, int Met, int Breached, double Compliance, double AvgResolutionHours);

    // ── Agent performance ───────────────────────────────────────────────────────
    public class AgentPerformanceVM
    {
        public List<AgentRow> Agents { get; set; } = new();
        public int Unassigned { get; set; }
        public int TotalResolved { get; set; }
    }

    public record AgentRow(string Name, int Assigned, int Open, int Resolved,
        double AvgResolutionHours, int SlaBreaches, double? AvgCsat);

    // ── ITIL overview (problems, changes, CMDB) ─────────────────────────────────
    public class ItilOverviewVM
    {
        public int TotalProblems { get; set; }
        public int OpenProblems { get; set; }
        public int KnownErrors { get; set; }
        public int TotalChanges { get; set; }
        public int ChangesAwaitingApproval { get; set; }
        public int ChangeSuccessRate { get; set; }
        public int TotalCis { get; set; }
        public int CriticalCis { get; set; }

        public List<NameCount> ProblemsByStatus { get; set; } = new();
        public List<NameCount> ChangesByStatus { get; set; } = new();
        public List<NameCount> ChangesByType { get; set; } = new();
        public List<NameCount> ChangesByRisk { get; set; } = new();
        public List<NameCount> CisByStatus { get; set; } = new();
        public List<NameCount> CisByCriticality { get; set; } = new();

        public List<Models.Itsm.Problem> RecentProblems { get; set; } = new();
        public List<Models.Itsm.ChangeRequest> UpcomingChanges { get; set; } = new();
    }

    // ── Ticket trends over time ─────────────────────────────────────────────────
    public class TicketTrendsVM
    {
        public List<TrendPoint> Months { get; set; } = new();
        public int PeakValue { get; set; }
        public int TotalCreated { get; set; }
        public int TotalResolved { get; set; }
    }

    public record TrendPoint(string Label, int Created, int Resolved);
}
