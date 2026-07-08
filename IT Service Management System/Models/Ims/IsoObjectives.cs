using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Ims
{
    // ── Objectives & KPIs enumerations (ISO 9001 cl. 6.2 / ISO 27001 cl. 6.2) ────
    public enum ObjectiveStatus { Draft, Active, OnTrack, AtRisk, Achieved, NotAchieved, Closed }
    public enum KpiDirection { HigherIsBetter, LowerIsBetter, OnTarget }
    public enum MeasurementFrequency { Monthly, Quarterly, SemiAnnual, Annual }

    /// <summary>A measurable quality/security objective (KPI) with a target and periodic measurements.</summary>
    public class Objective
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"OBJ-{Id:D5}";

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public IsoStandard Standard { get; set; } = IsoStandard.Both;

        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }

        [Display(Name = "Target Value")]
        public decimal? TargetValue { get; set; }
        [StringLength(30)]
        public string? Unit { get; set; }
        [Display(Name = "Baseline")]
        public decimal? BaselineValue { get; set; }
        [Display(Name = "Current Value")]
        public decimal? CurrentValue { get; set; }

        public KpiDirection Direction { get; set; } = KpiDirection.HigherIsBetter;
        public MeasurementFrequency Frequency { get; set; } = MeasurementFrequency.Quarterly;

        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }
        [Display(Name = "Due Date"), DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        public ObjectiveStatus Status { get; set; } = ObjectiveStatus.Draft;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        [ValidateNever] public ICollection<ObjectiveMeasurement> Measurements { get; set; } = new List<ObjectiveMeasurement>();

        /// <summary>Progress toward target (0–100), honouring the KPI direction. Null if not enough data.</summary>
        [NotMapped]
        public int? ProgressPercent
        {
            get
            {
                if (!TargetValue.HasValue || !CurrentValue.HasValue) return null;
                var target = TargetValue.Value;
                var current = CurrentValue.Value;
                if (Direction == KpiDirection.LowerIsBetter)
                {
                    if (current <= target) return 100;
                    var baseline = BaselineValue ?? (current == 0 ? 1 : current);
                    if (baseline <= target) return 100;
                    var pct = (double)((baseline - current) / (baseline - target)) * 100;
                    return Math.Clamp((int)Math.Round(pct), 0, 100);
                }
                if (target == 0) return current >= 0 ? 100 : 0;
                return Math.Clamp((int)Math.Round((double)(current / target) * 100), 0, 100);
            }
        }
    }

    public class ObjectiveMeasurement
    {
        public int Id { get; set; }
        public int ObjectiveId { get; set; }
        [ValidateNever] public Objective? Objective { get; set; }

        [Required, StringLength(40), Display(Name = "Period")]
        public string PeriodLabel { get; set; } = string.Empty;

        public decimal Value { get; set; }

        [Display(Name = "Recorded Date"), DataType(DataType.Date)]
        public DateTime RecordedDate { get; set; } = DateTime.Now;

        [StringLength(1000)]
        public string? Notes { get; set; }

        public int? RecordedById { get; set; }
        [ValidateNever] public User? RecordedBy { get; set; }
    }
}
