namespace IT_Service_Management_System.Enums
{
    /// <summary>Lifecycle state of a meeting.</summary>
    public enum MeetingStatus
    {
        Scheduled,
        Held,
        Cancelled
    }

    /// <summary>How a rostered person attended a given meeting.</summary>
    public enum AttendanceStatus
    {
        Present,
        Absent,
        Apology,
        Late,
        OnLeave
    }

    /// <summary>Progress state of an action item raised in a meeting.</summary>
    public enum ActionItemStatus
    {
        Open,
        InProgress,
        Blocked,
        Done
    }

    /// <summary>Relative importance of an action item.</summary>
    public enum ActionItemPriority
    {
        Low,
        Normal,
        High
    }
}
