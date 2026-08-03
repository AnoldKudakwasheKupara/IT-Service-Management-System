using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddDisciplinaryModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DisciplinaryOffences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    Authority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Seriousness = table.Column<int>(type: "int", nullable: false),
                    DismissableFirstOffence = table.Column<bool>(type: "bit", nullable: false),
                    DefaultFirstPenalty = table.Column<int>(type: "int", nullable: false),
                    WarningValidityMonths = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisciplinaryOffences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DisciplinaryCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    OffenceId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Particulars = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IncidentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ChargeServedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChargeDocumentName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ChargeDocumentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSuspended = table.Column<bool>(type: "bit", nullable: false),
                    SuspensionOnFullPay = table.Column<bool>(type: "bit", nullable: false),
                    SuspensionFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspensionTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HearingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HearingVenue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChairpersonId = table.Column<int>(type: "int", nullable: true),
                    RepresentationOffered = table.Column<bool>(type: "bit", nullable: false),
                    RepresentedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmployeeAttended = table.Column<bool>(type: "bit", nullable: false),
                    AbsenceExplanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EmployeeResponse = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    HearingMinutes = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    Finding = table.Column<int>(type: "int", nullable: false),
                    FindingReasons = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Penalty = table.Column<int>(type: "int", nullable: false),
                    PenaltyReasons = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MitigationConsidered = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PenaltyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WarningExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppealRightExplained = table.Column<bool>(type: "bit", nullable: false),
                    AppealDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppealLodged = table.Column<bool>(type: "bit", nullable: false),
                    AppealLodgedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppealGrounds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AppealHeardById = table.Column<int>(type: "int", nullable: true),
                    AppealHeardDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppealOutcome = table.Column<int>(type: "int", nullable: true),
                    AppealDecision = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SubstitutedPenalty = table.Column<int>(type: "int", nullable: true),
                    RaisedById = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisciplinaryCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisciplinaryCases_DisciplinaryOffences_OffenceId",
                        column: x => x.OffenceId,
                        principalTable: "DisciplinaryOffences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DisciplinaryCases_Employees_AppealHeardById",
                        column: x => x.AppealHeardById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DisciplinaryCases_Employees_ChairpersonId",
                        column: x => x.ChairpersonId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DisciplinaryCases_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisciplinaryCases_Users_RaisedById",
                        column: x => x.RaisedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisciplinaryEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseId = table.Column<int>(type: "int", nullable: false),
                    Step = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedById = table.Column<int>(type: "int", nullable: true),
                    DocumentName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    DocumentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisciplinaryEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisciplinaryEvents_DisciplinaryCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "DisciplinaryCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DisciplinaryEvents_Users_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryCases_AppealHeardById",
                table: "DisciplinaryCases",
                column: "AppealHeardById");

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryCases_ChairpersonId",
                table: "DisciplinaryCases",
                column: "ChairpersonId");

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryCases_EmployeeId_Status",
                table: "DisciplinaryCases",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryCases_OffenceId",
                table: "DisciplinaryCases",
                column: "OffenceId");

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryCases_RaisedById",
                table: "DisciplinaryCases",
                column: "RaisedById");

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryCases_WarningExpiryDate",
                table: "DisciplinaryCases",
                column: "WarningExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryEvents_CaseId_At",
                table: "DisciplinaryEvents",
                columns: new[] { "CaseId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryEvents_RecordedById",
                table: "DisciplinaryEvents",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryOffences_Code",
                table: "DisciplinaryOffences",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryOffences_IsActive_DisplayOrder",
                table: "DisciplinaryOffences",
                columns: new[] { "IsActive", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisciplinaryEvents");

            migrationBuilder.DropTable(
                name: "DisciplinaryCases");

            migrationBuilder.DropTable(
                name: "DisciplinaryOffences");
        }
    }
}
