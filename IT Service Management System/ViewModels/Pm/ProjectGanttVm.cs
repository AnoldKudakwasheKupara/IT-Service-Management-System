using IT_Service_Management_System.Models.Pm;

namespace IT_Service_Management_System.ViewModels.Pm
{
    /// <summary>What kind of thing a Gantt row represents.</summary>
    public enum GanttRowKind { Milestone, Task }

    /// <summary>
    /// One bar on the chart, already positioned. The percentages are computed server-side against
    /// the chart window so the view does no date arithmetic — the same window then drives the
    /// header ticks, the today line and the dependency arrows, and none of them can disagree.
    /// </summary>
    public class GanttRow
    {
        public GanttRowKind Kind { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int PercentComplete { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Assignee { get; set; }
        public bool IsCritical { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsDone { get; set; }

        /// <summary>Milestone this row hangs under, for grouping. Null means straight on the project.</summary>
        public int? MilestoneId { get; set; }

        /// <summary>Task ids that must finish first. Drawn as arrows into this bar.</summary>
        public List<int> PredecessorTaskIds { get; set; } = new();

        /// <summary>Baseline dates, when the plan was frozen — drawn as a ghost bar beneath.</summary>
        public DateTime? BaselineStart { get; set; }
        public DateTime? BaselineEnd { get; set; }

        // Positions as a percentage of the chart window, filled in by the controller.
        public double LeftPercent { get; set; }
        public double WidthPercent { get; set; }
        public double? BaselineLeftPercent { get; set; }
        public double? BaselineWidthPercent { get; set; }

        /// <summary>Row index, so the dependency arrows know which line to draw between.</summary>
        public int Index { get; set; }
    }

    /// <summary>A label on the chart's time axis.</summary>
    public class GanttTick
    {
        public string Label { get; set; } = string.Empty;
        public double LeftPercent { get; set; }
        public double WidthPercent { get; set; }
        public bool IsMonthStart { get; set; }
    }

    /// <summary>
    /// A project's schedule as a Gantt chart. Everything the chart draws is derived once here,
    /// including the rows that cannot be drawn at all — a task with no dates is listed separately
    /// rather than dropped, because a plan that quietly hides work is worse than one that admits
    /// it is incomplete.
    /// </summary>
    public class ProjectGanttVm
    {
        public Project Project { get; set; } = null!;

        public List<GanttRow> Rows { get; set; } = new();
        public List<GanttTick> Ticks { get; set; } = new();

        /// <summary>Tasks with no start or due date, which cannot be placed on a timeline.</summary>
        public List<ProjectTask> Unscheduled { get; set; } = new();

        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public int WindowDays { get; set; }

        /// <summary>Position of today, or null when today falls outside the window.</summary>
        public double? TodayPercent { get; set; }

        public bool CanContribute { get; set; }

        public int CriticalCount => Rows.Count(r => r.IsCritical);
        public int OverdueCount => Rows.Count(r => r.IsOverdue);
        public bool HasBaseline => Rows.Any(r => r.BaselineLeftPercent.HasValue);

        /// <summary>Row height in pixels, shared by the CSS and the arrow overlay.</summary>
        public const int RowHeight = 34;

        public int ChartHeight => Math.Max(RowHeight, Rows.Count * RowHeight);
    }
}
