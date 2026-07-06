using IT_Service_Management_System.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    /// <summary>
    /// A progress note logged against an action item — optionally tied to the meeting where it
    /// was discussed. The running history that lets a task carry forward week to week.
    /// </summary>
    public class ActionItemUpdate
    {
        public int Id { get; set; }

        public int ActionItemId { get; set; }

        [ValidateNever]
        public ActionItem? ActionItem { get; set; }

        /// <summary>Meeting this update was recorded at (optional).</summary>
        public int? MeetingId { get; set; }

        [ValidateNever]
        public Meeting? Meeting { get; set; }

        [Required]
        [StringLength(2000)]
        public string Note { get; set; } = string.Empty;

        /// <summary>Snapshot of the item's status at the moment of this update.</summary>
        public ActionItemStatus StatusAtUpdate { get; set; }

        public int UpdatedById { get; set; }

        [ValidateNever]
        public User? UpdatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
