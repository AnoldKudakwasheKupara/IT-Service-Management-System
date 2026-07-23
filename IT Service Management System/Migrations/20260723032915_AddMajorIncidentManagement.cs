using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddMajorIncidentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MajorIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BusinessImpact = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeclaredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeclaredById = table.Column<int>(type: "int", nullable: true),
                    SourceTicketId = table.Column<int>(type: "int", nullable: true),
                    CommanderId = table.Column<int>(type: "int", nullable: true),
                    TechnicalLeadId = table.Column<int>(type: "int", nullable: true),
                    CommunicationsLeadId = table.Column<int>(type: "int", nullable: true),
                    RecoveryStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Workaround = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RootCauseSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsersAffected = table.Column<int>(type: "int", nullable: true),
                    DowntimeMinutes = table.Column<int>(type: "int", nullable: true),
                    ReviewScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewHeldAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewFacilitatorId = table.Column<int>(type: "int", nullable: true),
                    PirWhatHappened = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    PirWhatWentWell = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PirWhatWentWrong = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PirLessonsLearned = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ReviewCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MajorIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MajorIncidents_Tickets_SourceTicketId",
                        column: x => x.SourceTicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MajorIncidents_Users_CommanderId",
                        column: x => x.CommanderId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MajorIncidents_Users_CommunicationsLeadId",
                        column: x => x.CommunicationsLeadId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MajorIncidents_Users_DeclaredById",
                        column: x => x.DeclaredById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MajorIncidents_Users_ReviewFacilitatorId",
                        column: x => x.ReviewFacilitatorId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MajorIncidents_Users_TechnicalLeadId",
                        column: x => x.TechnicalLeadId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MajorIncidentAffectedItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MajorIncidentId = table.Column<int>(type: "int", nullable: false),
                    ConfigurationItemId = table.Column<int>(type: "int", nullable: true),
                    ServiceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImpactNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Restored = table.Column<bool>(type: "bit", nullable: false),
                    RestoredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MajorIncidentAffectedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MajorIncidentAffectedItems_ConfigurationItems_ConfigurationItemId",
                        column: x => x.ConfigurationItemId,
                        principalTable: "ConfigurationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MajorIncidentAffectedItems_MajorIncidents_MajorIncidentId",
                        column: x => x.MajorIncidentId,
                        principalTable: "MajorIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MajorIncidentFollowUps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MajorIncidentId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MajorIncidentFollowUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MajorIncidentFollowUps_MajorIncidents_MajorIncidentId",
                        column: x => x.MajorIncidentId,
                        principalTable: "MajorIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MajorIncidentFollowUps_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MajorIncidentTimelineEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MajorIncidentId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LoggedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MajorIncidentTimelineEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MajorIncidentTimelineEntries_MajorIncidents_MajorIncidentId",
                        column: x => x.MajorIncidentId,
                        principalTable: "MajorIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MajorIncidentTimelineEntries_Users_LoggedById",
                        column: x => x.LoggedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MajorIncidentUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MajorIncidentId = table.Column<int>(type: "int", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    StatusAtUpdate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MajorIncidentUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MajorIncidentUpdates_MajorIncidents_MajorIncidentId",
                        column: x => x.MajorIncidentId,
                        principalTable: "MajorIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MajorIncidentUpdates_Users_PostedById",
                        column: x => x.PostedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidentAffectedItems_ConfigurationItemId",
                table: "MajorIncidentAffectedItems",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidentAffectedItems_MajorIncidentId",
                table: "MajorIncidentAffectedItems",
                column: "MajorIncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidentFollowUps_MajorIncidentId_Status",
                table: "MajorIncidentFollowUps",
                columns: new[] { "MajorIncidentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidentFollowUps_OwnerId",
                table: "MajorIncidentFollowUps",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidents_CommanderId",
                table: "MajorIncidents",
                column: "CommanderId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidents_CommunicationsLeadId",
                table: "MajorIncidents",
                column: "CommunicationsLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidents_DeclaredAt",
                table: "MajorIncidents",
                column: "DeclaredAt");

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidents_DeclaredById",
                table: "MajorIncidents",
                column: "DeclaredById");

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidents_ReviewFacilitatorId",
                table: "MajorIncidents",
                column: "ReviewFacilitatorId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidents_SourceTicketId",
                table: "MajorIncidents",
                column: "SourceTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidents_Status_Severity",
                table: "MajorIncidents",
                columns: new[] { "Status", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidents_TechnicalLeadId",
                table: "MajorIncidents",
                column: "TechnicalLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidentTimelineEntries_LoggedById",
                table: "MajorIncidentTimelineEntries",
                column: "LoggedById");

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidentTimelineEntries_MajorIncidentId_OccurredAt",
                table: "MajorIncidentTimelineEntries",
                columns: new[] { "MajorIncidentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidentUpdates_MajorIncidentId",
                table: "MajorIncidentUpdates",
                column: "MajorIncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorIncidentUpdates_PostedById",
                table: "MajorIncidentUpdates",
                column: "PostedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MajorIncidentAffectedItems");

            migrationBuilder.DropTable(
                name: "MajorIncidentFollowUps");

            migrationBuilder.DropTable(
                name: "MajorIncidentTimelineEntries");

            migrationBuilder.DropTable(
                name: "MajorIncidentUpdates");

            migrationBuilder.DropTable(
                name: "MajorIncidents");
        }
    }
}
