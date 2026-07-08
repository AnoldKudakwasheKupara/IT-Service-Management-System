using IT_Service_Management_System.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IT_Service_Management_System.Models
{
    /// <summary>
    /// A single recurring team meeting (typically Monday or Friday). Owns the attendance
    /// register and the action items raised during it.
    /// </summary>
    public class Meeting
    {
        public int Id { get; set; }

        [Display(Name = "Meeting Date")]
        public DateTime Date { get; set; }

        /// <summary>Owning department. Null = an organization-wide meeting (e.g. the DARE leadership meeting).</summary>
        public int? DepartmentId { get; set; }

        [ValidateNever]
        public Department? Department { get; set; }

        [StringLength(200)]
        public string? Title { get; set; }

        /// <summary>Physical room or a virtual link (e.g. "Arundel Boardroom" or "Google Meet").</summary>
        [StringLength(200)]
        public string? Venue { get; set; }

        [Display(Name = "Start Time")]
        public TimeOnly? StartTime { get; set; }

        [Display(Name = "End Time")]
        public TimeOnly? EndTime { get; set; }

        /// <summary>Chair of the meeting.</summary>
        public int? FacilitatorId { get; set; }

        [ValidateNever]
        public User? Facilitator { get; set; }

        public int? MinuteTakerId { get; set; }

        [ValidateNever]
        public User? MinuteTaker { get; set; }

        [Display(Name = "Overview / Objective")]
        public string? Objective { get; set; }

        [Display(Name = "Minutes / Discussion")]
        public string? Summary { get; set; }

        [Display(Name = "Next Meeting Date")]
        public DateTime? NextMeetingDate { get; set; }

        public MeetingStatus Status { get; set; } = MeetingStatus.Held;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? CreatedById { get; set; }

        [ValidateNever]
        public ICollection<MeetingAttendance> Attendances { get; set; } = new List<MeetingAttendance>();

        [ValidateNever]
        public ICollection<ActionItem> ActionItems { get; set; } = new List<ActionItem>();

        /// <summary>Day of week derived from the date (e.g. "Wednesday") — meetings run on any weekday.</summary>
        [NotMapped]
        public string WeekdayName => Date.DayOfWeek.ToString();

        /// <summary>Human-friendly label, e.g. "Wednesday Meeting — 17 Jun 2026".</summary>
        [NotMapped]
        public string DisplayTitle =>
            string.IsNullOrWhiteSpace(Title)
                ? $"{WeekdayName} Meeting — {Date:dd MMM yyyy}"
                : Title!;
    }
}
