using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentNo = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DateOfIncident = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimeOfIncident = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    LocationOfIncident = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    ReportedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DateReported = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BriefDescription = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: true),
                    ReportedToPoliceAt = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    PoliceDetailsTel = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CaseNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DetailedDescription = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: true),
                    EvidencePeople = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidencePaper = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidenceParts = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidencePositions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Probability = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReportStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocPollutionReport = table.Column<bool>(type: "bit", nullable: false),
                    DocSketchDiagram = table.Column<bool>(type: "bit", nullable: false),
                    DocWrittenStatements = table.Column<bool>(type: "bit", nullable: false),
                    DocMotorInsurance = table.Column<bool>(type: "bit", nullable: false),
                    DocDeptOfLabour = table.Column<bool>(type: "bit", nullable: false),
                    DocDriversDetails = table.Column<bool>(type: "bit", nullable: false),
                    DocInternalAudit = table.Column<bool>(type: "bit", nullable: false),
                    DocWorkmenCompensation = table.Column<bool>(type: "bit", nullable: false),
                    DocOther = table.Column<bool>(type: "bit", nullable: false),
                    DocOtherText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Preventable = table.Column<bool>(type: "bit", nullable: true),
                    PreventableNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Claimable = table.Column<bool>(type: "bit", nullable: true),
                    ClaimedFromInsurance = table.Column<bool>(type: "bit", nullable: true),
                    ClaimNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriticalFactors = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: true),
                    ImmediateCause = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BasicCause = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RootCause = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LessonsLearned = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: true),
                    DeptManagerComments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DeptManagerCommentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QaComments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    QaCommentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GmComments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    GmCommentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CapaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidents_Capas_CapaId",
                        column: x => x.CapaId,
                        principalTable: "Capas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Incidents_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidentActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ResponsiblePerson = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PlannedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentActions_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncidentDamages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Payer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Cost = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentDamages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentDamages_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncidentInvestigators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    InvestigationDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentInvestigators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentInvestigators_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentActions_IncidentId",
                table: "IncidentActions",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentDamages_IncidentId",
                table: "IncidentDamages",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentInvestigators_IncidentId",
                table: "IncidentInvestigators",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_CapaId",
                table: "Incidents",
                column: "CapaId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_CreatedById",
                table: "Incidents",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_DepartmentId",
                table: "Incidents",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Status",
                table: "Incidents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Year_IncidentNo",
                table: "Incidents",
                columns: new[] { "Year", "IncidentNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentActions");

            migrationBuilder.DropTable(
                name: "IncidentDamages");

            migrationBuilder.DropTable(
                name: "IncidentInvestigators");

            migrationBuilder.DropTable(
                name: "Incidents");
        }
    }
}
