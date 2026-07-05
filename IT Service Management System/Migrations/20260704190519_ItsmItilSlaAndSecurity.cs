using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class ItsmItilSlaAndSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConfigurationItemId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProblemId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseDueAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConfigurationItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Criticality = table.Column<int>(type: "int", nullable: false),
                    Environment = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Vendor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IpOrHostname = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OwnerId = table.Column<int>(type: "int", nullable: true),
                    AssetId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfigurationItems_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConfigurationItems_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SlaPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResponseMinutes = table.Column<int>(type: "int", nullable: false),
                    ResolutionMinutes = table.Column<int>(type: "int", nullable: false),
                    BusinessHoursOnly = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Problems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    RootCause = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Workaround = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ConfigurationItemId = table.Column<int>(type: "int", nullable: true),
                    AssignedToId = table.Column<int>(type: "int", nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Problems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Problems_ConfigurationItems_ConfigurationItemId",
                        column: x => x.ConfigurationItemId,
                        principalTable: "ConfigurationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Problems_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Problems_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Risk = table.Column<int>(type: "int", nullable: false),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    ImplementationPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BackoutPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TestPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ScheduledStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfigurationItemId = table.Column<int>(type: "int", nullable: true),
                    ProblemId = table.Column<int>(type: "int", nullable: true),
                    AssignedToId = table.Column<int>(type: "int", nullable: true),
                    ApprovedById = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ImplementedSuccessfully = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeRequests_ConfigurationItems_ConfigurationItemId",
                        column: x => x.ConfigurationItemId,
                        principalTable: "ConfigurationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChangeRequests_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChangeRequests_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChangeRequests_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChangeRequests_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "SlaPolicies",
                columns: new[] { "Id", "BusinessHoursOnly", "Category", "CreatedAt", "IsActive", "Name", "Priority", "ResolutionMinutes", "ResponseMinutes" },
                values: new object[,]
                {
                    { 1, false, null, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "Critical Priority", 3, 240, 30 },
                    { 2, false, null, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "High Priority", 2, 480, 60 },
                    { 3, false, null, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "Medium Priority", 1, 1440, 240 },
                    { 4, false, null, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "Low Priority", 0, 4320, 480 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ConfigurationItemId",
                table: "Tickets",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ProblemId",
                table: "Tickets",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_ApprovedById",
                table: "ChangeRequests",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_AssignedToId",
                table: "ChangeRequests",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_ConfigurationItemId",
                table: "ChangeRequests",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_CreatedById",
                table: "ChangeRequests",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_ProblemId",
                table: "ChangeRequests",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_Status",
                table: "ChangeRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationItems_AssetId",
                table: "ConfigurationItems",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationItems_Name",
                table: "ConfigurationItems",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationItems_OwnerId",
                table: "ConfigurationItems",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationItems_Status",
                table: "ConfigurationItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_AssignedToId",
                table: "Problems",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_ConfigurationItemId",
                table: "Problems",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_CreatedById",
                table: "Problems",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_Status",
                table: "Problems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Priority_IsActive",
                table: "SlaPolicies",
                columns: new[] { "Priority", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_ConfigurationItems_ConfigurationItemId",
                table: "Tickets",
                column: "ConfigurationItemId",
                principalTable: "ConfigurationItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Problems_ProblemId",
                table: "Tickets",
                column: "ProblemId",
                principalTable: "Problems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_ConfigurationItems_ConfigurationItemId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Problems_ProblemId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "ChangeRequests");

            migrationBuilder.DropTable(
                name: "SlaPolicies");

            migrationBuilder.DropTable(
                name: "Problems");

            migrationBuilder.DropTable(
                name: "ConfigurationItems");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ConfigurationItemId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ProblemId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ConfigurationItemId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ProblemId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResponseDueAt",
                table: "Tickets");
        }
    }
}
