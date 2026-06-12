using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class CreateExitInterviewTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExitInterviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Client = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfResignation = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastWorkingDay = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InterviewConductedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InterviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PrimaryReasonForDeparture = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CareerGrowthOpportunities = table.Column<int>(type: "int", nullable: false),
                    CompensationAndBenefits = table.Column<int>(type: "int", nullable: false),
                    WorkLifeBalanceRating = table.Column<int>(type: "int", nullable: false),
                    ManagementLeadershipStyle = table.Column<int>(type: "int", nullable: false),
                    CompanyCultureWorkEnvironment = table.Column<int>(type: "int", nullable: false),
                    JobResponsibilities = table.Column<int>(type: "int", nullable: false),
                    RelationshipWithManagerRating = table.Column<int>(type: "int", nullable: false),
                    OtherReasonDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherRating = table.Column<int>(type: "int", nullable: false),
                    RoleMetExpectations = table.Column<bool>(type: "bit", nullable: true),
                    ResponsibilitiesClearlyDefined = table.Column<bool>(type: "bit", nullable: true),
                    AdequateTrainingAndResources = table.Column<bool>(type: "bit", nullable: true),
                    JobSatisfactionComments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RelationshipWithManagerDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupportedByTeamAndLeadership = table.Column<bool>(type: "bit", nullable: true),
                    CommunicationCollaborationSuggestions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SatisfiedWithSalaryAndBenefits = table.Column<bool>(type: "bit", nullable: true),
                    CompensationMarketCompetitiveness = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FeltValuedAndRecognized = table.Column<bool>(type: "bit", nullable: true),
                    MostLikedAboutCompany = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CultureImprovementSuggestions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeRetentionRecommendations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResignationPreventionSuggestions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WouldReturnToCompany = table.Column<bool>(type: "bit", nullable: true),
                    WouldRecommendCompany = table.Column<bool>(type: "bit", nullable: true),
                    WorkLifeBalanceComments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdditionalComments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeSignature = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HRRepresentativeSignature = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignOffDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExitInterviews", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExitInterviews");
        }
    }
}
