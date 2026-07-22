using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    public class EngagementStayInterview
    {
        [Key]
        public int Id { get; set; }

        // Employee Information

        public string NameAndSurname { get; set; } = string.Empty;

        public string JobTitle { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public DateTime? DateJoinedAxis { get; set; }

        public string ManagerName { get; set; } = string.Empty;

        public DateTime? CurrentPositionStartDate { get; set; }

        public DateTime? DiscussionDate { get; set; }

        // Questions

        public string CurrentPrioritiesAndOverallWellbeing { get; set; } = string.Empty;

        public string MotivationAndEngagementFactors { get; set; } = string.Empty;

        public string DemotivatingFactors { get; set; } = string.Empty;

        public string SkillsUtilizationFeedback { get; set; } = string.Empty;

        public string ReasonsPeopleStay { get; set; } = string.Empty;

        public string ReasonsPeopleLeave { get; set; } = string.Empty;

        public string ChangesToWorkingAtAxis { get; set; } = string.Empty;

        public string NextCareerMilestone { get; set; } = string.Empty;

        public bool? FeelsSupported { get; set; }

        public bool? HasDevelopmentPlan { get; set; }

        public string ImprovementIdeas { get; set; } = string.Empty;

        // Overall Status

        public EngagementStatus OverallStatus { get; set; }

        // Rating Matrix

        public EngagementRating WellbeingRating { get; set; }
        public string WellbeingComment { get; set; } = string.Empty;

        public EngagementRating JobSatisfactionRating { get; set; }
        public string JobSatisfactionComment { get; set; } = string.Empty;

        public EngagementRating CareerOpportunitiesRating { get; set; }
        public string CareerOpportunitiesComment { get; set; } = string.Empty;

        public EngagementRating LeadershipQualityRating { get; set; }
        public string LeadershipQualityComment { get; set; } = string.Empty;

        public EngagementRating ManagerRelationshipRating { get; set; }
        public string ManagerRelationshipComment { get; set; } = string.Empty;

        public EngagementRating TeamRelationshipRating { get; set; }
        public string TeamRelationshipComment { get; set; } = string.Empty;

        public EngagementRating BSCSystemRating { get; set; }
        public string BSCSystemComment { get; set; } = string.Empty;

        public EngagementRating RewardForPerformanceRating { get; set; }
        public string RewardForPerformanceComment { get; set; } = string.Empty;

        public EngagementRating CommunicationChannelsRating { get; set; }
        public string CommunicationChannelsComment { get; set; } = string.Empty;

        public EngagementRating DevelopmentOpportunitiesRating { get; set; }
        public string DevelopmentOpportunitiesComment { get; set; } = string.Empty;

        public EngagementRating PayAndBenefitsRating { get; set; }
        public string PayAndBenefitsComment { get; set; } = string.Empty;

        public EngagementRating WorkingConditionsRating { get; set; }
        public string WorkingConditionsComment { get; set; } = string.Empty;

        public EngagementRating OrganizationGeneralRating { get; set; }
        public string OrganizationGeneralComment { get; set; } = string.Empty;

        public EngagementRating OtherRating { get; set; }
        public string OtherComment { get; set; } = string.Empty;

        // Interviewer Comments

        public string InterviewerOverallComments { get; set; } = string.Empty;

        // Audit Fields

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ModifiedDate { get; set; }

        public bool IsDeleted { get; set; } = false;
    }

    public enum EngagementStatus
    {
        NotSelected = 0,
        ConsideringOpportunitiesOutsideAxis = 1,
        DissatisfiedWithWorkingAtAxis = 2,
        SatisfiedWithWorkingAtAxis = 3,
        HighlyMotivatedAndEngaged = 4
    }

    public enum EngagementRating
    {
        NotSelected = 0,
        NotMeetingExpectations = 1,
        NeedsImprovement = 2,
        MeetingMyExpectations = 3
    }

}