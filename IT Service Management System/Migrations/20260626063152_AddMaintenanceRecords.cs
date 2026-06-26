using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmployeeName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaintenanceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaintenanceType = table.Column<int>(type: "int", nullable: false),
                    ProblemDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkDone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PartsReplaced = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SoftwareInstalled = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NextMaintenanceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRecords", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceRecords");
        }
    }
}
