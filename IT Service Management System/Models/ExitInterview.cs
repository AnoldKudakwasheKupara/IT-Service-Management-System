using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    public class ExitInterview
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

        // Employee Information — kept as a snapshot of what the person's details were at the time
        // of the interview, which is what an exit record should preserve.
        [Required]
        [StringLength(150)]
        public string EmployeeName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the position held.")]
        [StringLength(150)]
        public string Position { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Client { get; set; }

        public DateTime? DateOfResignation { get; set; }

        public DateTime? LastWorkingDay { get; set; }

        [StringLength(150)]
        public string? InterviewConductedBy { get; set; }

        public DateTime? InterviewDate { get; set; }

        // Primary Reason for Leaving
        [StringLength(200)]
        public string? PrimaryReasonForDeparture { get; set; }

        // Section 1: Overall Views
        public Rating CareerGrowthOpportunities { get; set; }

        public Rating CompensationAndBenefits { get; set; }

        public Rating WorkLifeBalanceRating { get; set; }

        public Rating ManagementLeadershipStyle { get; set; }

        public Rating CompanyCultureWorkEnvironment { get; set; }

        public Rating JobResponsibilities { get; set; }

        public Rating RelationshipWithManagerRating { get; set; }

        [StringLength(1000)]
        public string? OtherReasonDescription { get; set; }

        public Rating? OtherRating { get; set; }

        // Section 2: Job Satisfaction & Role Clarity
        public bool? RoleMetExpectations { get; set; }

        public bool? ResponsibilitiesClearlyDefined { get; set; }

        public bool? AdequateTrainingAndResources { get; set; }

        [StringLength(2000)]
        public string? JobSatisfactionComments { get; set; }

        // Section 3: Management & Team Dynamics
        [StringLength(2000)]
        public string? RelationshipWithManagerDescription { get; set; }

        public bool? SupportedByTeamAndLeadership { get; set; }

        [StringLength(2000)]
        public string? CommunicationCollaborationSuggestions { get; set; }

        // Section 4: Compensation & Benefits
        public bool? SatisfiedWithSalaryAndBenefits { get; set; }

        [StringLength(1000)]
        public string? CompensationMarketCompetitiveness { get; set; }

        // Section 5: Work Environment & Culture
        public bool? FeltValuedAndRecognized { get; set; }

        [StringLength(2000)]
        public string? MostLikedAboutCompany { get; set; }

        [StringLength(2000)]
        public string? CultureImprovementSuggestions { get; set; }

        // Section 6: Suggestions for Improvement
        [StringLength(2000)]
        public string? EmployeeRetentionRecommendations { get; set; }

        [StringLength(2000)]
        public string? ResignationPreventionSuggestions { get; set; }

        // Section 7: Future Engagement
        public bool? WouldReturnToCompany { get; set; }

        public bool? WouldRecommendCompany { get; set; }

        // Section 8: Work-Life Balance
        [StringLength(2000)]
        public string? WorkLifeBalanceComments { get; set; }

        // Section 9: Additional Comments
        [StringLength(4000)]
        public string? AdditionalComments { get; set; }

        // Sign-off
        [StringLength(200)]
        public string? EmployeeSignature { get; set; }

        [StringLength(200)]
        public string? HRRepresentativeSignature { get; set; }

        public DateTime? SignOffDate { get; set; }

        // Audit Fields
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ModifiedDate { get; set; }

        public bool IsDeleted { get; set; } = false;
    }

    public enum Rating
    {
        NotSelected = 0,
        MetExpectations = 1,
        NeedsImprovement = 2,
        DidNotMeetExpectations = 3
    }
}