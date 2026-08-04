using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruitmentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobRequisitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    Grade = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Positions = table.Column<int>(type: "int", nullable: false),
                    EmploymentType = table.Column<int>(type: "int", nullable: false),
                    ContractEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReplacingEmployeeId = table.Column<int>(type: "int", nullable: true),
                    ReportsToEmployeeId = table.Column<int>(type: "int", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EssentialRequirements = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DesirableRequirements = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SalaryMin = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SalaryMax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequiredByDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RaisedById = table.Column<int>(type: "int", nullable: false),
                    ApprovedById = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequisitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobRequisitions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobRequisitions_Employees_ReplacingEmployeeId",
                        column: x => x.ReplacingEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobRequisitions_Employees_ReportsToEmployeeId",
                        column: x => x.ReportsToEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobRequisitions_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobRequisitions_Users_RaisedById",
                        column: x => x.RaisedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vacancies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequisitionId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AdvertText = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    OpenToInternal = table.Column<bool>(type: "bit", nullable: false),
                    OpenToExternal = table.Column<bool>(type: "bit", nullable: false),
                    OpenDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CloseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdvertisedIn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vacancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vacancies_JobRequisitions_RequisitionId",
                        column: x => x.RequisitionId,
                        principalTable: "JobRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VacancyId = table.Column<int>(type: "int", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    NationalId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    EntitledToWork = table.Column<bool>(type: "bit", nullable: false),
                    HighestQualification = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    YearsExperience = table.Column<int>(type: "int", nullable: false),
                    CoveringStatement = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CvFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    CvFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApplications_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobApplications_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SelectionCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VacancyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descriptor = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    IsEssential = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelectionCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SelectionCriteria_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateInterviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Venue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Panel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Held = table.Column<bool>(type: "bit", nullable: false),
                    CandidateAttended = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    Recommendation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: true),
                    ArrangedById = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateInterviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateInterviews_JobApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CandidateInterviews_Users_ArrangedById",
                        column: x => x.ArrangedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Grade = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    EmploymentType = table.Column<int>(type: "int", nullable: false),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProbationMonths = table.Column<int>(type: "int", nullable: false),
                    ContractEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtherTerms = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedEmployeeId = table.Column<int>(type: "int", nullable: true),
                    IssuedById = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobOffers_Employees_CreatedEmployeeId",
                        column: x => x.CreatedEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobOffers_JobApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobOffers_Users_IssuedById",
                        column: x => x.IssuedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CandidateScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    CriterionId = table.Column<int>(type: "int", nullable: false),
                    InterviewId = table.Column<int>(type: "int", nullable: true),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ScoredById = table.Column<int>(type: "int", nullable: true),
                    ScoredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateScores_CandidateInterviews_InterviewId",
                        column: x => x.InterviewId,
                        principalTable: "CandidateInterviews",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CandidateScores_JobApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CandidateScores_SelectionCriteria_CriterionId",
                        column: x => x.CriterionId,
                        principalTable: "SelectionCriteria",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CandidateScores_Users_ScoredById",
                        column: x => x.ScoredById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateInterviews_ApplicationId_ScheduledFor",
                table: "CandidateInterviews",
                columns: new[] { "ApplicationId", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateInterviews_ArrangedById",
                table: "CandidateInterviews",
                column: "ArrangedById");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateScores_ApplicationId_CriterionId_InterviewId",
                table: "CandidateScores",
                columns: new[] { "ApplicationId", "CriterionId", "InterviewId" },
                unique: true,
                filter: "[InterviewId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateScores_CriterionId",
                table: "CandidateScores",
                column: "CriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateScores_InterviewId",
                table: "CandidateScores",
                column: "InterviewId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateScores_ScoredById",
                table: "CandidateScores",
                column: "ScoredById");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_Email",
                table: "JobApplications",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_EmployeeId",
                table: "JobApplications",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_VacancyId_Status",
                table: "JobApplications",
                columns: new[] { "VacancyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_ApplicationId",
                table: "JobOffers",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_CreatedEmployeeId",
                table: "JobOffers",
                column: "CreatedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_IssuedById",
                table: "JobOffers",
                column: "IssuedById");

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_Status",
                table: "JobOffers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_ApprovedById",
                table: "JobRequisitions",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_DepartmentId",
                table: "JobRequisitions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_RaisedById",
                table: "JobRequisitions",
                column: "RaisedById");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_ReplacingEmployeeId",
                table: "JobRequisitions",
                column: "ReplacingEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_ReportsToEmployeeId",
                table: "JobRequisitions",
                column: "ReportsToEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_Status",
                table: "JobRequisitions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SelectionCriteria_VacancyId_DisplayOrder",
                table: "SelectionCriteria",
                columns: new[] { "VacancyId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_RequisitionId",
                table: "Vacancies",
                column: "RequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_Status_CloseDate",
                table: "Vacancies",
                columns: new[] { "Status", "CloseDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateScores");

            migrationBuilder.DropTable(
                name: "JobOffers");

            migrationBuilder.DropTable(
                name: "CandidateInterviews");

            migrationBuilder.DropTable(
                name: "SelectionCriteria");

            migrationBuilder.DropTable(
                name: "JobApplications");

            migrationBuilder.DropTable(
                name: "Vacancies");

            migrationBuilder.DropTable(
                name: "JobRequisitions");
        }
    }
}
