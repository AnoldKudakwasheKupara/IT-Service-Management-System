using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Models.Itsm
{
    /// <summary>
    /// ITIL Problem record — the underlying cause of one or more incidents (tickets). Tracks root
    /// cause, workaround and a known-error state, and can spawn a change to fix it permanently.
    /// </summary>
    public class Problem
    {
        public int Id { get; set; }

        [NotMapped]
        public string ProblemRef => $"PRB-{Id:D5}";

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public ProblemStatus Status { get; set; } = ProblemStatus.New;
        public TicketPriority Priority { get; set; } = TicketPriority.Medium;

        [StringLength(2000)]
        public string? RootCause { get; set; }

        [StringLength(2000)]
        public string? Workaround { get; set; }

        public int? ConfigurationItemId { get; set; }
        [ValidateNever]
        public ConfigurationItem? ConfigurationItem { get; set; }

        public int? AssignedToId { get; set; }
        [ValidateNever]
        public User? AssignedTo { get; set; }

        public int CreatedById { get; set; }
        [ValidateNever]
        public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        [NotMapped]
        public bool IsOpen => Status != ProblemStatus.Resolved && Status != ProblemStatus.Closed;

        /// <summary>Incidents (tickets) linked to this problem.</summary>
        [ValidateNever]
        public ICollection<Ticket> Incidents { get; set; } = new List<Ticket>();
        [ValidateNever]
        public ICollection<ChangeRequest> Changes { get; set; } = new List<ChangeRequest>();
    }
}
