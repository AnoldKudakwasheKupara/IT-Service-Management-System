using IT_Service_Management_System.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    /// <summary>One person's attendance for one meeting. Makes attendance reportable over time.</summary>
    public class MeetingAttendance
    {
        public int Id { get; set; }

        public int MeetingId { get; set; }

        [ValidateNever]
        public Meeting? Meeting { get; set; }

        public int UserId { get; set; }

        [ValidateNever]
        public User? User { get; set; }

        public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

        [StringLength(300)]
        public string? Note { get; set; }
    }
}
