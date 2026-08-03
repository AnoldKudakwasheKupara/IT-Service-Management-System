using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "TalentIdentifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "ExitInterviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "EngagementStayInterviews",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PreferredName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NationalId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    MaritalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    WorkEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PersonalEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    WorkNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NextOfKinName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    NextOfKinRelationship = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    NextOfKinPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EmergencyContactName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    ManagerId = table.Column<int>(type: "int", nullable: true),
                    EmploymentType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Grade = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    HireDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProbationEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerminationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerminationReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Client = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    TaxNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SocialSecurityNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_Employees_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TalentIdentifications_EmployeeId",
                table: "TalentIdentifications",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExitInterviews_EmployeeId",
                table: "ExitInterviews",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementStayInterviews_EmployeeId",
                table: "EngagementStayInterviews",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId",
                table: "Employees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeNumber",
                table: "Employees",
                column: "EmployeeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_HireDate",
                table: "Employees",
                column: "HireDate");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Status_DepartmentId",
                table: "Employees",
                columns: new[] { "Status", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_EngagementStayInterviews_Employees_EmployeeId",
                table: "EngagementStayInterviews",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExitInterviews_Employees_EmployeeId",
                table: "ExitInterviews",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TalentIdentifications_Employees_EmployeeId",
                table: "TalentIdentifications",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EngagementStayInterviews_Employees_EmployeeId",
                table: "EngagementStayInterviews");

            migrationBuilder.DropForeignKey(
                name: "FK_ExitInterviews_Employees_EmployeeId",
                table: "ExitInterviews");

            migrationBuilder.DropForeignKey(
                name: "FK_TalentIdentifications_Employees_EmployeeId",
                table: "TalentIdentifications");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_TalentIdentifications_EmployeeId",
                table: "TalentIdentifications");

            migrationBuilder.DropIndex(
                name: "IX_ExitInterviews_EmployeeId",
                table: "ExitInterviews");

            migrationBuilder.DropIndex(
                name: "IX_EngagementStayInterviews_EmployeeId",
                table: "EngagementStayInterviews");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "TalentIdentifications");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "ExitInterviews");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "EngagementStayInterviews");
        }
    }
}
