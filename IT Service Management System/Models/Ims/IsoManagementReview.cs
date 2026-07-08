using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Ims
{
    // ── Management Review enumerations (ISO 9001 cl. 9.3 / ISO 27001 cl. 9.3) ────
    public enum ReviewMeetingStatus { Planned, Scheduled, Held, Closed, Cancelled }
    public enum ReviewActionStatus { Open, InProgress, Completed, Overdue, Cancelled }

    /// <summary>A management review meeting. Inputs are auto-gathered from across the IMS into <see cref="Inputs"/>.</summary>
    public class ManagementReview
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"MRV-{Id:D5}";

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public IsoStandard Standard { get; set; } = IsoStandard.Both;

        [Display(Name = "Meeting Date")]
        public DateTime MeetingDate { get; set; } = DateTime.Now;

        [StringLength(200)]
        public string? Location { get; set; }

        [Display(Name = "Chairperson")]
        public int? ChairId { get; set; }
        [ValidateNever] public User? Chair { get; set; }

        public ReviewMeetingStatus Status { get; set; } = ReviewMeetingStatus.Planned;

        [StringLength(4000), Display(Name = "Agenda")]
        public string? AgendaNotes { get; set; }

        [StringLength(6000), Display(Name = "Decisions")]
        public string? Decisions { get; set; }

        [StringLength(6000), Display(Name = "Conclusions")]
        public string? Conclusions { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        [ValidateNever] public ICollection<ManagementReviewAttendee> Attendees { get; set; } = new List<ManagementReviewAttendee>();
        [ValidateNever] public ICollection<ManagementReviewInput> Inputs { get; set; } = new List<ManagementReviewInput>();
        [ValidateNever] public ICollection<ManagementReviewAction> Actions { get; set; } = new List<ManagementReviewAction>();
    }

    public class ManagementReviewAttendee
    {
        public int Id { get; set; }
        public int ManagementReviewId { get; set; }
        [ValidateNever] public ManagementReview? ManagementReview { get; set; }
        public int UserId { get; set; }
        [ValidateNever] public User? User { get; set; }
        [StringLength(100)] public string? Role { get; set; }
        public bool Present { get; set; } = true;
    }

    /// <summary>A standard ISO 9.3 input line, auto-populated from the module's live data.</summary>
    public class ManagementReviewInput
    {
        public int Id { get; set; }
        public int ManagementReviewId { get; set; }
        [ValidateNever] public ManagementReview? ManagementReview { get; set; }

        [Required, StringLength(120)]
        public string Category { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Summary { get; set; }

        public int Sequence { get; set; }
    }

    /// <summary>An action arising from a management review (Meeting Action Tracking — module 25).</summary>
    public class ManagementReviewAction
    {
        public int Id { get; set; }
        public int ManagementReviewId { get; set; }
        [ValidateNever] public ManagementReview? ManagementReview { get; set; }

        [NotMapped] public string Reference => $"MRA-{Id:D5}";

        [Required, StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Assigned To")]
        public int? AssignedToId { get; set; }
        [ValidateNever] public User? AssignedTo { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        public ReviewActionStatus Status { get; set; } = ReviewActionStatus.Open;

        [DataType(DataType.Date)]
        public DateTime? CompletedDate { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped] public bool IsOverdue =>
            Status is not ReviewActionStatus.Completed and not ReviewActionStatus.Cancelled
            && DueDate.HasValue && DueDate.Value.Date < DateTime.Now.Date;
    }
}
