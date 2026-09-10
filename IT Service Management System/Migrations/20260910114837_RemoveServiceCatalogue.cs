using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class RemoveServiceCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Both tables were empty when the module was retired, so nothing is lost here.
            // Requests go first: they hold the foreign key to the catalogue items.
            migrationBuilder.DropTable(
                name: "ServiceRequests");

            migrationBuilder.DropTable(
                name: "ServiceCatalogItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceCatalogItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DefaultPriority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FulfillmentTargetMinutes = table.Column<int>(type: "int", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCatalogItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceCatalogItems_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApprovedById = table.Column<int>(type: "int", nullable: true),
                    AssignedToId = table.Column<int>(type: "int", nullable: true),
                    RequestedById = table.Column<int>(type: "int", nullable: false),
                    ServiceCatalogItemId = table.Column<int>(type: "int", nullable: false),
                    ApprovalNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BusinessJustification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FulfilledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FulfillmentNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HoldReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceRequests_ServiceCatalogItems_ServiceCatalogItemId",
                        column: x => x.ServiceCatalogItemId,
                        principalTable: "ServiceCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceRequests_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceRequests_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceRequests_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCatalogItems_IsPublished_Category",
                table: "ServiceCatalogItems",
                columns: new[] { "IsPublished", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCatalogItems_OwnerId",
                table: "ServiceCatalogItems",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ApprovedById",
                table: "ServiceRequests",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_AssignedToId",
                table: "ServiceRequests",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_RequestedById_CreatedAt",
                table: "ServiceRequests",
                columns: new[] { "RequestedById", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ServiceCatalogItemId",
                table: "ServiceRequests",
                column: "ServiceCatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_Status_DueAt",
                table: "ServiceRequests",
                columns: new[] { "Status", "DueAt" });
        }
    }
}
