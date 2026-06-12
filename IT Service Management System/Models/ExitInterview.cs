using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    public class ExitInterview
    {
        [Key]
        public int Id { get; set; }

        // Employee Information
        [Required]
        public string EmployeeName { get; set; }

        public string Position { get; set; }

        public string? Client { get; set; }

        public DateTime? DateOfResignation { get; set; }

        public DateTime? LastWorkingDay { get; set; }

        public string? InterviewConductedBy { get; set; }

        public DateTime? InterviewDate { get; set; }

        // Primary Reason for Leaving
        public string? PrimaryReasonForDeparture { get; set; }

        // Section 1: Overall Views
        public Rating CareerGrowthOpportunities { get; set; }

        public Rating CompensationAndBenefits { get; set; }

        public Rating WorkLifeBalanceRating { get; set; }

        public Rating ManagementLeadershipStyle { get; set; }

        public Rating CompanyCultureWorkEnvironment { get; set; }

        public Rating JobResponsibilities { get; set; }

        public Rating RelationshipWithManagerRating { get; set; }

        public string? OtherReasonDescription { get; set; }

        public Rating? OtherRating { get; set; }

        // Section 2: Job Satisfaction & Role Clarity
        public bool? RoleMetExpectations { get; set; }

        public bool? ResponsibilitiesClearlyDefined { get; set; }

        public bool? AdequateTrainingAndResources { get; set; }

        public string? JobSatisfactionComments { get; set; }

        // Section 3: Management & Team Dynamics
        public string? RelationshipWithManagerDescription { get; set; }

        public bool? SupportedByTeamAndLeadership { get; set; }

        public string? CommunicationCollaborationSuggestions { get; set; }

        // Section 4: Compensation & Benefits
        public bool? SatisfiedWithSalaryAndBenefits { get; set; }

        public string? CompensationMarketCompetitiveness { get; set; }

        // Section 5: Work Environment & Culture
        public bool? FeltValuedAndRecognized { get; set; }

        public string? MostLikedAboutCompany { get; set; }

        public string? CultureImprovementSuggestions { get; set; }

        // Section 6: Suggestions for Improvement
        public string? EmployeeRetentionRecommendations { get; set; }

        public string? ResignationPreventionSuggestions { get; set; }

        // Section 7: Future Engagement
        public bool? WouldReturnToCompany { get; set; }

        public bool? WouldRecommendCompany { get; set; }

        // Section 8: Work-Life Balance
        public string? WorkLifeBalanceComments { get; set; }

        // Section 9: Additional Comments
        public string? AdditionalComments { get; set; }

        // Sign-off
        public string? EmployeeSignature { get; set; }

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