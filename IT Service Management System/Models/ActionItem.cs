using IT_Service_Management_System.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IT_Service_Management_System.Models
{
    /// <summary>
    /// A task/issue raised in a meeting and assigned to someone. Carries forward across
    /// weekly meetings via its <see cref="Updates"/> progress log until it reaches Done.
    /// </summary>
    public class ActionItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(250)]
        public string Title { get; set; } = string.Empty;

        public string? Details { get; set; }

        public int? AssignedToId { get; set; }

        [ValidateNever]
        public User? AssignedTo { get; set; }

        /// <summary>Free-text assignee for a team/department or multiple people
        /// (e.g. "Support Team", "Danai H. / Ngonidzashe J.") when it isn't a single system user.</summary>
        [StringLength(150)]
        public string? AssigneeLabel { get; set; }

        /// <summary>The meeting where this item was first raised.</summary>
        public int MeetingId { get; set; }

        [ValidateNever]
        public Meeting? Meeting { get; set; }

        [Display(Name = "Due Date")]
        public DateTime? DueDate { get; set; }

        public ActionItemStatus Status { get; set; } = ActionItemStatus.Open;

        public ActionItemPriority Priority { get; set; } = ActionItemPriority.Normal;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? CreatedById { get; set; }

        public DateTime? ClosedAt { get; set; }

        [ValidateNever]
        public ICollection<ActionItemUpdate> Updates { get; set; } = new List<ActionItemUpdate>();

        /// <summary>Who the item is on: the assigned user, else the free-text label, else "Unassigned".</summary>
        [NotMapped]
        public string AssigneeDisplay =>
            AssignedTo?.FullName
            ?? (string.IsNullOrWhiteSpace(AssigneeLabel) ? "Unassigned" : AssigneeLabel!);

        [NotMapped]
        public bool IsOpen => Status != ActionItemStatus.Done;

        [NotMapped]
        public bool IsOverdue => DueDate.HasValue && DueDate.Value.Date < DateTime.Now.Date && IsOpen;
    }
}
