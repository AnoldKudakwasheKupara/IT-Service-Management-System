using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OnboardingTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AppliesTo = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingProgrammes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BuddyId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingProgrammes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingProgrammes_Employees_BuddyId",
                        column: x => x.BuddyId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OnboardingProgrammes_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OnboardingProgrammes_OnboardingTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "OnboardingTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingTaskTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Owner = table.Column<int>(type: "int", nullable: false),
                    DueDayOffset = table.Column<int>(type: "int", nullable: false),
                    IsStatutory = table.Column<bool>(type: "bit", nullable: false),
                    Authority = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingTaskTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingTaskTemplates_OnboardingTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "OnboardingTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProgrammeId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Owner = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsStatutory = table.Column<bool>(type: "bit", nullable: false),
                    Authority = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsComplete = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedById = table.Column<int>(type: "int", nullable: true),
                    Evidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingTasks_OnboardingProgrammes_ProgrammeId",
                        column: x => x.ProgrammeId,
                        principalTable: "OnboardingProgrammes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OnboardingTasks_Users_CompletedById",
                        column: x => x.CompletedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProgrammes_BuddyId",
                table: "OnboardingProgrammes",
                column: "BuddyId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProgrammes_EmployeeId_Status",
                table: "OnboardingProgrammes",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProgrammes_TemplateId",
                table: "OnboardingProgrammes",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTasks_CompletedById",
                table: "OnboardingTasks",
                column: "CompletedById");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTasks_IsComplete_DueDate",
                table: "OnboardingTasks",
                columns: new[] { "IsComplete", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTasks_ProgrammeId_DisplayOrder",
                table: "OnboardingTasks",
                columns: new[] { "ProgrammeId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTaskTemplates_TemplateId_DisplayOrder",
                table: "OnboardingTaskTemplates",
                columns: new[] { "TemplateId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTemplates_IsActive_AppliesTo",
                table: "OnboardingTemplates",
                columns: new[] { "IsActive", "AppliesTo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingTasks");

            migrationBuilder.DropTable(
                name: "OnboardingTaskTemplates");

            migrationBuilder.DropTable(
                name: "OnboardingProgrammes");

            migrationBuilder.DropTable(
                name: "OnboardingTemplates");
        }
    }
}
