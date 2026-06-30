using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Helpers
{
    /// <summary>SLA resolution targets by priority (business expectation, not wall-clock guaranteed).</summary>
    public static class TicketSla
    {
        public static TimeSpan TargetFor(TicketPriority priority) => priority switch
        {
            TicketPriority.Critical => TimeSpan.FromHours(4),
            TicketPriority.High => TimeSpan.FromHours(8),
            TicketPriority.Medium => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(72)
        };

        public static DateTime DueFrom(DateTime createdAt, TicketPriority priority)
            => createdAt + TargetFor(priority);

        /// <summary>Short human label: "Due in 3h", "Overdue 2h", or "—".</summary>
        public static string Describe(DateTime? dueAt, bool isOpen)
        {
            if (dueAt == null) return "—";
            if (!isOpen) return "Met";

            var delta = dueAt.Value - DateTime.Now;
            var overdue = delta < TimeSpan.Zero;
            var abs = overdue ? -delta : delta;
            string span = abs.TotalDays >= 1 ? $"{(int)abs.TotalDays}d {abs.Hours}h"
                : abs.TotalHours >= 1 ? $"{(int)abs.TotalHours}h {abs.Minutes}m"
                : $"{abs.Minutes}m";
            return overdue ? $"Overdue {span}" : $"Due in {span}";
        }
    }
}
