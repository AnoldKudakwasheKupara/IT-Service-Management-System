using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Pm
{
    /// <summary>
    /// Anything a project consumes capacity from — a person, a vehicle, a licence, a meeting room.
    /// People-type resources link back to a <see cref="User"/>; the rest stand alone.
    /// </summary>
    public class Resource
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public ResourceType Type { get; set; } = ResourceType.Person;

        public ResourceStatus Status { get; set; } = ResourceStatus.Available;

        /// <summary>Set for Person resources so timesheets and assignments tie back to the account.</summary>
        public int? UserId { get; set; }
        [ValidateNever] public User? User { get; set; }

        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        /// <summary>Comma-separated skills / capabilities, used for allocation suggestions.</summary>
        [StringLength(500)]
        public string? Skills { get; set; }

        /// <summary>Cost per hour used to value time entries and forecast labour spend.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; }

        /// <summary>Chargeable rate when the resource's time is billed to a client.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal BillableRate { get; set; }

        /// <summary>Available hours per week — the denominator for utilisation.</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal WeeklyCapacityHours { get; set; } = 40m;

        [StringLength(200)]
        public string? Location { get; set; }

        /// <summary>Asset tag / registration / licence key, depending on the resource type.</summary>
        [StringLength(120)]
        public string? IdentifierCode { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever] public ICollection<ResourceAssignment> Assignments { get; set; } = new List<ResourceAssignment>();
        [ValidateNever] public ICollection<ResourceUnavailability> Unavailability { get; set; } = new List<ResourceUnavailability>();
    }

    /// <summary>A booking of a resource against a project (and optionally a specific task).</summary>
    public class ResourceAssignment
    {
        public int Id { get; set; }

        public int ResourceId { get; set; }
        [ValidateNever] public Resource? Resource { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public int? TaskId { get; set; }
        [ValidateNever] public ProjectTask? Task { get; set; }

        [DataType(DataType.Date)] public DateTime FromDate { get; set; } = DateTime.Today;
        [DataType(DataType.Date)] public DateTime ToDate { get; set; } = DateTime.Today;

        /// <summary>Share of the resource's capacity committed over the window.</summary>
        [Range(0, 100)]
        public int AllocationPercent { get; set; } = 100;

        /// <summary>Total hours planned across the window, used for workload charts.</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal PlannedHours { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>A window in which a resource cannot be booked — leave, maintenance, or an outage.</summary>
    public class ResourceUnavailability
    {
        public int Id { get; set; }

        public int ResourceId { get; set; }
        [ValidateNever] public Resource? Resource { get; set; }

        [Required, StringLength(200)]
        public string Reason { get; set; } = string.Empty;

        [DataType(DataType.Date)] public DateTime FromDate { get; set; } = DateTime.Today;
        [DataType(DataType.Date)] public DateTime ToDate { get; set; } = DateTime.Today;

        /// <summary>Distinguishes annual leave from equipment servicing on the calendar.</summary>
        [StringLength(60)]
        public string? Kind { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// A block of time an employee booked against a project (and usually a task). Timesheets are
    /// submitted, approved, then costed at the resource's hourly rate.
    /// </summary>
    public class TimeEntry
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public int? TaskId { get; set; }
        [ValidateNever] public ProjectTask? Task { get; set; }

        public int UserId { get; set; }
        [ValidateNever] public User? User { get; set; }

        [DataType(DataType.Date)]
        public DateTime WorkDate { get; set; } = DateTime.Today;

        [Column(TypeName = "decimal(10,2)")]
        [Range(0, 24)]
        public decimal Hours { get; set; }

        /// <summary>Unpaid break time within the booking, excluded from the costed total.</summary>
        [Column(TypeName = "decimal(10,2)")]
        [Range(0, 24)]
        public decimal BreakHours { get; set; }

        public TimeEntryType Type { get; set; } = TimeEntryType.Regular;

        public bool IsBillable { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public TimeEntryStatus Status { get; set; } = TimeEntryStatus.Draft;

        public int? ApprovedById { get; set; }
        [ValidateNever] public User? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        [StringLength(500)]
        public string? RejectionReason { get; set; }

        /// <summary>Rate snapshotted at approval so historical cost does not move when rates change.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostRate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Hours net of breaks — what actually gets costed.</summary>
        [NotMapped]
        public decimal NetHours => Math.Max(0, Hours - BreakHours);

        [NotMapped]
        public decimal Cost => NetHours * CostRate;
    }
}
