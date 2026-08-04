using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddBenefitsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BenefitPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    TaxTreatment = table.Column<int>(type: "int", nullable: false),
                    TaxAuthority = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Basis = table.Column<int>(type: "int", nullable: false),
                    EmployerAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployerRate = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    EmployeeRate = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    QualifyingMonths = table.Column<int>(type: "int", nullable: false),
                    AvailableTo = table.Column<int>(type: "int", nullable: true),
                    AllowsDependants = table.Column<bool>(type: "bit", nullable: false),
                    MaxDependants = table.Column<int>(type: "int", nullable: false),
                    CostPerDependant = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsAutomatic = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenefitPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BenefitEnrolments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MembershipNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    EmployeeAmountOverride = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EmployerAmountOverride = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EndReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenefitEnrolments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenefitEnrolments_BenefitPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "BenefitPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BenefitEnrolments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BenefitDependants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrolmentId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AddedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenefitDependants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenefitDependants_BenefitEnrolments_EnrolmentId",
                        column: x => x.EnrolmentId,
                        principalTable: "BenefitEnrolments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BenefitDependants_EnrolmentId_RemovedOn",
                table: "BenefitDependants",
                columns: new[] { "EnrolmentId", "RemovedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_BenefitEnrolments_EmployeeId_PlanId_StartDate",
                table: "BenefitEnrolments",
                columns: new[] { "EmployeeId", "PlanId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BenefitEnrolments_EndDate",
                table: "BenefitEnrolments",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_BenefitEnrolments_PlanId",
                table: "BenefitEnrolments",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_BenefitPlans_EffectiveFrom",
                table: "BenefitPlans",
                column: "EffectiveFrom");

            migrationBuilder.CreateIndex(
                name: "IX_BenefitPlans_IsActive_Category",
                table: "BenefitPlans",
                columns: new[] { "IsActive", "Category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BenefitDependants");

            migrationBuilder.DropTable(
                name: "BenefitEnrolments");

            migrationBuilder.DropTable(
                name: "BenefitPlans");
        }
    }
}
