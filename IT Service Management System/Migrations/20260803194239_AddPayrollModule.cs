using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PercentageOfBasic = table.Column<decimal>(type: "decimal(9,4)", nullable: true),
                    IsTaxable = table.Column<bool>(type: "bit", nullable: false),
                    IsPensionable = table.Column<bool>(type: "bit", nullable: false),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayComponents_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PayDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatutoryAsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalGross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPaye = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAidsLevy = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalNssaEmployee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalNssaEmployer = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalOtherDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalNet = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalZimdef = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalStandardsLevy = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployeeCount = table.Column<int>(type: "int", nullable: false),
                    PreparedById = table.Column<int>(type: "int", nullable: true),
                    PreparedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_Users_PreparedById",
                        column: x => x.PreparedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryStructures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PensionContribution = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MedicalAidContribution = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryStructures_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalaryStructures_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payslips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollRunId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Allowances = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Overtime = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OtherEarnings = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reimbursements = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Gross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxableIncome = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Paye = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AidsLevy = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NssaEmployee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NssaInsurableEarnings = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PensionContribution = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MedicalAid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LoanRepayments = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OtherDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Net = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NssaEmployer = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NssaAccidentPrevention = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Zimdef = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StandardsLevy = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnpaidLeaveDays = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    UnpaidLeaveDeduction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HalfPayLeaveDays = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    HalfPayLeaveDeduction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MarginalTaxRate = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payslips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payslips_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payslips_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayslipLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayslipId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Basis = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayslipLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayslipLines_Payslips_PayslipId",
                        column: x => x.PayslipId,
                        principalTable: "Payslips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayComponents_EmployeeId_IsActive_EffectiveFrom",
                table: "PayComponents",
                columns: new[] { "EmployeeId", "IsActive", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_ApprovedById",
                table: "PayrollRuns",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_PeriodYear_PeriodMonth_Currency",
                table: "PayrollRuns",
                columns: new[] { "PeriodYear", "PeriodMonth", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_PreparedById",
                table: "PayrollRuns",
                column: "PreparedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayslipLines_PayslipId_DisplayOrder",
                table: "PayslipLines",
                columns: new[] { "PayslipId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_EmployeeId",
                table: "Payslips",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PayrollRunId_EmployeeId",
                table: "Payslips",
                columns: new[] { "PayrollRunId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryStructures_CreatedById",
                table: "SalaryStructures",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryStructures_EmployeeId_EffectiveFrom",
                table: "SalaryStructures",
                columns: new[] { "EmployeeId", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayComponents");

            migrationBuilder.DropTable(
                name: "PayslipLines");

            migrationBuilder.DropTable(
                name: "SalaryStructures");

            migrationBuilder.DropTable(
                name: "Payslips");

            migrationBuilder.DropTable(
                name: "PayrollRuns");
        }
    }
}
