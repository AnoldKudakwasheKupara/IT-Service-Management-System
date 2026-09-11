using IT_Service_Management_System.Models.Pm;

namespace IT_Service_Management_System.ViewModels.Pm
{
    /// <summary>What a deadline belongs to, so the radar can label and link it.</summary>
    public enum DeadlineKind { Project, Milestone, Task }

    /// <summary>One dated commitment in the window.</summary>
    public class DeadlineItem
    {
        public DeadlineKind Kind { get; set; }
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime Due { get; set; }
        public string? Owner { get; set; }
        public bool IsOverdue { get; set; }

        public int DaysAway => (int)(Due.Date - DateTime.Today).TotalDays;
    }

    /// <summary>A day carrying more than one deadline, with everything landing on it.</summary>
    public class DeadlineDay
    {
        public DateTime Date { get; set; }
        public List<DeadlineItem> Items { get; set; } = new();

        /// <summary>Three or more commitments on one day is worth a second look.</summary>
        public bool IsCongested => Items.Count >= 3;
        public bool IsOverdue => Date.Date < DateTime.Today;
    }

    /// <summary>One of a person's dated, open tasks — an interval on their personal timeline.</summary>
    public class TaskSpan
    {
        public int TaskId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool IsOverdue { get; set; }
        public double LeftPercent { get; set; }
        public double WidthPercent { get; set; }
    }

    /// <summary>
    /// How loaded one person is over the window. The number that matters is the peak — the most
    /// tasks they are expected to be doing at any one moment — because a person with six tasks
    /// spread evenly is fine and a person with three all in the same week is not.
    /// </summary>
    public class PersonLoadRow
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<TaskSpan> Spans { get; set; } = new();

        public int PeakConcurrent { get; set; }
        public DateTime? PeakFrom { get; set; }
        public DateTime? PeakTo { get; set; }

        public int ProjectCount => Spans.Select(s => s.ProjectId).Distinct().Count();
        public int OverdueCount => Spans.Count(s => s.IsOverdue);
        public bool IsOverlapping => PeakConcurrent > 1;

        /// <summary>Rough severity for colouring: one task at a time is fine, four at once is not.</summary>
        public string Band => PeakConcurrent >= 4 ? "late"
            : PeakConcurrent == 3 ? "slipping"
            : PeakConcurrent == 2 ? "ontrack" : "ahead";
    }

    /// <summary>
    /// The scheduling view: one time axis for the whole portfolio, who is double-booked on it,
    /// and what falls due. Three questions that are usually answered in three different tools.
    /// </summary>
    public class ProjectScheduleVm
    {
        public int WindowDays { get; set; } = 60;
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public double? TodayPercent { get; set; }

        public List<GanttRow> ProjectBars { get; set; } = new();
        public List<GanttTick> Ticks { get; set; } = new();

        public List<PersonLoadRow> People { get; set; } = new();
        public List<DeadlineDay> Deadlines { get; set; } = new();

        /// <summary>Pairs of projects whose dates overlap — the portfolio's own contention.</summary>
        public int OverlappingProjectPairs { get; set; }

        public int PeopleOverlapping => People.Count(p => p.IsOverlapping);
        public int CongestedDays => Deadlines.Count(d => d.IsCongested);
        public int TotalDeadlines => Deadlines.Sum(d => d.Items.Count);
        public int OverdueDeadlines => Deadlines.Sum(d => d.Items.Count(i => i.IsOverdue));

        public const int RowHeight = 30;
        public int ChartHeight => Math.Max(RowHeight, ProjectBars.Count * RowHeight);
    }
}
