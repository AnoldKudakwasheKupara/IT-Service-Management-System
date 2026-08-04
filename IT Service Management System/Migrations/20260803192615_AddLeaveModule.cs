using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaveTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    Authority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AnnualEntitlementDays = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    AccrualPerMonth = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    HasHalfPayTier = table.Column<bool>(type: "bit", nullable: false),
                    HalfPayDays = table.Column<int>(type: "int", nullable: false),
                    MaxCarryOverDays = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    QualifyingMonths = table.Column<int>(type: "int", nullable: false),
                    RequiresMedicalCertificate = table.Column<bool>(type: "bit", nullable: false),
                    CertificateRequiredAfterDays = table.Column<int>(type: "int", nullable: false),
                    RestrictedToGender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CountsWorkingDaysOnly = table.Column<bool>(type: "bit", nullable: false),
                    NoticeDaysRequired = table.Column<int>(type: "int", nullable: false),
                    PaidOutOnTermination = table.Column<bool>(type: "bit", nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "int", nullable: false),
                    CycleYear = table.Column<int>(type: "int", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    Accrued = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    Taken = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    Booked = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    Pending = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    Adjustment = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    AdjustmentReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HalfPayTaken = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Days = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    FullPayDays = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    HalfPayDays = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    UnpaidDays = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    IsHalfDay = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CoveringEmployeeId = table.Column<int>(type: "int", nullable: true),
                    ContactWhileAway = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DocumentFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    DocumentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ManagerApprovedById = table.Column<int>(type: "int", nullable: true),
                    ManagerApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HrApprovedById = table.Column<int>(type: "int", nullable: true),
                    HrApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubmittedById = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Employees_CoveringEmployeeId",
                        column: x => x.CoveringEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Employees_HrApprovedById",
                        column: x => x.HrApprovedById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Employees_ManagerApprovedById",
                        column: x => x.ManagerApprovedById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeaveRequests_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Users_SubmittedById",
                        column: x => x.SubmittedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "int", nullable: false),
                    LeaveRequestId = table.Column<int>(type: "int", nullable: true),
                    CycleYear = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Days = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    Narrative = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RecordedById = table.Column<int>(type: "int", nullable: true),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        // No action, not cascade: the ledger is already reachable from Employees
                        // through LeaveRequests, and SQL Server will not create a second cascade
                        // path into the same table.
                        name: "FK_LeaveLedgerEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeaveLedgerEntries_LeaveRequests_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalTable: "LeaveRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeaveLedgerEntries_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveLedgerEntries_Users_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_EmployeeId_LeaveTypeId_CycleYear",
                table: "LeaveBalances",
                columns: new[] { "EmployeeId", "LeaveTypeId", "CycleYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_LeaveTypeId",
                table: "LeaveBalances",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgerEntries_EmployeeId_LeaveTypeId_CycleYear",
                table: "LeaveLedgerEntries",
                columns: new[] { "EmployeeId", "LeaveTypeId", "CycleYear" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgerEntries_LeaveRequestId",
                table: "LeaveLedgerEntries",
                column: "LeaveRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgerEntries_LeaveTypeId",
                table: "LeaveLedgerEntries",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgerEntries_RecordedById",
                table: "LeaveLedgerEntries",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_CoveringEmployeeId",
                table: "LeaveRequests",
                column: "CoveringEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId_StartDate",
                table: "LeaveRequests",
                columns: new[] { "EmployeeId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_HrApprovedById",
                table: "LeaveRequests",
                column: "HrApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_LeaveTypeId",
                table: "LeaveRequests",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_ManagerApprovedById",
                table: "LeaveRequests",
                column: "ManagerApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_Status",
                table: "LeaveRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_SubmittedById",
                table: "LeaveRequests",
                column: "SubmittedById");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_Code",
                table: "LeaveTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_IsActive_DisplayOrder",
                table: "LeaveTypes",
                columns: new[] { "IsActive", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaveBalances");

            migrationBuilder.DropTable(
                name: "LeaveLedgerEntries");

            migrationBuilder.DropTable(
                name: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "LeaveTypes");
        }
    }
}
