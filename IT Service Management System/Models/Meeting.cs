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

        public MeetingDay Day { get; set; }

        [StringLength(200)]
        public string? Title { get; set; }

        public int? FacilitatorId { get; set; }

        [ValidateNever]
        public User? Facilitator { get; set; }

        [Display(Name = "Minutes / Summary")]
        public string? Summary { get; set; }

        public MeetingStatus Status { get; set; } = MeetingStatus.Held;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? CreatedById { get; set; }

        [ValidateNever]
        public ICollection<MeetingAttendance> Attendances { get; set; } = new List<MeetingAttendance>();

        [ValidateNever]
        public ICollection<ActionItem> ActionItems { get; set; } = new List<ActionItem>();

        /// <summary>Human-friendly label, e.g. "Monday Meeting — 06 Jul 2026".</summary>
        [NotMapped]
        public string DisplayTitle =>
            string.IsNullOrWhiteSpace(Title)
                ? $"{Day} Meeting — {Date:dd MMM yyyy}"
                : Title!;
    }
}
