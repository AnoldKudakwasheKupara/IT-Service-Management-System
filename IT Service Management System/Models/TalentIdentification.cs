namespace IT_Service_Management_System.Models
{
    public class TalentIdentification
    {
        public int Id { get; set; }

        // Employee Information

        public string EmployeeName { get; set; }

        public string Department { get; set; }

        public string JobTitle { get; set; }

        public string Country { get; set; }

        public DateTime? HireDate { get; set; }

        // Performance Track Record

        public string KPI2023 { get; set; }

        public string KPI2024 { get; set; }

        public string KPI2025 { get; set; }

        public string KPI2026 { get; set; }

        public string KeyProjectsLed { get; set; }

        public string DeliverySetbacks { get; set; }

        public string LongTermBusinessInitiatives { get; set; }

        // Leadership Capability

        public string LeadershipOverallComments { get; set; }

        public string TeamCapabilityDevelopment { get; set; }

        public string StructuredOneOnOnes { get; set; }

        public string TerminatedPoorPerformers { get; set; }

        public string LeadershipDevelopmentAreas { get; set; }

        // Living The Axis Values

        public string ChallengesApplyingAxisValues { get; set; }

        public string SelfDevelopmentActions { get; set; }

        public string SelfInitiatedLeadershipDevelopment { get; set; }

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

        public string CareerAspirations { get; set; }

        public MobilityType Mobility { get; set; }

        public RiskLevel RiskOfLeaving { get; set; }

        public bool CanOccupyHigherGrade { get; set; }

        public string NineBoxAssessment { get; set; }

        public string NextCareerMilestone { get; set; }

        public ReadinessLevel Readiness { get; set; }

        // Audit

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public bool IsDeleted { get; set; }

        public ICollection<TalentDirectReportAssessment> DirectReports { get; set; }

        public ICollection<TalentDevelopmentAction> DevelopmentActions { get; set; }
    }
}