using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OvertimeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HoursRequested = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    HoursApproved = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    DayType = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedById = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaidInPayrollRunId = table.Column<int>(type: "int", nullable: true),
                    RequestedById = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OvertimeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_Employees_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    BreakMinutes = table.Column<int>(type: "int", nullable: false),
                    WorkingDaysMask = table.Column<int>(type: "int", nullable: false),
                    LateGraceMinutes = table.Column<int>(type: "int", nullable: false),
                    OvertimeThresholdMinutes = table.Column<int>(type: "int", nullable: false),
                    SpansMidnight = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClockIn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClockOut = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BreakMinutes = table.Column<int>(type: "int", nullable: false),
                    HoursWorked = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    ScheduledHours = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    RestDayHours = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    PublicHolidayHours = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DayType = table.Column<int>(type: "int", nullable: false),
                    LateMinutes = table.Column<int>(type: "int", nullable: false),
                    EarlyLeaveMinutes = table.Column<int>(type: "int", nullable: false),
                    LeaveRequestId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsManualEntry = table.Column<bool>(type: "bit", nullable: false),
                    RecordedById = table.Column<int>(type: "int", nullable: true),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedById = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Employees_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        // No action, not cascade: attendance is already reachable from Employees
                        // through LeaveRequests, and SQL Server will not create a second cascade
                        // path into the same table.
                        name: "FK_AttendanceRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_LeaveRequests_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalTable: "LeaveRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Users_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_ApprovedById",
                table: "AttendanceRecords",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_Date_Status",
                table: "AttendanceRecords",
                columns: new[] { "Date", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EmployeeId_Date",
                table: "AttendanceRecords",
                columns: new[] { "EmployeeId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_IsApproved",
                table: "AttendanceRecords",
                column: "IsApproved");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_LeaveRequestId",
                table: "AttendanceRecords",
                column: "LeaveRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_RecordedById",
                table: "AttendanceRecords",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_ShiftId",
                table: "AttendanceRecords",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_ApprovedById",
                table: "OvertimeRequests",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_EmployeeId_Date",
                table: "OvertimeRequests",
                columns: new[] { "EmployeeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_RequestedById",
                table: "OvertimeRequests",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_Status",
                table: "OvertimeRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_EmployeeId_FromDate",
                table: "ShiftAssignments",
                columns: new[] { "EmployeeId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ShiftId",
                table: "ShiftAssignments",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_IsActive_Name",
                table: "Shifts",
                columns: new[] { "IsActive", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "OvertimeRequests");

            migrationBuilder.DropTable(
                name: "ShiftAssignments");

            migrationBuilder.DropTable(
                name: "Shifts");
        }
    }
}
