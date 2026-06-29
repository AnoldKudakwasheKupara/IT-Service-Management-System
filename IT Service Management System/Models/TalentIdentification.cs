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


        // Employee Information

        public string EmployeeName { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public string JobTitle { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public DateTime? HireDate { get; set; }

        // Performance Track Record

        public string KPI2023 { get; set; } = string.Empty;

        public string KPI2024 { get; set; } = string.Empty;

        public string KPI2025 { get; set; } = string.Empty;

        public string KPI2026 { get; set; } = string.Empty;

        public string KeyProjectsLed { get; set; } = string.Empty;

        public string DeliverySetbacks { get; set; } = string.Empty;

        public string LongTermBusinessInitiatives { get; set; } = string.Empty;

        // Leadership Capability

        public string LeadershipOverallComments { get; set; } = string.Empty;

        public string TeamCapabilityDevelopment { get; set; } = string.Empty;

        public string StructuredOneOnOnes { get; set; } = string.Empty;

        public string TerminatedPoorPerformers { get; set; } = string.Empty;

        public string LeadershipDevelopmentAreas { get; set; } = string.Empty;

        // Living The Axis Values

        public string ChallengesApplyingAxisValues { get; set; } = string.Empty;

        public string SelfDevelopmentActions { get; set; } = string.Empty;

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

        public string CareerAspirations { get; set; } = string.Empty;

        public MobilityType Mobility { get; set; }

        public RiskLevel RiskOfLeaving { get; set; }

        public bool CanOccupyHigherGrade { get; set; }

        public string NineBoxAssessment { get; set; } = string.Empty;

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