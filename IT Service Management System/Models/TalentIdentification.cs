using System.ComponentModel.DataAnnotations;
using IT_Service_Management_System.Enums;

namespace IT_Service_Management_System.Models
{
    public class TalentIdentification
    {
        public int Id { get; set; }

        public TalentIdentification()
        {
            DirectReports = new List<TalentDirectReportAssessment>();
            DevelopmentActions = new List<TalentDevelopmentAction>();
        }


        /// <summary>
        /// The employee this assessment belongs to. Nullable only so historical rows captured
        /// before the employee register existed can be matched up gradually.
        /// </summary>
        public int? EmployeeId { get; set; }

        [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
        public Hr.Employee? Employee { get; set; }

        // Employee Information — a snapshot of the person's details at the time of assessment.

        [Required(ErrorMessage = "Enter the employee name.")]
        [StringLength(150)]
        public string EmployeeName { get; set; } = string.Empty;

        [StringLength(120)]
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the job title.")]
        [StringLength(150)]
        public string JobTitle { get; set; } = string.Empty;

        [StringLength(100)]
        public string Country { get; set; } = string.Empty;

        public DateTime? HireDate { get; set; }

        // Performance Track Record

        [StringLength(1000)]
        public string KPI2023 { get; set; } = string.Empty;

        [StringLength(1000)]
        public string KPI2024 { get; set; } = string.Empty;

        [StringLength(1000)]
        public string KPI2025 { get; set; } = string.Empty;

        [StringLength(1000)]
        public string KPI2026 { get; set; } = string.Empty;

        [StringLength(2000)]
        public string KeyProjectsLed { get; set; } = string.Empty;

        [StringLength(2000)]
        public string DeliverySetbacks { get; set; } = string.Empty;

        [StringLength(2000)]
        public string LongTermBusinessInitiatives { get; set; } = string.Empty;

        // Leadership Capability

        [StringLength(2000)]
        public string LeadershipOverallComments { get; set; } = string.Empty;

        [StringLength(2000)]
        public string TeamCapabilityDevelopment { get; set; } = string.Empty;

        [StringLength(2000)]
        public string StructuredOneOnOnes { get; set; } = string.Empty;

        [StringLength(2000)]
        public string TerminatedPoorPerformers { get; set; } = string.Empty;

        [StringLength(2000)]
        public string LeadershipDevelopmentAreas { get; set; } = string.Empty;

        // Living The Axis Values

        [StringLength(2000)]
        public string ChallengesApplyingAxisValues { get; set; } = string.Empty;

        [StringLength(2000)]
        public string SelfDevelopmentActions { get; set; } = string.Empty;

        [StringLength(2000)]
        public string SelfInitiatedLeadershipDevelopment { get; set; } = string.Empty;

        // Potential Assessment

        public bool ThinkingComplexity { get; set; }

        public bool ThinkingJudgement { get; set; }

        public bool ThinkingScale { get; set; }

        public bool WisdomFastJudgement { get; set; }

        public bool WisdomWhenToAct { get; set; }

        public bool WisdomLongTermImpact { get; set; }

        public bool CourageRiskTaking { get; set; }

        public bool CourageConvictions { get; set; }

        public bool SelfAwareness { get; set; }

        public bool OpenToFeedback { get; set; }

        public bool WillingToImprove { get; set; }

        public bool LearningAgility { get; set; }

        public bool Resilience { get; set; }

        // Career Development

        [StringLength(2000)]
        public string CareerAspirations { get; set; } = string.Empty;

        public MobilityType Mobility { get; set; }

        public RiskLevel RiskOfLeaving { get; set; }

        public bool CanOccupyHigherGrade { get; set; }

        [StringLength(120)]
        public string NineBoxAssessment { get; set; } = string.Empty;

        [StringLength(1000)]
        public string NextCareerMilestone { get; set; } = string.Empty;

        public ReadinessLevel Readiness { get; set; }

        // Audit

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public bool IsDeleted { get; set; }

        public List<TalentDirectReportAssessment> DirectReports { get; set; } = new();

        public List<TalentDevelopmentAction> DevelopmentActions { get; set; } = new();
    }
}