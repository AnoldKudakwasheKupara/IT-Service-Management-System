using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class CreateEngagementStayInterview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EngagementStayInterviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameAndSurname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateJoinedAxis = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ManagerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentPositionStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiscussionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentPrioritiesAndOverallWellbeing = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MotivationAndEngagementFactors = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DemotivatingFactors = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SkillsUtilizationFeedback = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReasonsPeopleStay = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReasonsPeopleLeave = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangesToWorkingAtAxis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NextCareerMilestone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FeelsSupported = table.Column<bool>(type: "bit", nullable: true),
                    HasDevelopmentPlan = table.Column<bool>(type: "bit", nullable: true),
                    ImprovementIdeas = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OverallStatus = table.Column<int>(type: "int", nullable: false),
                    WellbeingRating = table.Column<int>(type: "int", nullable: false),
                    WellbeingComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JobSatisfactionRating = table.Column<int>(type: "int", nullable: false),
                    JobSatisfactionComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CareerOpportunitiesRating = table.Column<int>(type: "int", nullable: false),
                    CareerOpportunitiesComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeadershipQualityRating = table.Column<int>(type: "int", nullable: false),
                    LeadershipQualityComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ManagerRelationshipRating = table.Column<int>(type: "int", nullable: false),
                    ManagerRelationshipComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamRelationshipRating = table.Column<int>(type: "int", nullable: false),
                    TeamRelationshipComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BSCSystemRating = table.Column<int>(type: "int", nullable: false),
                    BSCSystemComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RewardForPerformanceRating = table.Column<int>(type: "int", nullable: false),
                    RewardForPerformanceComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommunicationChannelsRating = table.Column<int>(type: "int", nullable: false),
                    CommunicationChannelsComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DevelopmentOpportunitiesRating = table.Column<int>(type: "int", nullable: false),
                    DevelopmentOpportunitiesComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayAndBenefitsRating = table.Column<int>(type: "int", nullable: false),
                    PayAndBenefitsComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkingConditionsRating = table.Column<int>(type: "int", nullable: false),
                    WorkingConditionsComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrganizationGeneralRating = table.Column<int>(type: "int", nullable: false),
                    OrganizationGeneralComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherRating = table.Column<int>(type: "int", nullable: false),
                    OtherComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InterviewerOverallComments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngagementStayInterviews", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EngagementStayInterviews");
        }
    }
}
