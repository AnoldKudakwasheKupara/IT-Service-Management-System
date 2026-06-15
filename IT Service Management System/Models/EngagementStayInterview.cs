using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    public class EngagementStayInterview
    {
        [Key]
        public int Id { get; set; }

        // Employee Information

        public string NameAndSurname { get; set; }

        public string JobTitle { get; set; }

        public string Department { get; set; }

        public DateTime? DateJoinedAxis { get; set; }

        public string ManagerName { get; set; }

        public DateTime? CurrentPositionStartDate { get; set; }

        public DateTime? DiscussionDate { get; set; }

        // Questions

        public string CurrentPrioritiesAndOverallWellbeing { get; set; }

        public string MotivationAndEngagementFactors { get; set; }

        public string DemotivatingFactors { get; set; }

        public string SkillsUtilizationFeedback { get; set; }

        public string ReasonsPeopleStay { get; set; }

        public string ReasonsPeopleLeave { get; set; }

        public string ChangesToWorkingAtAxis { get; set; }

        public string NextCareerMilestone { get; set; }

        public bool? FeelsSupported { get; set; }

        public bool? HasDevelopmentPlan { get; set; }

        public string ImprovementIdeas { get; set; }

        // Overall Status

        public EngagementStatus OverallStatus { get; set; }

        // Rating Matrix

        public EngagementRating WellbeingRating { get; set; }
        public string WellbeingComment { get; set; }

        public EngagementRating JobSatisfactionRating { get; set; }
        public string JobSatisfactionComment { get; set; }

        public EngagementRating CareerOpportunitiesRating { get; set; }
        public string CareerOpportunitiesComment { get; set; }

        public EngagementRating LeadershipQualityRating { get; set; }
        public string LeadershipQualityComment { get; set; }

        public EngagementRating ManagerRelationshipRating { get; set; }
        public string ManagerRelationshipComment { get; set; }

        public EngagementRating TeamRelationshipRating { get; set; }
        public string TeamRelationshipComment { get; set; }

        public EngagementRating BSCSystemRating { get; set; }
        public string BSCSystemComment { get; set; }

        public EngagementRating RewardForPerformanceRating { get; set; }
        public string RewardForPerformanceComment { get; set; }

        public EngagementRating CommunicationChannelsRating { get; set; }
        public string CommunicationChannelsComment { get; set; }

        public EngagementRating DevelopmentOpportunitiesRating { get; set; }
        public string DevelopmentOpportunitiesComment { get; set; }

        public EngagementRating PayAndBenefitsRating { get; set; }
        public string PayAndBenefitsComment { get; set; }

        public EngagementRating WorkingConditionsRating { get; set; }
        public string WorkingConditionsComment { get; set; }

        public EngagementRating OrganizationGeneralRating { get; set; }
        public string OrganizationGeneralComment { get; set; }

        public EngagementRating OtherRating { get; set; }
        public string OtherComment { get; set; }

        // Interviewer Comments

        public string InterviewerOverallComments { get; set; }

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