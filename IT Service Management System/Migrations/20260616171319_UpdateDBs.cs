using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDBs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TalentIdentifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HireDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KPI2023 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KPI2024 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KPI2025 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KPI2026 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyProjectsLed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliverySetbacks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LongTermBusinessInitiatives = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeadershipOverallComments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamCapabilityDevelopment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StructuredOneOnOnes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TerminatedPoorPerformers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeadershipDevelopmentAreas = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChallengesApplyingAxisValues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelfDevelopmentActions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelfInitiatedLeadershipDevelopment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ThinkingComplexity = table.Column<bool>(type: "bit", nullable: false),
                    ThinkingJudgement = table.Column<bool>(type: "bit", nullable: false),
                    ThinkingScale = table.Column<bool>(type: "bit", nullable: false),
                    WisdomFastJudgement = table.Column<bool>(type: "bit", nullable: false),
                    WisdomWhenToAct = table.Column<bool>(type: "bit", nullable: false),
                    WisdomLongTermImpact = table.Column<bool>(type: "bit", nullable: false),
                    CourageRiskTaking = table.Column<bool>(type: "bit", nullable: false),
                    CourageConvictions = table.Column<bool>(type: "bit", nullable: false),
                    SelfAwareness = table.Column<bool>(type: "bit", nullable: false),
                    OpenToFeedback = table.Column<bool>(type: "bit", nullable: false),
                    WillingToImprove = table.Column<bool>(type: "bit", nullable: false),
                    LearningAgility = table.Column<bool>(type: "bit", nullable: false),
                    Resilience = table.Column<bool>(type: "bit", nullable: false),
                    CareerAspirations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mobility = table.Column<int>(type: "int", nullable: false),
                    RiskOfLeaving = table.Column<int>(type: "int", nullable: false),
                    CanOccupyHigherGrade = table.Column<bool>(type: "bit", nullable: false),
                    NineBoxAssessment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NextCareerMilestone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Readiness = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalentIdentifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TalentDevelopmentActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TalentIdentificationId = table.Column<int>(type: "int", nullable: false),
                    DevelopmentAction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Objective = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timeline = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalentDevelopmentActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TalentDevelopmentActions_TalentIdentifications_TalentIdentificationId",
                        column: x => x.TalentIdentificationId,
                        principalTable: "TalentIdentifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TalentDirectReportAssessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TalentIdentificationId = table.Column<int>(type: "int", nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PerformanceRating = table.Column<int>(type: "int", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalentDirectReportAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TalentDirectReportAssessments_TalentIdentifications_TalentIdentificationId",
                        column: x => x.TalentIdentificationId,
                        principalTable: "TalentIdentifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TalentDevelopmentActions_TalentIdentificationId",
                table: "TalentDevelopmentActions",
                column: "TalentIdentificationId");

            migrationBuilder.CreateIndex(
                name: "IX_TalentDirectReportAssessments_TalentIdentificationId",
                table: "TalentDirectReportAssessments",
                column: "TalentIdentificationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TalentDevelopmentActions");

            migrationBuilder.DropTable(
                name: "TalentDirectReportAssessments");

            migrationBuilder.DropTable(
                name: "TalentIdentifications");
        }
    }
}
