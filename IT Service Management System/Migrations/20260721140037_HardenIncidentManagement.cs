using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class HardenIncidentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Incidents_Year_IncidentNo",
                table: "Incidents");

            migrationBuilder.AddColumn<int>(
                name: "DeptManagerSignedById",
                table: "Incidents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GmSignedById",
                table: "Incidents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QaSignedById",
                table: "Incidents",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_DeptManagerSignedById",
                table: "Incidents",
                column: "DeptManagerSignedById");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_GmSignedById",
                table: "Incidents",
                column: "GmSignedById");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_QaSignedById",
                table: "Incidents",
                column: "QaSignedById");

            // Older builds allocated MAX + 1 without a uniqueness guarantee. Preserve the first
            // occurrence of each reference and move only duplicate rows above that year's maximum
            // before creating the unique index.
            migrationBuilder.Sql("""
                ;WITH DuplicateRows AS
                (
                    SELECT Id, [Year], IncidentNo,
                           ROW_NUMBER() OVER (PARTITION BY [Year], IncidentNo ORDER BY Id) AS DuplicateOrdinal
                    FROM Incidents
                ),
                YearMaximums AS
                (
                    SELECT [Year], MAX(IncidentNo) AS MaximumIncidentNo
                    FROM Incidents
                    GROUP BY [Year]
                ),
                Replacements AS
                (
                    SELECT d.Id,
                           m.MaximumIncidentNo + ROW_NUMBER() OVER
                               (PARTITION BY d.[Year] ORDER BY d.IncidentNo, d.Id) AS NewIncidentNo
                    FROM DuplicateRows d
                    INNER JOIN YearMaximums m ON m.[Year] = d.[Year]
                    WHERE d.DuplicateOrdinal > 1
                )
                UPDATE i
                SET IncidentNo = r.NewIncidentNo
                FROM Incidents i
                INNER JOIN Replacements r ON r.Id = i.Id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Year_IncidentNo",
                table: "Incidents",
                columns: new[] { "Year", "IncidentNo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Users_DeptManagerSignedById",
                table: "Incidents",
                column: "DeptManagerSignedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Users_GmSignedById",
                table: "Incidents",
                column: "GmSignedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Users_QaSignedById",
                table: "Incidents",
                column: "QaSignedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Users_DeptManagerSignedById",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Users_GmSignedById",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Users_QaSignedById",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_DeptManagerSignedById",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_GmSignedById",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_QaSignedById",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_Year_IncidentNo",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "DeptManagerSignedById",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "GmSignedById",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "QaSignedById",
                table: "Incidents");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Year_IncidentNo",
                table: "Incidents",
                columns: new[] { "Year", "IncidentNo" });
        }
    }
}
