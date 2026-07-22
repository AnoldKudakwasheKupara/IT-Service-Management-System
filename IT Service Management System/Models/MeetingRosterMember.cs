using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models
{
    /// <summary>
    /// The standing team roster — the regular attendees. Each new meeting pre-loads these
    /// people into its attendance register (all Present by default), so admins only flip
    /// whoever was out. Ad-hoc guests can still be added per meeting.
    /// </summary>
    public class MeetingRosterMember
    {
        public int Id { get; set; }

        /// <summary>Department this roster belongs to. Null = an organization-wide roster.</summary>
        public int? DepartmentId { get; set; }

        [ValidateNever]
        public Department? Department { get; set; }

        public int UserId { get; set; }

        [ValidateNever]
        public User? User { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.Now;
    }
}
