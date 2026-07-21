using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Models.Itsm
{
    public enum ServiceRequestStatus
    {
        AwaitingApproval,
        Approved,
        InFulfillment,
        OnHold,
        Fulfilled,
        Rejected,
        Cancelled
    }

    /// <summary>A requestable service exposed through the employee self-service catalogue.</summary>
    public class ServiceCatalogItem
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(240)]
        public string Summary { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        [Required, StringLength(80)]
        public string Category { get; set; } = "General";

        [StringLength(60)]
        public string Icon { get; set; } = "fa-concierge-bell";

        public TicketPriority DefaultPriority { get; set; } = TicketPriority.Medium;

        [Range(1, 525600)]
        public int FulfillmentTargetMinutes { get; set; } = 1440;

        public bool RequiresApproval { get; set; }
        public bool IsPublished { get; set; } = true;

        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        [ValidateNever]
        public ICollection<ServiceRequest> Requests { get; set; } = new List<ServiceRequest>();
    }

    /// <summary>A user's request for a catalogue service, including approval and fulfilment data.</summary>
    public class ServiceRequest
    {
        public int Id { get; set; }

        [NotMapped]
        public string Reference => $"REQ-{Id:D5}";

        public int ServiceCatalogItemId { get; set; }
        [ValidateNever] public ServiceCatalogItem? ServiceCatalogItem { get; set; }

        public int RequestedById { get; set; }
        [ValidateNever] public User? RequestedBy { get; set; }

        public int? AssignedToId { get; set; }
        [ValidateNever] public User? AssignedTo { get; set; }

        public int? ApprovedById { get; set; }
        [ValidateNever] public User? ApprovedBy { get; set; }

        [Required, StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required, StringLength(4000)]
        public string Details { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? BusinessJustification { get; set; }

        public TicketPriority Priority { get; set; } = TicketPriority.Medium;
        public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.AwaitingApproval;

        [StringLength(1000)]
        public string? ApprovalNotes { get; set; }

        [StringLength(2000)]
        public string? FulfillmentNotes { get; set; }

        [StringLength(1000)]
        public string? HoldReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? DueAt { get; set; }
        public DateTime? FulfilledAt { get; set; }

        [NotMapped]
        public bool IsClosed => Status is ServiceRequestStatus.Fulfilled
            or ServiceRequestStatus.Rejected or ServiceRequestStatus.Cancelled;

        [NotMapped]
        public bool IsOverdue => DueAt.HasValue && !IsClosed && DueAt.Value < DateTime.Now;
    }
}
