using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    public class EngagementStayInterview
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The employee this interview belongs to. Nullable only so historical rows captured before
        /// the employee register existed can be matched up gradually; new interviews always set it.
        /// </summary>
        public int? EmployeeId { get; set; }

        [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
        public Hr.Employee? Employee { get; set; }

        // Employee Information — a snapshot of the person's details at the time of the discussion.

        [Required(ErrorMessage = "Enter the employee name.")]
        [StringLength(150)]
        public string NameAndSurname { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the job title.")]
        [StringLength(150)]
        public string JobTitle { get; set; } = string.Empty;

        [StringLength(120)]
        public string Department { get; set; } = string.Empty;

        public DateTime? DateJoinedAxis { get; set; }

        [StringLength(150)]
        public string ManagerName { get; set; } = string.Empty;

        public DateTime? CurrentPositionStartDate { get; set; }

        public DateTime? DiscussionDate { get; set; }

        // Questions

        [StringLength(2000)]
        public string CurrentPrioritiesAndOverallWellbeing { get; set; } = string.Empty;

        [StringLength(2000)]
        public string MotivationAndEngagementFactors { get; set; } = string.Empty;

        [StringLength(2000)]
        public string DemotivatingFactors { get; set; } = string.Empty;

        [StringLength(2000)]
        public string SkillsUtilizationFeedback { get; set; } = string.Empty;

        [StringLength(2000)]
        public string ReasonsPeopleStay { get; set; } = string.Empty;

        [StringLength(2000)]
        public string ReasonsPeopleLeave { get; set; } = string.Empty;

        [StringLength(2000)]
        public string ChangesToWorkingAtAxis { get; set; } = string.Empty;

        [StringLength(1000)]
        public string NextCareerMilestone { get; set; } = string.Empty;

        public bool? FeelsSupported { get; set; }

        public bool? HasDevelopmentPlan { get; set; }

        [StringLength(2000)]
        public string ImprovementIdeas { get; set; } = string.Empty;

        // Overall Status

        public EngagementStatus OverallStatus { get; set; }

        // Rating Matrix

        public EngagementRating WellbeingRating { get; set; }
        [StringLength(1000)]
        public string WellbeingComment { get; set; } = string.Empty;

        public EngagementRating JobSatisfactionRating { get; set; }
        [StringLength(1000)]
        public string JobSatisfactionComment { get; set; } = string.Empty;

        public EngagementRating CareerOpportunitiesRating { get; set; }
        [StringLength(1000)]
        public string CareerOpportunitiesComment { get; set; } = string.Empty;

        public EngagementRating LeadershipQualityRating { get; set; }
        [StringLength(1000)]
        public string LeadershipQualityComment { get; set; } = string.Empty;

        public EngagementRating ManagerRelationshipRating { get; set; }
        [StringLength(1000)]
        public string ManagerRelationshipComment { get; set; } = string.Empty;

        public EngagementRating TeamRelationshipRating { get; set; }
        [StringLength(1000)]
        public string TeamRelationshipComment { get; set; } = string.Empty;

        public EngagementRating BSCSystemRating { get; set; }
        [StringLength(1000)]
        public string BSCSystemComment { get; set; } = string.Empty;

        public EngagementRating RewardForPerformanceRating { get; set; }
        [StringLength(1000)]
        public string RewardForPerformanceComment { get; set; } = string.Empty;

        public EngagementRating CommunicationChannelsRating { get; set; }
        [StringLength(1000)]
        public string CommunicationChannelsComment { get; set; } = string.Empty;

        public EngagementRating DevelopmentOpportunitiesRating { get; set; }
        [StringLength(1000)]
        public string DevelopmentOpportunitiesComment { get; set; } = string.Empty;

        public EngagementRating PayAndBenefitsRating { get; set; }
        [StringLength(1000)]
        public string PayAndBenefitsComment { get; set; } = string.Empty;

        public EngagementRating WorkingConditionsRating { get; set; }
        [StringLength(1000)]
        public string WorkingConditionsComment { get; set; } = string.Empty;

        public EngagementRating OrganizationGeneralRating { get; set; }
        [StringLength(1000)]
        public string OrganizationGeneralComment { get; set; } = string.Empty;

        public EngagementRating OtherRating { get; set; }
        [StringLength(1000)]
        public string OtherComment { get; set; } = string.Empty;

        // Interviewer Comments

        [StringLength(4000)]
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