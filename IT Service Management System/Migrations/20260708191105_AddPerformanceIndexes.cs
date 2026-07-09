using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "TrainingRecords",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Tickets",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Tickets",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Suppliers",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Risks",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Risks",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Objectives",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "NonConformances",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ManagementReviews",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ManagementReviewActions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "IsoDocuments",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "IsoDocumentAcknowledgements",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Improvements",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ComplianceObligations",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Capas",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Audits",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "AuditLogs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AuditFindings",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRecords_CertificateExpiry",
                table: "TrainingRecords",
                column: "CertificateExpiry");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRecords_Status",
                table: "TrainingRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status_AssignedToId",
                table: "Tickets",
                columns: new[] { "Status", "AssignedToId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status_Priority",
                table: "Tickets",
                columns: new[] { "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Status",
                table: "Suppliers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_Category",
                table: "Risks",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_Status",
                table: "Risks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Objectives_Status",
                table: "Objectives",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NonConformances_Status",
                table: "NonConformances",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementReviews_Status_MeetingDate",
                table: "ManagementReviews",
                columns: new[] { "Status", "MeetingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ManagementReviewActions_Status_DueDate",
                table: "ManagementReviewActions",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocuments_ExpiryDate",
                table: "IsoDocuments",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocuments_ReviewDate",
                table: "IsoDocuments",
                column: "ReviewDate");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocuments_Status",
                table: "IsoDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentAcknowledgements_Status",
                table: "IsoDocumentAcknowledgements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Improvements_Status",
                table: "Improvements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceObligations_Status",
                table: "ComplianceObligations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Capas_Status_DueDate",
                table: "Capas",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Audits_Status_ActualEndDate",
                table: "Audits",
                columns: new[] { "Status", "ActualEndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_Status",
                table: "AuditFindings",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrainingRecords_CertificateExpiry",
                table: "TrainingRecords");

            migrationBuilder.DropIndex(
                name: "IX_TrainingRecords_Status",
                table: "TrainingRecords");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_Status_AssignedToId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_Status_Priority",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Status",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Risks_Category",
                table: "Risks");

            migrationBuilder.DropIndex(
                name: "IX_Risks_Status",
                table: "Risks");

            migrationBuilder.DropIndex(
                name: "IX_Objectives_Status",
                table: "Objectives");

            migrationBuilder.DropIndex(
                name: "IX_NonConformances_Status",
                table: "NonConformances");

            migrationBuilder.DropIndex(
                name: "IX_ManagementReviews_Status_MeetingDate",
                table: "ManagementReviews");

            migrationBuilder.DropIndex(
                name: "IX_ManagementReviewActions_Status_DueDate",
                table: "ManagementReviewActions");

            migrationBuilder.DropIndex(
                name: "IX_IsoDocuments_ExpiryDate",
                table: "IsoDocuments");

            migrationBuilder.DropIndex(
                name: "IX_IsoDocuments_ReviewDate",
                table: "IsoDocuments");

            migrationBuilder.DropIndex(
                name: "IX_IsoDocuments_Status",
                table: "IsoDocuments");

            migrationBuilder.DropIndex(
                name: "IX_IsoDocumentAcknowledgements_Status",
                table: "IsoDocumentAcknowledgements");

            migrationBuilder.DropIndex(
                name: "IX_Improvements_Status",
                table: "Improvements");

            migrationBuilder.DropIndex(
                name: "IX_ComplianceObligations_Status",
                table: "ComplianceObligations");

            migrationBuilder.DropIndex(
                name: "IX_Capas_Status_DueDate",
                table: "Capas");

            migrationBuilder.DropIndex(
                name: "IX_Audits_Status_ActualEndDate",
                table: "Audits");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditFindings_Status",
                table: "AuditFindings");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "TrainingRecords",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Suppliers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Risks",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Risks",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Objectives",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "NonConformances",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ManagementReviews",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ManagementReviewActions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "IsoDocuments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "IsoDocumentAcknowledgements",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Improvements",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ComplianceObligations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Capas",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Audits",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AuditFindings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
