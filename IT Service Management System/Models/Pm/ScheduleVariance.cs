namespace IT_Service_Management_System.Models.Pm
{
    /// <summary>
    /// One way of putting schedule variance into words, shared by projects, milestones and tasks.
    ///
    /// A bare number cannot be read at a glance — "-3" is only meaningful once you know the sign
    /// convention — so everything that shows variance goes through here and says "3 days ahead".
    /// Positive is late, negative is early, which matches how the underlying subtraction reads
    /// (actual minus planned).
    /// </summary>
    public static class ScheduleVariance
    {
        public static string Describe(int? days)
        {
            if (days is null) return "No end date set";
            if (days == 0) return "On schedule";

            var magnitude = Math.Abs(days.Value);
            var unit = magnitude == 1 ? "day" : "days";
            return days > 0 ? $"{magnitude} {unit} delay" : $"{magnitude} {unit} ahead";
        }

        /// <summary>
        /// Severity band for colouring, so a one-day slip does not shout as loudly as a month.
        /// Returns "ahead", "ontrack", "slipping" or "late".
        /// </summary>
        public static string Band(int? days) => days switch
        {
            null => "ontrack",
            < 0 => "ahead",
            0 => "ontrack",
            <= 7 => "slipping",
            _ => "late"
        };
    }
}
