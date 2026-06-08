using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddExitClearanceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DevelopmentClearances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExitClearanceId = table.Column<int>(type: "int", nullable: false),
                    BitbucketRemoved = table.Column<bool>(type: "bit", nullable: false),
                    ProjectManagementRemoved = table.Column<bool>(type: "bit", nullable: false),
                    ManageEngineRemoved = table.Column<bool>(type: "bit", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClearedById = table.Column<int>(type: "int", nullable: true),
                    ClearedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevelopmentClearances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExitClearances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    CurrentStage = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExitClearances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExitClearances_Users_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceClearances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExitClearanceId = table.Column<int>(type: "int", nullable: false),
                    StaffAdvanceCleared = table.Column<bool>(type: "bit", nullable: false),
                    ReceiptsHandedOver = table.Column<bool>(type: "bit", nullable: false),
                    FinalDuesProcessed = table.Column<bool>(type: "bit", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClearedById = table.Column<int>(type: "int", nullable: true),
                    ClearedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceClearances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HodApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExitClearanceId = table.Column<int>(type: "int", nullable: false),
                    HodId = table.Column<int>(type: "int", nullable: false),
                    Approved = table.Column<bool>(type: "bit", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HodApprovals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExitClearanceId = table.Column<int>(type: "int", nullable: false),
                    Approved = table.Column<bool>(type: "bit", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HrUserId = table.Column<int>(type: "int", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrApprovals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockHandoverItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExitClearanceId = table.Column<int>(type: "int", nullable: false),
                    ItemDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockHandoverItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupervisorApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExitClearanceId = table.Column<int>(type: "int", nullable: false),
                    SupervisorId = table.Column<int>(type: "int", nullable: false),
                    Approved = table.Column<bool>(type: "bit", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupervisorApprovals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemsAdminClearances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExitClearanceId = table.Column<int>(type: "int", nullable: false),
                    LaptopReturned = table.Column<bool>(type: "bit", nullable: false),
                    LaptopBagReturned = table.Column<bool>(type: "bit", nullable: false),
                    AccessoriesReturned = table.Column<bool>(type: "bit", nullable: false),
                    LockerReturned = table.Column<bool>(type: "bit", nullable: false),
                    EndpointSecurityRemoved = table.Column<bool>(type: "bit", nullable: false),
                    SophosCredentialsRemoved = table.Column<bool>(type: "bit", nullable: false),
                    AccessRemoved = table.Column<bool>(type: "bit", nullable: false),
                    EmailDisabled = table.Column<bool>(type: "bit", nullable: false),
                    EmailRedirected = table.Column<bool>(type: "bit", nullable: false),
                    SocialMediaRemoved = table.Column<bool>(type: "bit", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClearedById = table.Column<int>(type: "int", nullable: true),
                    ClearedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemsAdminClearances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClearanceWorkflows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExitClearanceId = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    AssignedToUserId = table.Column<int>(type: "int", nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClearanceWorkflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClearanceWorkflows_ExitClearances_ExitClearanceId",
                        column: x => x.ExitClearanceId,
                        principalTable: "ExitClearances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClearanceWorkflows_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExitClearanceEmployeeDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExitClearanceId = table.Column<int>(type: "int", nullable: false),
                    Surname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Supervisor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PositionHeld = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LengthOfService = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfNotice = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastDayOfWork = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ForwardingAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmployeeSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExitClearanceEmployeeDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExitClearanceEmployeeDetails_ExitClearances_ExitClearanceId",
                        column: x => x.ExitClearanceId,
                        principalTable: "ExitClearances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClearanceWorkflows_AssignedToUserId",
                table: "ClearanceWorkflows",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClearanceWorkflows_ExitClearanceId",
                table: "ClearanceWorkflows",
                column: "ExitClearanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExitClearanceEmployeeDetails_ExitClearanceId",
                table: "ExitClearanceEmployeeDetails",
                column: "ExitClearanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExitClearances_EmployeeId",
                table: "ExitClearances",
                column: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClearanceWorkflows");

            migrationBuilder.DropTable(
                name: "DevelopmentClearances");

            migrationBuilder.DropTable(
                name: "ExitClearanceEmployeeDetails");

            migrationBuilder.DropTable(
                name: "FinanceClearances");

            migrationBuilder.DropTable(
                name: "HodApprovals");

            migrationBuilder.DropTable(
                name: "HrApprovals");

            migrationBuilder.DropTable(
                name: "StockHandoverItems");

            migrationBuilder.DropTable(
                name: "SupervisorApprovals");

            migrationBuilder.DropTable(
                name: "SystemsAdminClearances");

            migrationBuilder.DropTable(
                name: "ExitClearances");
        }
    }
}
