using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Itsm
{
    public class SlaCalendar
    {
        public const int MondayToFriday = 62;

        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public TimeSpan WorkDayStart { get; set; } = new(8, 0, 0);
        public TimeSpan WorkDayEnd { get; set; } = new(17, 0, 0);

        /// <summary>DayOfWeek bitmask; the default enables Monday through Friday.</summary>
        public int WorkingDaysMask { get; set; } = MondayToFriday;

        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever]
        public ICollection<SlaHoliday> Holidays { get; set; } = new List<SlaHoliday>();

        [ValidateNever]
        public ICollection<SlaPolicy> Policies { get; set; } = new List<SlaPolicy>();

        public bool IsWorkingDay(DayOfWeek day) => (WorkingDaysMask & (1 << (int)day)) != 0;

        [NotMapped]
        public string WorkHoursLabel => $"{WorkDayStart:hh\\:mm}–{WorkDayEnd:hh\\:mm}";
    }

    public class SlaHoliday
    {
        public int Id { get; set; }
        public int SlaCalendarId { get; set; }
        [ValidateNever] public SlaCalendar? SlaCalendar { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        public DateOnly Date { get; set; }
    }

    public enum SlaEventType
    {
        ResponseWarning,
        ResolutionWarning,
        ResponseBreached,
        ResolutionBreached
    }

    /// <summary>Persistent, de-duplicated SLA warning and breach history.</summary>
    public class SlaEvent
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        [ValidateNever] public Ticket? Ticket { get; set; }
        public SlaEventType Type { get; set; }
        public int ThresholdPercent { get; set; }
        [Required, StringLength(500)] public string Message { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; } = DateTime.Now;
    }
}
