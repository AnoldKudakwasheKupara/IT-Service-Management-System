using System.ComponentModel.DataAnnotations;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Models.Itsm
{
    /// <summary>
    /// A configurable SLA target for tickets. The most specific active policy that matches a
    /// ticket's priority + category wins; response = first-reply target, resolution = close target.
    /// Replaces the hard-coded targets that used to live in <c>Helpers/TicketSla</c>.
    /// </summary>
    public class SlaPolicy
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Null = applies to any priority.</summary>
        public TicketPriority? Priority { get; set; }

        /// <summary>Null/empty = applies to any category/queue.</summary>
        [StringLength(100)]
        public string? Category { get; set; }

        [Range(1, 100000)]
        public int ResponseMinutes { get; set; } = 240;

        [Range(1, 1000000)]
        public int ResolutionMinutes { get; set; } = 1440;

        /// <summary>When true, targets are measured in business hours (Mon–Fri, working hours) only.</summary>
        public bool BusinessHoursOnly { get; set; }

        public int? SlaCalendarId { get; set; }
        public SlaCalendar? Calendar { get; set; }

        /// <summary>Percentage of the available SLA window at which a proactive warning is raised.</summary>
        [Range(1, 99)]
        public int WarningThresholdPercent { get; set; } = 75;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
