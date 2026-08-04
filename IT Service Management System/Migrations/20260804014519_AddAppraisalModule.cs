using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppraisalCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SelfAssessmentDue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ManagerReviewDue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsProbationReview = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Appraisals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CycleId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ReviewerId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SelfAchievements = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    SelfChallenges = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    SelfDevelopmentNeeds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SelfAssessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewerComments = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    DevelopmentPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OverallRating = table.Column<int>(type: "int", nullable: true),
                    RatingReasons = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModeratedRating = table.Column<int>(type: "int", nullable: true),
                    ModerationReasons = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ModeratedById = table.Column<int>(type: "int", nullable: true),
                    ModeratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmployeeAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    EmployeeComments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EmployeeDisagrees = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appraisals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appraisals_AppraisalCycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "AppraisalCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Appraisals_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appraisals_Employees_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Appraisals_Users_ModeratedById",
                        column: x => x.ModeratedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalObjectives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppraisalId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SuccessMeasure = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TargetDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AchievementPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Evidence = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AgreedUpFront = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalObjectives_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceImprovementPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    AppraisalId = table.Column<int>(type: "int", nullable: true),
                    ManagerId = table.Column<int>(type: "int", nullable: true),
                    Concern = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RequiredStandard = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SupportOffered = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiscussedWithEmployee = table.Column<bool>(type: "bit", nullable: false),
                    EmployeeComments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceImprovementPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceImprovementPlans_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PerformanceImprovementPlans_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceImprovementPlans_Employees_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_Status",
                table: "AppraisalCycles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalObjectives_AppraisalId_DisplayOrder",
                table: "AppraisalObjectives",
                columns: new[] { "AppraisalId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_CycleId_EmployeeId",
                table: "Appraisals",
                columns: new[] { "CycleId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_EmployeeId",
                table: "Appraisals",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_ModeratedById",
                table: "Appraisals",
                column: "ModeratedById");

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_ReviewerId",
                table: "Appraisals",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_Status",
                table: "Appraisals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_AppraisalId",
                table: "PerformanceImprovementPlans",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_EmployeeId_Status",
                table: "PerformanceImprovementPlans",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_ManagerId",
                table: "PerformanceImprovementPlans",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_ReviewDate",
                table: "PerformanceImprovementPlans",
                column: "ReviewDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppraisalObjectives");

            migrationBuilder.DropTable(
                name: "PerformanceImprovementPlans");

            migrationBuilder.DropTable(
                name: "Appraisals");

            migrationBuilder.DropTable(
                name: "AppraisalCycles");
        }
    }
}
