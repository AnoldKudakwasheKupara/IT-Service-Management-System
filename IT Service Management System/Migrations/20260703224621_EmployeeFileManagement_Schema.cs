using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeFileManagement_Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeDocumentId = table.Column<int>(type: "int", nullable: true),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    PerformedById = table.Column<int>(type: "int", nullable: true),
                    PerformedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentFolders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipientUserId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    EmployeeDocumentId = table.Column<int>(type: "int", nullable: true),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageProviders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    RootLocation = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DefaultFolderId = table.Column<int>(type: "int", nullable: true),
                    IsExpiryTracked = table.Column<bool>(type: "bit", nullable: false),
                    DefaultRetentionYears = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentCategories_DocumentFolders_DefaultFolderId",
                        column: x => x.DefaultFolderId,
                        principalTable: "DocumentFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequiredDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    AppliesToRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AppliesToDepartmentId = table.Column<int>(type: "int", nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequiredDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequiredDocuments_Departments_AppliesToDepartmentId",
                        column: x => x.AppliesToDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequiredDocuments_DocumentCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "DocumentCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RetentionPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    FolderId = table.Column<int>(type: "int", nullable: true),
                    RetentionYears = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetentionPolicies_DocumentCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "DocumentCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RetentionPolicies_DocumentFolders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "DocumentFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeDocumentId = table.Column<int>(type: "int", nullable: false),
                    DocumentVersionId = table.Column<int>(type: "int", nullable: true),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ApproverRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ApproverUserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedById = table.Column<int>(type: "int", nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentApprovals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeDocumentId = table.Column<int>(type: "int", nullable: false),
                    AuthorId = table.Column<int>(type: "int", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentComments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeDocumentId = table.Column<int>(type: "int", nullable: false),
                    DocumentVersionId = table.Column<int>(type: "int", nullable: true),
                    Token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    MaxDownloads = table.Column<int>(type: "int", nullable: true),
                    DownloadCount = table.Column<int>(type: "int", nullable: false),
                    IsReadOnly = table.Column<bool>(type: "bit", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentShares", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTagMaps",
                columns: table => new
                {
                    EmployeeDocumentId = table.Column<int>(type: "int", nullable: false),
                    DocumentTagId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTagMaps", x => new { x.EmployeeDocumentId, x.DocumentTagId });
                    table.ForeignKey(
                        name: "FK_DocumentTagMaps_DocumentTags_DocumentTagId",
                        column: x => x.DocumentTagId,
                        principalTable: "DocumentTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeDocumentId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    StorageProvider = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OcrText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangeNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedById = table.Column<int>(type: "int", nullable: true),
                    UploadedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    FolderId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Keywords = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConfidentialityLevel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentVersionId = table.Column<int>(type: "int", nullable: true),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetentionUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_DocumentCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "DocumentCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_DocumentFolders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "DocumentFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_DocumentVersions_CurrentVersionId",
                        column: x => x.CurrentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_Users_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpiryAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeDocumentId = table.Column<int>(type: "int", nullable: false),
                    ThresholdDays = table.Column<int>(type: "int", nullable: false),
                    AlertedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Acknowledged = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpiryAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpiryAlerts_EmployeeDocuments_EmployeeDocumentId",
                        column: x => x.EmployeeDocumentId,
                        principalTable: "EmployeeDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "DocumentFolders",
                columns: new[] { "Id", "Description", "Icon", "IsActive", "IsSystem", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, null, "fa-id-card", true, true, "Personal Documents", 1 },
                    { 2, null, "fa-briefcase", true, true, "Employment Documents", 2 },
                    { 3, null, "fa-graduation-cap", true, true, "Academic Qualifications", 3 },
                    { 4, null, "fa-certificate", true, true, "Professional Certifications", 4 },
                    { 5, null, "fa-notes-medical", true, true, "Medical Records", 5 },
                    { 6, null, "fa-money-check-dollar", true, true, "Payroll Documents", 6 },
                    { 7, null, "fa-file-invoice-dollar", true, true, "Tax Documents", 7 },
                    { 8, null, "fa-file-contract", true, true, "Contracts", 8 },
                    { 9, null, "fa-gavel", true, true, "Disciplinary Records", 9 },
                    { 10, null, "fa-chart-line", true, true, "Performance Reviews", 10 },
                    { 11, null, "fa-chalkboard-user", true, true, "Training Records", 11 },
                    { 12, null, "fa-plane-departure", true, true, "Leave Documents", 12 },
                    { 13, null, "fa-arrow-up-right-dots", true, true, "Promotion Documents", 13 },
                    { 14, null, "fa-right-left", true, true, "Transfer Documents", 14 },
                    { 15, null, "fa-award", true, true, "Awards", 15 },
                    { 16, null, "fa-triangle-exclamation", true, true, "Warnings", 16 },
                    { 17, null, "fa-door-open", true, true, "Exit Documents", 17 },
                    { 18, null, "fa-file-signature", true, true, "Resignation Documents", 18 },
                    { 19, null, "fa-umbrella-beach", true, true, "Retirement Documents", 19 },
                    { 20, null, "fa-folder", true, true, "Other Documents", 20 }
                });

            migrationBuilder.InsertData(
                table: "StorageProviders",
                columns: new[] { "Id", "CreatedAt", "IsActive", "IsDefault", "Name", "RootLocation", "Type" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, true, "Local Disk", "employee-documents", 0 });

            migrationBuilder.InsertData(
                table: "DocumentCategories",
                columns: new[] { "Id", "CreatedAt", "DefaultFolderId", "DefaultRetentionYears", "Description", "IsActive", "IsExpiryTracked", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, null, null, true, true, "Passport" },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, null, null, true, false, "National ID" },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, null, null, true, false, "Birth Certificate" },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, null, null, true, true, "Driver's License" },
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, null, null, true, true, "Police Clearance" },
                    { 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, null, null, true, false, "CV" },
                    { 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, null, null, true, false, "Offer Letter" },
                    { 8, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 8, null, null, true, false, "Employment Contract" },
                    { 9, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 8, null, null, true, false, "NDA" },
                    { 10, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, null, null, true, true, "Medical Aid Card" },
                    { 11, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 6, null, null, true, false, "NSSA" },
                    { 12, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 7, null, null, true, false, "Tax Certificate" },
                    { 13, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, null, null, true, false, "Degree Certificate" },
                    { 14, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, null, null, true, false, "Diploma" },
                    { 15, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, null, null, true, true, "Professional License" },
                    { 16, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 10, null, null, true, false, "Performance Review" },
                    { 17, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 11, null, null, true, false, "Training Certificate" },
                    { 18, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 16, null, null, true, false, "Warning Letter" },
                    { 19, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 13, null, null, true, false, "Promotion Letter" },
                    { 20, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 17, null, null, true, false, "Termination Letter" },
                    { 21, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 19, null, null, true, false, "Retirement Letter" },
                    { 22, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 20, null, null, true, false, "Other" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovals_EmployeeDocumentId",
                table: "DocumentApprovals",
                column: "EmployeeDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAuditLogs_EmployeeDocumentId",
                table: "DocumentAuditLogs",
                column: "EmployeeDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAuditLogs_EmployeeId",
                table: "DocumentAuditLogs",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAuditLogs_Timestamp",
                table: "DocumentAuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCategories_DefaultFolderId",
                table: "DocumentCategories",
                column: "DefaultFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentComments_EmployeeDocumentId",
                table: "DocumentComments",
                column: "EmployeeDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentNotifications_RecipientUserId_IsRead",
                table: "DocumentNotifications",
                columns: new[] { "RecipientUserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_EmployeeDocumentId",
                table: "DocumentShares",
                column: "EmployeeDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_Token",
                table: "DocumentShares",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTagMaps_DocumentTagId",
                table: "DocumentTagMaps",
                column: "DocumentTagId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTags_Name",
                table: "DocumentTags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_EmployeeDocumentId",
                table: "DocumentVersions",
                column: "EmployeeDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_CategoryId",
                table: "EmployeeDocuments",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_CurrentVersionId",
                table: "EmployeeDocuments",
                column: "CurrentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_FolderId",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "FolderId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_ExpiryDate",
                table: "EmployeeDocuments",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_FolderId",
                table: "EmployeeDocuments",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_Status",
                table: "EmployeeDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ExpiryAlerts_EmployeeDocumentId",
                table: "ExpiryAlerts",
                column: "EmployeeDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_RequiredDocuments_AppliesToDepartmentId",
                table: "RequiredDocuments",
                column: "AppliesToDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RequiredDocuments_CategoryId",
                table: "RequiredDocuments",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionPolicies_CategoryId",
                table: "RetentionPolicies",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionPolicies_FolderId",
                table: "RetentionPolicies",
                column: "FolderId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentApprovals_EmployeeDocuments_EmployeeDocumentId",
                table: "DocumentApprovals",
                column: "EmployeeDocumentId",
                principalTable: "EmployeeDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentComments_EmployeeDocuments_EmployeeDocumentId",
                table: "DocumentComments",
                column: "EmployeeDocumentId",
                principalTable: "EmployeeDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentShares_EmployeeDocuments_EmployeeDocumentId",
                table: "DocumentShares",
                column: "EmployeeDocumentId",
                principalTable: "EmployeeDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentTagMaps_EmployeeDocuments_EmployeeDocumentId",
                table: "DocumentTagMaps",
                column: "EmployeeDocumentId",
                principalTable: "EmployeeDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentVersions_EmployeeDocuments_EmployeeDocumentId",
                table: "DocumentVersions",
                column: "EmployeeDocumentId",
                principalTable: "EmployeeDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentVersions_EmployeeDocuments_EmployeeDocumentId",
                table: "DocumentVersions");

            migrationBuilder.DropTable(
                name: "DocumentApprovals");

            migrationBuilder.DropTable(
                name: "DocumentAuditLogs");

            migrationBuilder.DropTable(
                name: "DocumentComments");

            migrationBuilder.DropTable(
                name: "DocumentNotifications");

            migrationBuilder.DropTable(
                name: "DocumentShares");

            migrationBuilder.DropTable(
                name: "DocumentTagMaps");

            migrationBuilder.DropTable(
                name: "ExpiryAlerts");

            migrationBuilder.DropTable(
                name: "RequiredDocuments");

            migrationBuilder.DropTable(
                name: "RetentionPolicies");

            migrationBuilder.DropTable(
                name: "StorageProviders");

            migrationBuilder.DropTable(
                name: "DocumentTags");

            migrationBuilder.DropTable(
                name: "EmployeeDocuments");

            migrationBuilder.DropTable(
                name: "DocumentCategories");

            migrationBuilder.DropTable(
                name: "DocumentVersions");

            migrationBuilder.DropTable(
                name: "DocumentFolders");
        }
    }
}
