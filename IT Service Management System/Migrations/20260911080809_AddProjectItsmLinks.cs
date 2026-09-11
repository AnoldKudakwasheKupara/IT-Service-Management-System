using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectItsmLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectItsmLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    MilestoneId = table.Column<int>(type: "int", nullable: true),
                    Relation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TicketId = table.Column<int>(type: "int", nullable: true),
                    ChangeRequestId = table.Column<int>(type: "int", nullable: true),
                    ProblemId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectItsmLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectItsmLinks_ChangeRequests_ChangeRequestId",
                        column: x => x.ChangeRequestId,
                        principalTable: "ChangeRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectItsmLinks_Milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "Milestones",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectItsmLinks_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectItsmLinks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectItsmLinks_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectItsmLinks_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectItsmLinks_ChangeRequestId",
                table: "ProjectItsmLinks",
                column: "ChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectItsmLinks_CreatedById",
                table: "ProjectItsmLinks",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectItsmLinks_MilestoneId",
                table: "ProjectItsmLinks",
                column: "MilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectItsmLinks_ProblemId",
                table: "ProjectItsmLinks",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectItsmLinks_ProjectId_Relation",
                table: "ProjectItsmLinks",
                columns: new[] { "ProjectId", "Relation" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectItsmLinks_TicketId",
                table: "ProjectItsmLinks",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectItsmLinks");
        }
    }
}
