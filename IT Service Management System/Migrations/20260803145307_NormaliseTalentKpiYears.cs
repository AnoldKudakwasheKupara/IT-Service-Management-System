using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class NormaliseTalentKpiYears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Order matters. EF scaffolded the four DropColumn calls first, which would have thrown
            // away every KPI already recorded. The table is created, the existing values are copied
            // into it, and only then are the old columns dropped.
            migrationBuilder.CreateTable(
                name: "TalentKpiYears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TalentIdentificationId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Achievement = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Rating = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalentKpiYears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TalentKpiYears_TalentIdentifications_TalentIdentificationId",
                        column: x => x.TalentIdentificationId,
                        principalTable: "TalentIdentifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TalentKpiYears_TalentIdentificationId_Year",
                table: "TalentKpiYears",
                columns: new[] { "TalentIdentificationId", "Year" },
                unique: true);

            // Carry the four fixed columns across, one row per year, skipping blanks so an
            // untouched column does not become an empty record.
            foreach (var year in new[] { 2023, 2024, 2025, 2026 })
            {
                migrationBuilder.Sql($@"
                    INSERT INTO [TalentKpiYears] ([TalentIdentificationId], [Year], [Achievement])
                    SELECT [Id], {year}, [KPI{year}]
                    FROM [TalentIdentifications]
                    WHERE [KPI{year}] IS NOT NULL AND LTRIM(RTRIM([KPI{year}])) <> '';");
            }

            migrationBuilder.DropColumn(name: "KPI2023", table: "TalentIdentifications");
            migrationBuilder.DropColumn(name: "KPI2024", table: "TalentIdentifications");
            migrationBuilder.DropColumn(name: "KPI2025", table: "TalentIdentifications");
            migrationBuilder.DropColumn(name: "KPI2026", table: "TalentIdentifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate the four columns first, copy 2023–2026 back into them, then drop the table.
            // Any year outside that range cannot be represented by the old schema and is lost —
            // which is the point of having moved away from it.
            migrationBuilder.AddColumn<string>(
                name: "KPI2023",
                table: "TalentIdentifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KPI2024",
                table: "TalentIdentifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KPI2025",
                table: "TalentIdentifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KPI2026",
                table: "TalentIdentifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            foreach (var year in new[] { 2023, 2024, 2025, 2026 })
            {
                migrationBuilder.Sql($@"
                    UPDATE t SET t.[KPI{year}] = k.[Achievement]
                    FROM [TalentIdentifications] t
                    INNER JOIN [TalentKpiYears] k
                        ON k.[TalentIdentificationId] = t.[Id] AND k.[Year] = {year};");
            }

            migrationBuilder.DropTable(name: "TalentKpiYears");
        }
    }
}
