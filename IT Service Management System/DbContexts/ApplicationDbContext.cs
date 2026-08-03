using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Efm;
using IT_Service_Management_System.Models.Itsm;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.Models.Pm;
using IT_Service_Management_System.Models.Hr;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.DbContexts
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<TicketMessage> TicketMessages { get; set; }
        public DbSet<TicketAttachment> TicketAttachments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<AssetHistory> AssetHistories { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<SSLCertificate> SSLCertificates { get; set; }
        public DbSet<PaymentSchedule> PaymentSchedules { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<ActivityCategory> ActivityCategories { get; set; }
        public DbSet<ExitClearance> ExitClearances { get; set; }
        public DbSet<ClearanceWorkflow> ClearanceWorkflows { get; set; }
        public DbSet<ExitClearanceEmployeeDetails> ExitClearanceEmployeeDetails { get; set; }
        public DbSet<FinanceClearance> FinanceClearances { get; set; }
        public DbSet<SystemsAdminClearance> SystemsAdminClearances { get; set; }
        public DbSet<DevelopmentClearance> DevelopmentClearances { get; set; }
        public DbSet<StockHandoverItem> StockHandoverItems { get; set; }
        public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }
        public DbSet<SupervisorApproval> SupervisorApprovals { get; set; }
        public DbSet<HodApproval> HodApprovals { get; set; }
        public DbSet<HrApproval> HrApprovals { get; set; }
        public DbSet<ExitInterview> ExitInterviews { get; set; }
        public DbSet<EngagementStayInterview> EngagementStayInterviews { get; set; }
        public DbSet<TalentIdentification> TalentIdentifications { get; set; }
        public DbSet<TalentDirectReportAssessment> TalentDirectReportAssessments { get; set; }
        public DbSet<TalentDevelopmentAction> TalentDevelopmentActions { get; set; }
        public DbSet<UserAccessRight> UserAccessRights { get; set; }
        public DbSet<UserAccessRightItem> UserAccessRightItems { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<AppConfiguration> AppConfigurations { get; set; }
        public DbSet<CannedResponse> CannedResponses { get; set; }
        // Retired in favour of EFM. Mapped only so the existing table and its rows survive until
        // they have been migrated across — see EmployeeFile for the plan.
#pragma warning disable CS0618
        public DbSet<EmployeeFile> EmployeeFiles { get; set; }
#pragma warning restore CS0618

        // ── Employee File Management (EFM) ─────────────────────────────────────────
        public DbSet<DocumentFolder> DocumentFolders { get; set; }
        public DbSet<DocumentCategory> DocumentCategories { get; set; }
        public DbSet<RequiredDocument> RequiredDocuments { get; set; }
        public DbSet<EmployeeDocument> EmployeeDocuments { get; set; }
        public DbSet<DocumentVersion> DocumentVersions { get; set; }
        public DbSet<DocumentTag> DocumentTags { get; set; }
        public DbSet<DocumentTagMap> DocumentTagMaps { get; set; }
        public DbSet<DocumentAuditLog> DocumentAuditLogs { get; set; }
        public DbSet<DocumentShare> DocumentShares { get; set; }
        public DbSet<DocumentApproval> DocumentApprovals { get; set; }
        public DbSet<DocumentComment> DocumentComments { get; set; }
        public DbSet<DocumentNotification> DocumentNotifications { get; set; }
        public DbSet<StorageProvider> StorageProviders { get; set; }
        public DbSet<RetentionPolicy> RetentionPolicies { get; set; }
        public DbSet<ExpiryAlert> ExpiryAlerts { get; set; }
        public DbSet<DocumentRequest> DocumentRequests { get; set; }

        // ── ITSM / ITIL (CMDB, Problems, Changes) + SLA ────────────────────────────
        public DbSet<ConfigurationItem> ConfigurationItems { get; set; }
        public DbSet<Problem> Problems { get; set; }
        public DbSet<ChangeRequest> ChangeRequests { get; set; }
        public DbSet<SlaPolicy> SlaPolicies { get; set; }
        public DbSet<SlaCalendar> SlaCalendars { get; set; }
        public DbSet<SlaHoliday> SlaHolidays { get; set; }
        public DbSet<SlaEvent> SlaEvents { get; set; }
        public DbSet<ServiceCatalogItem> ServiceCatalogItems { get; set; }
        public DbSet<ServiceRequest> ServiceRequests { get; set; }
        public DbSet<MajorIncident> MajorIncidents { get; set; }
        public DbSet<MajorIncidentAffectedItem> MajorIncidentAffectedItems { get; set; }
        public DbSet<MajorIncidentTimelineEntry> MajorIncidentTimelineEntries { get; set; }
        public DbSet<MajorIncidentUpdate> MajorIncidentUpdates { get; set; }
        public DbSet<MajorIncidentFollowUp> MajorIncidentFollowUps { get; set; }

        // ── Meeting Minutes (Operations) ───────────────────────────────────────────
        public DbSet<Meeting> Meetings { get; set; }
        public DbSet<MeetingRosterMember> MeetingRosterMembers { get; set; }
        public DbSet<MeetingAttendance> MeetingAttendances { get; set; }
        public DbSet<ActionItem> ActionItems { get; set; }
        public DbSet<ActionItemUpdate> ActionItemUpdates { get; set; }

        // ── IMS / ISO (ISO 9001:2015 & ISO/IEC 27001:2022) ─────────────────────────
        // Document Control
        public DbSet<IsoDocumentCategory> IsoDocumentCategories { get; set; }
        public DbSet<IsoDocument> IsoDocuments { get; set; }
        public DbSet<IsoDocumentVersion> IsoDocumentVersions { get; set; }
        public DbSet<IsoDocumentApproval> IsoDocumentApprovals { get; set; }
        public DbSet<IsoDocumentAcknowledgement> IsoDocumentAcknowledgements { get; set; }
        public DbSet<IsoDocumentDistribution> IsoDocumentDistributions { get; set; }
        public DbSet<IsoDocumentReview> IsoDocumentReviews { get; set; }
        // Internal Audits & Findings
        public DbSet<AuditProgramme> AuditProgrammes { get; set; }
        public DbSet<Audit> Audits { get; set; }
        public DbSet<AuditTeamMember> AuditTeamMembers { get; set; }
        public DbSet<AuditChecklistItem> AuditChecklistItems { get; set; }
        public DbSet<AuditFinding> AuditFindings { get; set; }
        // CAPA & Non-Conformance
        public DbSet<Capa> Capas { get; set; }
        public DbSet<NonConformance> NonConformances { get; set; }
        // Incident Management (Improvement)
        public DbSet<Incident> Incidents { get; set; }
        public DbSet<IncidentInvestigator> IncidentInvestigators { get; set; }
        public DbSet<IncidentDamage> IncidentDamages { get; set; }
        public DbSet<IncidentAction> IncidentActions { get; set; }
        public DbSet<IncidentAttachment> IncidentAttachments { get; set; }
        // Risk & Opportunity
        public DbSet<Risk> Risks { get; set; }
        public DbSet<Opportunity> Opportunities { get; set; }
        // Suppliers
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierEvaluation> SupplierEvaluations { get; set; }
        // Training & Competency
        public DbSet<TrainingCourse> TrainingCourses { get; set; }
        public DbSet<TrainingRecord> TrainingRecords { get; set; }
        public DbSet<Competency> Competencies { get; set; }
        public DbSet<UserCompetency> UserCompetencies { get; set; }
        // Management Review
        public DbSet<ManagementReview> ManagementReviews { get; set; }
        public DbSet<ManagementReviewAttendee> ManagementReviewAttendees { get; set; }
        public DbSet<ManagementReviewInput> ManagementReviewInputs { get; set; }
        public DbSet<ManagementReviewAction> ManagementReviewActions { get; set; }
        // Objectives & KPIs
        public DbSet<Objective> Objectives { get; set; }
        public DbSet<ObjectiveMeasurement> ObjectiveMeasurements { get; set; }
        // Compliance, Improvement, Evidence, Reference
        public DbSet<ComplianceObligation> ComplianceObligations { get; set; }
        public DbSet<Improvement> Improvements { get; set; }
        public DbSet<IsoEvidence> IsoEvidences { get; set; }
        public DbSet<IsoClause> IsoClauses { get; set; }
        public DbSet<IsoNotification> IsoNotifications { get; set; }

        // ── Project Management (portfolio, planning, delivery, finance, governance) ─
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectTeamMember> ProjectTeamMembers { get; set; }
        public DbSet<ProjectLink> ProjectLinks { get; set; }
        public DbSet<ProjectAttachment> ProjectAttachments { get; set; }
        public DbSet<ProjectActivityLog> ProjectActivityLogs { get; set; }
        public DbSet<PmNotification> PmNotifications { get; set; }
        // Planning
        public DbSet<ProjectPhase> ProjectPhases { get; set; }
        public DbSet<WbsItem> WbsItems { get; set; }
        public DbSet<Milestone> Milestones { get; set; }
        public DbSet<Deliverable> Deliverables { get; set; }
        // Tasks
        public DbSet<ProjectTask> ProjectTasks { get; set; }
        public DbSet<TaskDependency> TaskDependencies { get; set; }
        public DbSet<TaskChecklistItem> TaskChecklistItems { get; set; }
        public DbSet<TaskComment> TaskComments { get; set; }
        public DbSet<TaskAttachment> TaskAttachments { get; set; }
        // Resources & time
        public DbSet<Resource> Resources { get; set; }
        public DbSet<ResourceAssignment> ResourceAssignments { get; set; }
        public DbSet<ResourceUnavailability> ResourceUnavailabilities { get; set; }
        public DbSet<TimeEntry> TimeEntries { get; set; }
        // Finance & procurement
        public DbSet<BudgetLine> BudgetLines { get; set; }
        public DbSet<ProjectExpense> ProjectExpenses { get; set; }
        public DbSet<ProcurementRequest> ProcurementRequests { get; set; }
        public DbSet<ProcurementQuote> ProcurementQuotes { get; set; }
        public DbSet<ProjectAsset> ProjectAssets { get; set; }
        // Governance
        public DbSet<ProjectRisk> ProjectRisks { get; set; }
        public DbSet<ProjectIssue> ProjectIssues { get; set; }
        public DbSet<ProjectChangeRequest> ProjectChangeRequests { get; set; }
        public DbSet<QualityCheck> QualityChecks { get; set; }
        public DbSet<ProjectApproval> ProjectApprovals { get; set; }
        public DbSet<ProjectKpi> ProjectKpis { get; set; }
        // Collaboration, templates & closure
        public DbSet<ProjectDocument> ProjectDocuments { get; set; }
        public DbSet<ProjectDocumentVersion> ProjectDocumentVersions { get; set; }
        public DbSet<ProjectMeeting> ProjectMeetings { get; set; }
        public DbSet<ProjectMeetingAttendee> ProjectMeetingAttendees { get; set; }
        public DbSet<ProjectMeetingAction> ProjectMeetingActions { get; set; }
        public DbSet<ProjectDiscussion> ProjectDiscussions { get; set; }
        public DbSet<ProjectTemplate> ProjectTemplates { get; set; }
        public DbSet<ProjectTemplateItem> ProjectTemplateItems { get; set; }
        public DbSet<ProjectClosure> ProjectClosures { get; set; }
        public DbSet<LessonLearned> LessonsLearned { get; set; }

        // ── HR (employee master record) ────────────────────────────────────────────
        public DbSet<Employee> Employees { get; set; }
        public DbSet<TalentKpiYear> TalentKpiYears { get; set; }

        // ── HR statutory reference data (Zimbabwe) ─────────────────────────────────
        public DbSet<StatutoryParameter> StatutoryParameters { get; set; }
        public DbSet<PayeTaxBand> PayeTaxBands { get; set; }
        public DbSet<PublicHoliday> PublicHolidays { get; set; }

        // ── HR: leave ──────────────────────────────────────────────────────────────
        public DbSet<LeaveType> LeaveTypes { get; set; }
        public DbSet<LeaveBalance> LeaveBalances { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<LeaveLedgerEntry> LeaveLedgerEntries { get; set; }

        // ── HR: payroll ────────────────────────────────────────────────────────────
        public DbSet<SalaryStructure> SalaryStructures { get; set; }
        public DbSet<PayComponent> PayComponents { get; set; }
        public DbSet<PayrollRun> PayrollRuns { get; set; }
        public DbSet<Payslip> Payslips { get; set; }
        public DbSet<PayslipLine> PayslipLines { get; set; }

        // ── HR: attendance ─────────────────────────────────────────────────────────
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<ShiftAssignment> ShiftAssignments { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<OvertimeRequest> OvertimeRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Ticket>()
                .Property(t => t.Priority)
                .HasConversion<string>();

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.CreatedBy)
                .WithMany(u => u.TicketsCreated)
                .HasForeignKey(t => t.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.AssignedTo)
                .WithMany()
                .HasForeignKey(t => t.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ MESSAGE RELATIONSHIPS
            modelBuilder.Entity<TicketMessage>()
                .HasOne(m => m.Ticket)
                .WithMany(t => t.Messages)
                .HasForeignKey(m => m.TicketId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TicketMessage>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TicketAttachment>()
                .HasOne(a => a.Ticket)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TicketAttachment>()
                .HasOne(a => a.TicketMessage)
                .WithMany(m => m.Attachments)
                .HasForeignKey(a => a.TicketMessageId)
                .OnDelete(DeleteBehavior.Restrict);

            // User -> Department
            modelBuilder.Entity<User>()
                .HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Department -> HOD
            modelBuilder.Entity<Department>()
                .HasOne(d => d.Hod)
                .WithMany()
                .HasForeignKey(d => d.HodId)
                .OnDelete(DeleteBehavior.Restrict);

            // User -> Supervisor
            modelBuilder.Entity<User>()
                .HasOne(u => u.Supervisor)
                .WithMany(u => u.Subordinates)
                .HasForeignKey(u => u.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClearanceWorkflow>()
                .HasOne(c => c.AssignedToUser)
                .WithMany()
                .HasForeignKey(c => c.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExitClearance>()
                .HasOne(c => c.Employee)
                .WithMany()
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Department>()
                .HasOne(d => d.Hod)
                .WithMany()
                .HasForeignKey(d => d.HodId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Supervisor)
                .WithMany(u => u.Subordinates)
                .HasForeignKey(u => u.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserAccessRightItem>()
                .HasOne(u => u.UserAccessRight)
                .WithMany(h => h.Users)
                .HasForeignKey(u => u.UserAccessRightsId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TicketAttachment>()
                .ToTable(t => t.HasCheckConstraint("CK_Attachment_Owner",
                    "[TicketId] IS NOT NULL OR [TicketMessageId] IS NOT NULL"));

            // Decimal precision — avoids silent truncation (and EF model-validation warnings).
            modelBuilder.Entity<Asset>().Property(a => a.PurchaseCost).HasPrecision(18, 2);
            modelBuilder.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<PaymentSchedule>().Property(p => p.Amount).HasPrecision(18, 2);

            // Soft-delete: retain deleted tickets but hide them from normal queries.
            modelBuilder.Entity<Ticket>().HasQueryFilter(t => !t.IsDeleted);

            // UserSession -> User
            modelBuilder.Entity<UserSession>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSession>()
                .HasIndex(s => s.SessionToken);

            modelBuilder.Entity<UserSession>()
                .HasIndex(s => new { s.UserId, s.RevokedAt });

            // EmployeeFile -> User (employee). Cascade so a user's files are removed with them.
            // Retired store, kept mapped so its rows are not dropped before they are migrated.
#pragma warning disable CS0618
            modelBuilder.Entity<EmployeeFile>()
                .HasOne(f => f.Employee)
                .WithMany()
                .HasForeignKey(f => f.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmployeeFile>()
                .HasIndex(f => f.EmployeeId);
#pragma warning restore CS0618

            // ── Employee File Management (EFM) relationships & indexes ──────────────
            modelBuilder.Entity<EmployeeDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<EmployeeDocument>()
                .HasOne(d => d.Employee).WithMany().HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<EmployeeDocument>()
                .HasOne(d => d.Folder).WithMany(f => f.Documents).HasForeignKey(d => d.FolderId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<EmployeeDocument>()
                .HasOne(d => d.Category).WithMany(c => c.Documents).HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<EmployeeDocument>()
                .HasOne(d => d.CurrentVersion).WithMany().HasForeignKey(d => d.CurrentVersionId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EmployeeDocument>().HasIndex(d => new { d.EmployeeId, d.FolderId });
            modelBuilder.Entity<EmployeeDocument>().HasIndex(d => d.CategoryId);
            modelBuilder.Entity<EmployeeDocument>().HasIndex(d => d.Status);
            modelBuilder.Entity<EmployeeDocument>().HasIndex(d => d.ExpiryDate);

            modelBuilder.Entity<DocumentVersion>()
                .HasOne(v => v.Document).WithMany(d => d.Versions).HasForeignKey(v => v.EmployeeDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DocumentVersion>().HasIndex(v => v.EmployeeDocumentId);

            modelBuilder.Entity<DocumentTagMap>().HasKey(m => new { m.EmployeeDocumentId, m.DocumentTagId });
            modelBuilder.Entity<DocumentTagMap>()
                .HasOne(m => m.Document).WithMany(d => d.Tags).HasForeignKey(m => m.EmployeeDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DocumentTagMap>()
                .HasOne(m => m.Tag).WithMany(t => t.Documents).HasForeignKey(m => m.DocumentTagId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DocumentTag>().HasIndex(t => t.Name).IsUnique();

            modelBuilder.Entity<DocumentCategory>()
                .HasOne(c => c.DefaultFolder).WithMany().HasForeignKey(c => c.DefaultFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RequiredDocument>()
                .HasOne(r => r.Category).WithMany().HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<RequiredDocument>()
                .HasOne(r => r.AppliesToDepartment).WithMany().HasForeignKey(r => r.AppliesToDepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocumentApproval>()
                .HasOne(a => a.Document).WithMany(d => d.Approvals).HasForeignKey(a => a.EmployeeDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DocumentComment>()
                .HasOne(c => c.Document).WithMany(d => d.Comments).HasForeignKey(c => c.EmployeeDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DocumentShare>()
                .HasOne(s => s.Document).WithMany().HasForeignKey(s => s.EmployeeDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DocumentShare>().HasIndex(s => s.Token).IsUnique();
            modelBuilder.Entity<ExpiryAlert>()
                .HasOne(e => e.Document).WithMany().HasForeignKey(e => e.EmployeeDocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RetentionPolicy>()
                .HasOne(r => r.Category).WithMany().HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<RetentionPolicy>()
                .HasOne(r => r.Folder).WithMany().HasForeignKey(r => r.FolderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocumentRequest>()
                .HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DocumentRequest>()
                .HasOne(r => r.Category).WithMany().HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DocumentRequest>()
                .HasOne(r => r.FulfilledDocument).WithMany().HasForeignKey(r => r.FulfilledDocumentId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<DocumentRequest>().HasIndex(r => new { r.EmployeeId, r.Status });
            modelBuilder.Entity<DocumentRequest>().HasIndex(r => r.Status);

            modelBuilder.Entity<DocumentAuditLog>().HasIndex(a => a.EmployeeDocumentId);
            modelBuilder.Entity<DocumentAuditLog>().HasIndex(a => a.EmployeeId);
            modelBuilder.Entity<DocumentAuditLog>().HasIndex(a => a.Timestamp);
            modelBuilder.Entity<DocumentNotification>().HasIndex(n => new { n.RecipientUserId, n.IsRead });

            // ── ITSM / ITIL relationships (all nullable links use SetNull/NoAction to avoid
            //     multiple-cascade-path errors on SQL Server) ─────────────────────────────
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Problem).WithMany(p => p.Incidents).HasForeignKey(t => t.ProblemId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.ConfigurationItem).WithMany(c => c.Incidents).HasForeignKey(t => t.ConfigurationItemId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ConfigurationItem>()
                .HasOne(c => c.Owner).WithMany().HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ConfigurationItem>()
                .HasOne(c => c.Asset).WithMany().HasForeignKey(c => c.AssetId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<ConfigurationItem>().HasIndex(c => c.Name);
            modelBuilder.Entity<ConfigurationItem>().HasIndex(c => c.Status);

            modelBuilder.Entity<Problem>()
                .HasOne(p => p.ConfigurationItem).WithMany(c => c.Problems).HasForeignKey(p => p.ConfigurationItemId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Problem>()
                .HasOne(p => p.AssignedTo).WithMany().HasForeignKey(p => p.AssignedToId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Problem>()
                .HasOne(p => p.CreatedBy).WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Problem>().HasIndex(p => p.Status);

            modelBuilder.Entity<ChangeRequest>()
                .HasOne(c => c.ConfigurationItem).WithMany(ci => ci.Changes).HasForeignKey(c => c.ConfigurationItemId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<ChangeRequest>()
                .HasOne(c => c.Problem).WithMany(p => p.Changes).HasForeignKey(c => c.ProblemId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<ChangeRequest>()
                .HasOne(c => c.AssignedTo).WithMany().HasForeignKey(c => c.AssignedToId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ChangeRequest>()
                .HasOne(c => c.ApprovedBy).WithMany().HasForeignKey(c => c.ApprovedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ChangeRequest>()
                .HasOne(c => c.CreatedBy).WithMany().HasForeignKey(c => c.CreatedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ChangeRequest>().HasIndex(c => c.Status);

            modelBuilder.Entity<SlaPolicy>().HasIndex(s => new { s.Priority, s.IsActive });
            modelBuilder.Entity<SlaPolicy>().Property(s => s.WarningThresholdPercent).HasDefaultValue(75);
            modelBuilder.Entity<SlaPolicy>()
                .HasOne(s => s.Calendar).WithMany(c => c.Policies).HasForeignKey(s => s.SlaCalendarId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<SlaCalendar>().HasIndex(c => c.Name).IsUnique();
            modelBuilder.Entity<SlaHoliday>()
                .HasOne(h => h.SlaCalendar).WithMany(c => c.Holidays).HasForeignKey(h => h.SlaCalendarId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<SlaHoliday>().HasIndex(h => new { h.SlaCalendarId, h.Date }).IsUnique();
            modelBuilder.Entity<SlaEvent>().Property(e => e.Type).HasConversion<string>();
            modelBuilder.Entity<SlaEvent>()
                .HasOne(e => e.Ticket).WithMany(t => t.SlaEvents).HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<SlaEvent>().HasIndex(e => new { e.TicketId, e.Type }).IsUnique();
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.SlaPolicy).WithMany().HasForeignKey(t => t.SlaPolicyId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ServiceCatalogItem>().Property(i => i.DefaultPriority).HasConversion<string>();
            modelBuilder.Entity<ServiceCatalogItem>()
                .HasOne(i => i.Owner).WithMany().HasForeignKey(i => i.OwnerId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<ServiceCatalogItem>().HasIndex(i => new { i.IsPublished, i.Category });

            modelBuilder.Entity<ServiceRequest>().Property(r => r.Priority).HasConversion<string>();
            modelBuilder.Entity<ServiceRequest>().Property(r => r.Status).HasConversion<string>();
            modelBuilder.Entity<ServiceRequest>()
                .HasOne(r => r.ServiceCatalogItem).WithMany(i => i.Requests)
                .HasForeignKey(r => r.ServiceCatalogItemId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ServiceRequest>()
                .HasOne(r => r.RequestedBy).WithMany().HasForeignKey(r => r.RequestedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ServiceRequest>()
                .HasOne(r => r.AssignedTo).WithMany().HasForeignKey(r => r.AssignedToId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ServiceRequest>()
                .HasOne(r => r.ApprovedBy).WithMany().HasForeignKey(r => r.ApprovedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ServiceRequest>().HasIndex(r => new { r.Status, r.DueAt });
            modelBuilder.Entity<ServiceRequest>().HasIndex(r => new { r.RequestedById, r.CreatedAt });

            // ── Major incident management ───────────────────────────────────────────────
            // Enums stored as readable strings; children cascade; user/ticket links never cascade.
            modelBuilder.Entity<MajorIncident>().Property(m => m.Severity).HasConversion<string>();
            modelBuilder.Entity<MajorIncident>().Property(m => m.Status).HasConversion<string>();
            modelBuilder.Entity<MajorIncident>()
                .HasOne(m => m.SourceTicket).WithMany().HasForeignKey(m => m.SourceTicketId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<MajorIncident>()
                .HasOne(m => m.DeclaredBy).WithMany().HasForeignKey(m => m.DeclaredById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncident>()
                .HasOne(m => m.Commander).WithMany().HasForeignKey(m => m.CommanderId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncident>()
                .HasOne(m => m.TechnicalLead).WithMany().HasForeignKey(m => m.TechnicalLeadId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncident>()
                .HasOne(m => m.CommunicationsLead).WithMany().HasForeignKey(m => m.CommunicationsLeadId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncident>()
                .HasOne(m => m.ReviewFacilitator).WithMany().HasForeignKey(m => m.ReviewFacilitatorId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncident>().HasIndex(m => new { m.Status, m.Severity });
            modelBuilder.Entity<MajorIncident>().HasIndex(m => m.DeclaredAt);

            modelBuilder.Entity<MajorIncidentAffectedItem>()
                .HasOne(a => a.MajorIncident).WithMany(m => m.AffectedItems).HasForeignKey(a => a.MajorIncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MajorIncidentAffectedItem>()
                .HasOne(a => a.ConfigurationItem).WithMany().HasForeignKey(a => a.ConfigurationItemId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MajorIncidentTimelineEntry>().Property(e => e.Type).HasConversion<string>();
            modelBuilder.Entity<MajorIncidentTimelineEntry>()
                .HasOne(e => e.MajorIncident).WithMany(m => m.Timeline).HasForeignKey(e => e.MajorIncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MajorIncidentTimelineEntry>()
                .HasOne(e => e.LoggedBy).WithMany().HasForeignKey(e => e.LoggedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncidentTimelineEntry>().HasIndex(e => new { e.MajorIncidentId, e.OccurredAt });

            modelBuilder.Entity<MajorIncidentUpdate>().Property(u => u.Channel).HasConversion<string>();
            modelBuilder.Entity<MajorIncidentUpdate>().Property(u => u.StatusAtUpdate).HasConversion<string>();
            modelBuilder.Entity<MajorIncidentUpdate>()
                .HasOne(u => u.MajorIncident).WithMany(m => m.Updates).HasForeignKey(u => u.MajorIncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MajorIncidentUpdate>()
                .HasOne(u => u.PostedBy).WithMany().HasForeignKey(u => u.PostedById).OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<MajorIncidentFollowUp>().Property(f => f.Status).HasConversion<string>();
            modelBuilder.Entity<MajorIncidentFollowUp>()
                .HasOne(f => f.MajorIncident).WithMany(m => m.FollowUps).HasForeignKey(f => f.MajorIncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MajorIncidentFollowUp>()
                .HasOne(f => f.Owner).WithMany().HasForeignKey(f => f.OwnerId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncidentFollowUp>().HasIndex(f => new { f.MajorIncidentId, f.Status });

            // ── Meeting Minutes relationships ──────────────────────────────────────────
            // Store enums as readable strings (matches the Ticket/User convention above).
            modelBuilder.Entity<Meeting>().Property(m => m.Status).HasConversion<string>();
            modelBuilder.Entity<MeetingAttendance>().Property(a => a.Status).HasConversion<string>();
            modelBuilder.Entity<ActionItem>().Property(a => a.Status).HasConversion<string>();
            modelBuilder.Entity<ActionItem>().Property(a => a.Priority).HasConversion<string>();
            modelBuilder.Entity<ActionItemUpdate>().Property(u => u.StatusAtUpdate).HasConversion<string>();

            // All User/Department FKs use Restrict (no cascade) so the many links from these
            // tables never create multiple-cascade-path errors on SQL Server.
            modelBuilder.Entity<Meeting>()
                .HasOne(m => m.Facilitator).WithMany().HasForeignKey(m => m.FacilitatorId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Meeting>()
                .HasOne(m => m.MinuteTaker).WithMany().HasForeignKey(m => m.MinuteTakerId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Meeting>()
                .HasOne(m => m.Department).WithMany().HasForeignKey(m => m.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Meeting>().HasIndex(m => m.DepartmentId);

            modelBuilder.Entity<MeetingRosterMember>()
                .HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<MeetingRosterMember>()
                .HasOne(r => r.Department).WithMany().HasForeignKey(r => r.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
            // A user appears once per department roster (and once on the org-wide roster).
            modelBuilder.Entity<MeetingRosterMember>().HasIndex(r => new { r.DepartmentId, r.UserId }).IsUnique();

            // Attendance: cascade from its meeting; Restrict on the user link.
            modelBuilder.Entity<MeetingAttendance>()
                .HasOne(a => a.Meeting).WithMany(m => m.Attendances).HasForeignKey(a => a.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MeetingAttendance>()
                .HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<MeetingAttendance>().HasIndex(a => new { a.MeetingId, a.UserId }).IsUnique();

            // Action items belong to the meeting they were raised in (Restrict: don't lose
            // tracked tasks by deleting a meeting) and to an assignee (Restrict).
            modelBuilder.Entity<ActionItem>()
                .HasOne(a => a.Meeting).WithMany(m => m.ActionItems).HasForeignKey(a => a.MeetingId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ActionItem>()
                .HasOne(a => a.AssignedTo).WithMany().HasForeignKey(a => a.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ActionItem>().HasIndex(a => a.Status);

            // Updates cascade from their action item; the optional meeting link is NoAction
            // (avoids a second cascade path into ActionItemUpdate).
            modelBuilder.Entity<ActionItemUpdate>()
                .HasOne(u => u.ActionItem).WithMany(a => a.Updates).HasForeignKey(u => u.ActionItemId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ActionItemUpdate>()
                .HasOne(u => u.Meeting).WithMany().HasForeignKey(u => u.MeetingId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ActionItemUpdate>()
                .HasOne(u => u.UpdatedBy).WithMany().HasForeignKey(u => u.UpdatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Default SLA policies (mirror the previous hard-coded targets; HR/Admin can edit these).
            var slaSeed = new DateTime(2026, 7, 1);
            modelBuilder.Entity<SlaPolicy>().HasData(
                new SlaPolicy { Id = 1, Name = "Critical Priority", Priority = Ticket.TicketPriority.Critical, ResponseMinutes = 30, ResolutionMinutes = 240, IsActive = true, CreatedAt = slaSeed },
                new SlaPolicy { Id = 2, Name = "High Priority", Priority = Ticket.TicketPriority.High, ResponseMinutes = 60, ResolutionMinutes = 480, IsActive = true, CreatedAt = slaSeed },
                new SlaPolicy { Id = 3, Name = "Medium Priority", Priority = Ticket.TicketPriority.Medium, ResponseMinutes = 240, ResolutionMinutes = 1440, IsActive = true, CreatedAt = slaSeed },
                new SlaPolicy { Id = 4, Name = "Low Priority", Priority = Ticket.TicketPriority.Low, ResponseMinutes = 480, ResolutionMinutes = 4320, IsActive = true, CreatedAt = slaSeed }
            );

            // ── Seed: system folders, starter categories, default storage provider ──
            var efmSeed = new DateTime(2026, 1, 1);
            modelBuilder.Entity<DocumentFolder>().HasData(
                new DocumentFolder { Id = 1, Name = "Personal Documents", Icon = "fa-id-card", SortOrder = 1, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 2, Name = "Employment Documents", Icon = "fa-briefcase", SortOrder = 2, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 3, Name = "Academic Qualifications", Icon = "fa-graduation-cap", SortOrder = 3, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 4, Name = "Professional Certifications", Icon = "fa-certificate", SortOrder = 4, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 5, Name = "Medical Records", Icon = "fa-notes-medical", SortOrder = 5, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 6, Name = "Payroll Documents", Icon = "fa-money-check-dollar", SortOrder = 6, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 7, Name = "Tax Documents", Icon = "fa-file-invoice-dollar", SortOrder = 7, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 8, Name = "Contracts", Icon = "fa-file-contract", SortOrder = 8, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 9, Name = "Disciplinary Records", Icon = "fa-gavel", SortOrder = 9, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 10, Name = "Performance Reviews", Icon = "fa-chart-line", SortOrder = 10, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 11, Name = "Training Records", Icon = "fa-chalkboard-user", SortOrder = 11, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 12, Name = "Leave Documents", Icon = "fa-plane-departure", SortOrder = 12, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 13, Name = "Promotion Documents", Icon = "fa-arrow-up-right-dots", SortOrder = 13, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 14, Name = "Transfer Documents", Icon = "fa-right-left", SortOrder = 14, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 15, Name = "Awards", Icon = "fa-award", SortOrder = 15, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 16, Name = "Warnings", Icon = "fa-triangle-exclamation", SortOrder = 16, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 17, Name = "Exit Documents", Icon = "fa-door-open", SortOrder = 17, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 18, Name = "Resignation Documents", Icon = "fa-file-signature", SortOrder = 18, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 19, Name = "Retirement Documents", Icon = "fa-umbrella-beach", SortOrder = 19, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 20, Name = "Other Documents", Icon = "fa-folder", SortOrder = 20, IsSystem = true, IsActive = true }
            );

            modelBuilder.Entity<DocumentCategory>().HasData(
                new DocumentCategory { Id = 1, Name = "Passport", DefaultFolderId = 1, IsExpiryTracked = true, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 2, Name = "National ID", DefaultFolderId = 1, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 3, Name = "Birth Certificate", DefaultFolderId = 1, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 4, Name = "Driver's License", DefaultFolderId = 1, IsExpiryTracked = true, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 5, Name = "Police Clearance", DefaultFolderId = 1, IsExpiryTracked = true, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 6, Name = "CV", DefaultFolderId = 2, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 7, Name = "Offer Letter", DefaultFolderId = 2, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 8, Name = "Employment Contract", DefaultFolderId = 8, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 9, Name = "NDA", DefaultFolderId = 8, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 10, Name = "Medical Aid Card", DefaultFolderId = 5, IsExpiryTracked = true, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 11, Name = "NSSA", DefaultFolderId = 6, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 12, Name = "Tax Certificate", DefaultFolderId = 7, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 13, Name = "Degree Certificate", DefaultFolderId = 3, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 14, Name = "Diploma", DefaultFolderId = 3, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 15, Name = "Professional License", DefaultFolderId = 4, IsExpiryTracked = true, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 16, Name = "Performance Review", DefaultFolderId = 10, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 17, Name = "Training Certificate", DefaultFolderId = 11, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 18, Name = "Warning Letter", DefaultFolderId = 16, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 19, Name = "Promotion Letter", DefaultFolderId = 13, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 20, Name = "Termination Letter", DefaultFolderId = 17, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 21, Name = "Retirement Letter", DefaultFolderId = 19, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 22, Name = "Other", DefaultFolderId = 20, IsActive = true, CreatedAt = efmSeed }
            );

            modelBuilder.Entity<StorageProvider>().HasData(
                new StorageProvider { Id = 1, Name = "Local Disk", Type = StorageProviderType.LocalDisk, RootLocation = "employee-documents", IsDefault = true, IsActive = true, CreatedAt = efmSeed }
            );

            // ── IMS / ISO (ISO 9001:2015 & ISO/IEC 27001:2022) ──────────────────────
            // Persist every IMS enum as a readable string, keep decimals at (18,2), and set every IMS
            // foreign key to Restrict so SQL Server never sees multiple cascade paths. IMS parents are
            // soft-deleted (or never hard-deleted), so cascade delete is intentionally not used here.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                         .Where(e => e.ClrType.Namespace == "IT_Service_Management_System.Models.Ims")
                         .ToList())
            {
                var entity = modelBuilder.Entity(entityType.ClrType);

                foreach (var prop in entityType.GetProperties().ToList())
                {
                    var clr = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
                    if (clr.IsEnum)
                        entity.Property(prop.Name).HasConversion<string>();
                    else if (clr == typeof(decimal))
                        entity.Property(prop.Name).HasPrecision(18, 2);
                }

                foreach (var fk in entityType.GetForeignKeys())
                    fk.DeleteBehavior = DeleteBehavior.Restrict;
            }

            // A document has many versions (history) and one "current" version pointer — configured
            // explicitly so the two relationships between the same pair of types are unambiguous.
            modelBuilder.Entity<IsoDocumentVersion>()
                .HasOne(v => v.Document)
                .WithMany(d => d.Versions)
                .HasForeignKey(v => v.IsoDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<IsoDocument>()
                .HasOne(d => d.CurrentVersionRef)
                .WithMany()
                .HasForeignKey(d => d.CurrentVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IsoDocument>().HasIndex(d => d.DocumentNumber).IsUnique();
            modelBuilder.Entity<IsoDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<IsoClause>().HasIndex(c => new { c.Standard, c.ClauseNumber });

            // ── Performance indexes ─────────────────────────────────────────────────
            // Status/date columns are filtered/counted constantly by the dashboards,
            // registers and the ECIE compliance-health engine but were unindexed, so
            // every load scanned the table. (Enum columns are persisted as strings;
            // an index on the nvarchar column serves equality filters well.)
            modelBuilder.Entity<Ticket>().HasIndex(t => new { t.Status, t.AssignedToId });
            modelBuilder.Entity<Ticket>().HasIndex(t => new { t.Status, t.Priority });
            modelBuilder.Entity<AuditLog>().HasIndex(a => a.Timestamp);
            modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.UserId, a.Timestamp });

            modelBuilder.Entity<IsoDocument>().HasIndex(d => d.Status);
            modelBuilder.Entity<IsoDocument>().HasIndex(d => d.ReviewDate);
            modelBuilder.Entity<IsoDocument>().HasIndex(d => d.ExpiryDate);
            modelBuilder.Entity<IsoDocumentAcknowledgement>().HasIndex(a => a.Status);
            modelBuilder.Entity<Risk>().HasIndex(r => r.Status);
            modelBuilder.Entity<Risk>().HasIndex(r => r.Category);
            modelBuilder.Entity<Capa>().HasIndex(c => new { c.Status, c.DueDate });
            modelBuilder.Entity<NonConformance>().HasIndex(n => n.Status);

            // Incidents: children cascade with the parent; department link must not cascade.
            modelBuilder.Entity<Incident>().HasIndex(i => new { i.Year, i.IncidentNo }).IsUnique();
            modelBuilder.Entity<Incident>().HasIndex(i => i.Status);
            modelBuilder.Entity<Incident>()
                .HasOne(i => i.Department).WithMany().HasForeignKey(i => i.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Incident>()
                .HasOne(i => i.CreatedBy).WithMany().HasForeignKey(i => i.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Incident>()
                .HasOne(i => i.Capa).WithMany().HasForeignKey(i => i.CapaId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Incident>()
                .HasOne(i => i.DeptManagerSignedBy).WithMany().HasForeignKey(i => i.DeptManagerSignedById)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Incident>()
                .HasOne(i => i.QaSignedBy).WithMany().HasForeignKey(i => i.QaSignedById)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Incident>()
                .HasOne(i => i.GmSignedBy).WithMany().HasForeignKey(i => i.GmSignedById)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<IncidentInvestigator>()
                .HasOne(x => x.Incident).WithMany(i => i.Investigators).HasForeignKey(x => x.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<IncidentDamage>()
                .HasOne(x => x.Incident).WithMany(i => i.Damages).HasForeignKey(x => x.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<IncidentAction>()
                .HasOne(x => x.Incident).WithMany(i => i.Actions).HasForeignKey(x => x.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<IncidentAttachment>()
                .HasOne(x => x.Incident).WithMany(i => i.Attachments).HasForeignKey(x => x.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<IncidentAttachment>()
                .HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<IncidentAttachment>().HasIndex(x => x.IncidentId);
            modelBuilder.Entity<AuditFinding>().HasIndex(f => f.Status);
            modelBuilder.Entity<Audit>().HasIndex(a => new { a.Status, a.ActualEndDate });
            modelBuilder.Entity<TrainingRecord>().HasIndex(t => t.Status);
            modelBuilder.Entity<TrainingRecord>().HasIndex(t => t.CertificateExpiry);
            modelBuilder.Entity<Supplier>().HasIndex(s => s.Status);
            modelBuilder.Entity<ManagementReview>().HasIndex(m => new { m.Status, m.MeetingDate });
            modelBuilder.Entity<ManagementReviewAction>().HasIndex(a => new { a.Status, a.DueDate });
            modelBuilder.Entity<Objective>().HasIndex(o => o.Status);
            modelBuilder.Entity<ComplianceObligation>().HasIndex(o => o.Status);
            modelBuilder.Entity<Improvement>().HasIndex(i => i.Status);

            // ── Project Management ────────────────────────────────────────────────
            // Every reference to a User is Restrict: a project touches the same person from many
            // angles (manager, sponsor, assignee, approver) and SQL Server rejects the resulting
            // multiple cascade paths. Records owned by a project cascade from the project itself.
            ConfigurePmModule(modelBuilder);

            // ── HR employee register ──────────────────────────────────────────────
            ConfigureHrModule(modelBuilder);

            // Seed document categories
            modelBuilder.Entity<IsoDocumentCategory>().HasData(
                new IsoDocumentCategory { Id = 1, Name = "Quality Management", Code = "QMS", IsActive = true },
                new IsoDocumentCategory { Id = 2, Name = "Information Security", Code = "ISMS", IsActive = true },
                new IsoDocumentCategory { Id = 3, Name = "Human Resources", Code = "HR", IsActive = true },
                new IsoDocumentCategory { Id = 4, Name = "Information Technology", Code = "IT", IsActive = true },
                new IsoDocumentCategory { Id = 5, Name = "Operations", Code = "OPS", IsActive = true },
                new IsoDocumentCategory { Id = 6, Name = "Finance", Code = "FIN", IsActive = true },
                new IsoDocumentCategory { Id = 7, Name = "Health, Safety & Environment", Code = "HSE", IsActive = true },
                new IsoDocumentCategory { Id = 8, Name = "General / Administration", Code = "GEN", IsActive = true }
            );

            // Seed key ISO clauses (reference data for tagging, evidence and the AI compliance assistant)
            modelBuilder.Entity<IsoClause>().HasData(
                // ISO 9001:2015
                new IsoClause { Id = 1, Standard = IsoStandard.Iso9001, ClauseNumber = "4", Title = "Context of the organization" },
                new IsoClause { Id = 2, Standard = IsoStandard.Iso9001, ClauseNumber = "5", Title = "Leadership" },
                new IsoClause { Id = 3, Standard = IsoStandard.Iso9001, ClauseNumber = "6", Title = "Planning" },
                new IsoClause { Id = 4, Standard = IsoStandard.Iso9001, ClauseNumber = "6.2", Title = "Quality objectives and planning to achieve them" },
                new IsoClause { Id = 5, Standard = IsoStandard.Iso9001, ClauseNumber = "7", Title = "Support" },
                new IsoClause { Id = 6, Standard = IsoStandard.Iso9001, ClauseNumber = "7.2", Title = "Competence" },
                new IsoClause { Id = 7, Standard = IsoStandard.Iso9001, ClauseNumber = "7.5", Title = "Documented information" },
                new IsoClause { Id = 8, Standard = IsoStandard.Iso9001, ClauseNumber = "8", Title = "Operation" },
                new IsoClause { Id = 9, Standard = IsoStandard.Iso9001, ClauseNumber = "8.4", Title = "Control of externally provided processes, products and services" },
                new IsoClause { Id = 10, Standard = IsoStandard.Iso9001, ClauseNumber = "8.5", Title = "Production and service provision" },
                new IsoClause { Id = 11, Standard = IsoStandard.Iso9001, ClauseNumber = "9", Title = "Performance evaluation" },
                new IsoClause { Id = 12, Standard = IsoStandard.Iso9001, ClauseNumber = "9.2", Title = "Internal audit" },
                new IsoClause { Id = 13, Standard = IsoStandard.Iso9001, ClauseNumber = "9.3", Title = "Management review" },
                new IsoClause { Id = 14, Standard = IsoStandard.Iso9001, ClauseNumber = "10", Title = "Improvement" },
                new IsoClause { Id = 15, Standard = IsoStandard.Iso9001, ClauseNumber = "10.2", Title = "Nonconformity and corrective action" },
                // ISO/IEC 27001:2022
                new IsoClause { Id = 16, Standard = IsoStandard.Iso27001, ClauseNumber = "4", Title = "Context of the organization" },
                new IsoClause { Id = 17, Standard = IsoStandard.Iso27001, ClauseNumber = "5", Title = "Leadership" },
                new IsoClause { Id = 18, Standard = IsoStandard.Iso27001, ClauseNumber = "6", Title = "Planning" },
                new IsoClause { Id = 19, Standard = IsoStandard.Iso27001, ClauseNumber = "6.1.2", Title = "Information security risk assessment" },
                new IsoClause { Id = 20, Standard = IsoStandard.Iso27001, ClauseNumber = "6.1.3", Title = "Information security risk treatment" },
                new IsoClause { Id = 21, Standard = IsoStandard.Iso27001, ClauseNumber = "7", Title = "Support" },
                new IsoClause { Id = 22, Standard = IsoStandard.Iso27001, ClauseNumber = "7.5", Title = "Documented information" },
                new IsoClause { Id = 23, Standard = IsoStandard.Iso27001, ClauseNumber = "8", Title = "Operation" },
                new IsoClause { Id = 24, Standard = IsoStandard.Iso27001, ClauseNumber = "9", Title = "Performance evaluation" },
                new IsoClause { Id = 25, Standard = IsoStandard.Iso27001, ClauseNumber = "9.2", Title = "Internal audit" },
                new IsoClause { Id = 26, Standard = IsoStandard.Iso27001, ClauseNumber = "9.3", Title = "Management review" },
                new IsoClause { Id = 27, Standard = IsoStandard.Iso27001, ClauseNumber = "10", Title = "Improvement" },
                new IsoClause { Id = 28, Standard = IsoStandard.Iso27001, ClauseNumber = "10.2", Title = "Nonconformity and corrective action" }
            );
        }

        /// <summary>
        /// The employee register and the links from each HR process module back to it. Employee
        /// records are soft-deleted and never destroyed, so a global query filter hides retired
        /// rows while keeping them available to an administrator who ignores the filter.
        /// </summary>
        private static void ConfigureHrModule(ModelBuilder b)
        {
            b.Entity<Employee>(e =>
            {
                e.HasIndex(x => x.EmployeeNumber).IsUnique();
                e.HasIndex(x => new { x.Status, x.DepartmentId });
                e.HasIndex(x => x.HireDate);
                e.HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
                e.HasQueryFilter(x => !x.IsDeleted);

                // One account maps to at most one employee. Deleting the account must never
                // cascade away the employment record.
                e.HasOne(x => x.User).WithOne()
                    .HasForeignKey<Employee>(x => x.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Department).WithMany()
                    .HasForeignKey(x => x.DepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Self-reference for the reporting line.
                e.HasOne(x => x.Manager).WithMany(x => x.DirectReports)
                    .HasForeignKey(x => x.ManagerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Each process module points at the employee. The dependents carry a matching query
            // filter so EF does not warn about the required-end mismatch, and so an assessment of
            // a deleted employee disappears with them.
            // All three carry an IsDeleted flag. A global filter enforces it centrally, so a query
            // that forgets the predicate cannot leak a deleted record — which is exactly what was
            // happening before.
            // ── Statutory reference data ──────────────────────────────────────────
            b.Entity<StatutoryParameter>(e =>
            {
                // Lookups are always "this key, effective at this date", so that is the index.
                e.HasIndex(x => new { x.Key, x.EffectiveFrom });
                e.HasOne(x => x.UpdatedBy).WithMany().HasForeignKey(x => x.UpdatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<PayeTaxBand>()
                .HasIndex(x => new { x.Currency, x.Period, x.EffectiveFrom, x.FromAmount });

            b.Entity<PublicHoliday>().HasIndex(x => x.Date);

            // ── Leave ─────────────────────────────────────────────────────────────
            // Employee references are Restrict throughout: an employee is touched from several
            // angles here (the applicant, the cover, both approvers) and SQL Server rejects the
            // multiple cascade paths that would otherwise result.
            b.Entity<LeaveType>(e =>
            {
                e.HasIndex(x => x.Code).IsUnique();
                e.HasIndex(x => new { x.IsActive, x.DisplayOrder });
            });

            b.Entity<LeaveBalance>(e =>
            {
                // One balance per employee, per type, per cycle — the unique index is what stops a
                // race creating two and halving somebody's entitlement.
                e.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.CycleYear }).IsUnique();
                e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.LeaveType).WithMany().HasForeignKey(x => x.LeaveTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<LeaveRequest>(e =>
            {
                e.HasIndex(x => new { x.EmployeeId, x.StartDate });
                e.HasIndex(x => x.Status);
                e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.LeaveType).WithMany(t => t.Requests).HasForeignKey(x => x.LeaveTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CoveringEmployee).WithMany().HasForeignKey(x => x.CoveringEmployeeId)
                    .OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.ManagerApprovedBy).WithMany().HasForeignKey(x => x.ManagerApprovedById)
                    .OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.HrApprovedBy).WithMany().HasForeignKey(x => x.HrApprovedById)
                    .OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.SubmittedBy).WithMany().HasForeignKey(x => x.SubmittedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Payroll ───────────────────────────────────────────────────────────
            b.Entity<SalaryStructure>(e =>
            {
                // Salary is effective-dated, so the lookup is always employee plus date.
                e.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom });
                e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<PayComponent>(e =>
            {
                e.HasIndex(x => new { x.EmployeeId, x.IsActive, x.EffectiveFrom });
                e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<PayrollRun>(e =>
            {
                // One run per period per currency — the unique index is what stops a month being
                // paid twice.
                e.HasIndex(x => new { x.PeriodYear, x.PeriodMonth, x.Currency }).IsUnique();
                e.HasOne(x => x.PreparedBy).WithMany().HasForeignKey(x => x.PreparedById)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<Payslip>(e =>
            {
                e.HasIndex(x => new { x.PayrollRunId, x.EmployeeId }).IsUnique();
                e.HasOne(x => x.PayrollRun).WithMany(r => r.Payslips).HasForeignKey(x => x.PayrollRunId)
                    .OnDelete(DeleteBehavior.Cascade);
                // Restrict, not cascade: deleting an employee must never erase what they were paid.
                e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Attendance ────────────────────────────────────────────────────────
            b.Entity<Shift>().HasIndex(x => new { x.IsActive, x.Name });

            b.Entity<ShiftAssignment>(e =>
            {
                e.HasIndex(x => new { x.EmployeeId, x.FromDate });
                e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Shift).WithMany(s => s.Assignments).HasForeignKey(x => x.ShiftId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<AttendanceRecord>(e =>
            {
                // One record per employee per day — the unique index is what stops a double
                // clock-in creating two rows and paying the overtime twice.
                e.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
                e.HasIndex(x => new { x.Date, x.Status });
                e.HasIndex(x => x.IsApproved);

                e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId)
                    .OnDelete(DeleteBehavior.SetNull);
                // The attendance row outlives the leave request that explained it.
                e.HasOne(x => x.LeaveRequest).WithMany().HasForeignKey(x => x.LeaveRequestId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById)
                    .OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<OvertimeRequest>(e =>
            {
                e.HasIndex(x => new { x.EmployeeId, x.Date });
                e.HasIndex(x => x.Status);
                e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById)
                    .OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<PayslipLine>(e =>
            {
                e.HasIndex(x => new { x.PayslipId, x.DisplayOrder });
                e.HasOne(x => x.Payslip).WithMany(p => p.Lines).HasForeignKey(x => x.PayslipId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<LeaveLedgerEntry>(e =>
            {
                e.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.CycleYear });
                e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.LeaveType).WithMany().HasForeignKey(x => x.LeaveTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
                // The ledger outlives the request it describes — deleting a request must not erase
                // the movement it caused.
                e.HasOne(x => x.LeaveRequest).WithMany().HasForeignKey(x => x.LeaveRequestId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<TalentKpiYear>(e =>
            {
                // One row per assessment per year; the unique index is what stops a duplicate year.
                e.HasIndex(x => new { x.TalentIdentificationId, x.Year }).IsUnique();
                e.HasQueryFilter(x => !x.TalentIdentification!.IsDeleted);
                e.HasOne(x => x.TalentIdentification).WithMany(t => t.KpiYears)
                    .HasForeignKey(x => x.TalentIdentificationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<TalentIdentification>(e =>
            {
                e.HasIndex(x => x.EmployeeId);
                e.HasQueryFilter(x => !x.IsDeleted);
                e.HasOne(x => x.Employee).WithMany(x => x.TalentAssessments)
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            b.Entity<ExitInterview>(e =>
            {
                e.HasIndex(x => x.EmployeeId);
                e.HasQueryFilter(x => !x.IsDeleted);
                e.HasOne(x => x.Employee).WithMany(x => x.ExitInterviews)
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            b.Entity<EngagementStayInterview>(e =>
            {
                e.HasIndex(x => x.EmployeeId);
                e.HasQueryFilter(x => !x.IsDeleted);
                e.HasOne(x => x.Employee).WithMany(x => x.StayInterviews)
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // The talent assessment's children must share the parent's filter, or EF warns that a
            // required relationship can be broken by filtering.
            b.Entity<TalentDirectReportAssessment>()
                .HasQueryFilter(x => !x.TalentIdentification!.IsDeleted);
            b.Entity<TalentDevelopmentAction>()
                .HasQueryFilter(x => !x.TalentIdentification!.IsDeleted);
        }

        /// <summary>
        /// Relationships, indexes and seed data for the Project Management module. Split out of
        /// <see cref="OnModelCreating"/> to keep that method readable.
        /// </summary>
        private static void ConfigurePmModule(ModelBuilder b)
        {
            // Seed rows must be byte-for-byte identical on every build. A property defaulting to
            // DateTime.Now would make the model non-deterministic and fail migration validation at
            // startup, so seeded timestamps are pinned to a fixed date.
            var PmSeedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

            // Cascade rules. A project owns its children, so those cascade from Project. Every
            // *secondary* link between two of those children (a task to its phase, an expense to its
            // budget line) is NoAction: both ends already cascade from the project, and SQL Server
            // rejects two cascade paths converging on one table. In practice this costs nothing —
            // projects are soft-deleted, so a hard cascade never runs.

            // ── Project ───────────────────────────────────────────────────────────
            b.Entity<Project>(e =>
            {
                e.HasIndex(p => p.Code).IsUnique();
                e.HasIndex(p => new { p.Status, p.EndDate });
                e.HasIndex(p => p.DepartmentId);
                e.HasQueryFilter(p => !p.IsDeleted);

                e.HasOne(p => p.Department).WithMany().HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(p => p.Sponsor).WithMany().HasForeignKey(p => p.SponsorId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(p => p.ProjectManager).WithMany().HasForeignKey(p => p.ProjectManagerId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(p => p.CreatedBy).WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(p => p.ApprovedBy).WithMany().HasForeignKey(p => p.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ProjectTeamMember>(e =>
            {
                e.HasIndex(m => new { m.ProjectId, m.UserId });
                e.HasOne(m => m.Project).WithMany(p => p.TeamMembers).HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            // Two FKs onto Project — the second must not cascade, or SQL Server sees two paths.
            b.Entity<ProjectLink>(e =>
            {
                e.HasOne(l => l.Project).WithMany(p => p.Dependencies).HasForeignKey(l => l.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(l => l.DependsOnProject).WithMany().HasForeignKey(l => l.DependsOnProjectId).OnDelete(DeleteBehavior.NoAction);
            });

            b.Entity<ProjectAttachment>(e =>
            {
                e.HasOne(a => a.Project).WithMany(p => p.Attachments).HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.UploadedBy).WithMany().HasForeignKey(a => a.UploadedById).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ProjectActivityLog>(e =>
            {
                e.HasIndex(l => new { l.ProjectId, l.At });
                e.HasIndex(l => new { l.EntityType, l.EntityId });
                e.HasOne(l => l.Project).WithMany().HasForeignKey(l => l.ProjectId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<PmNotification>(e =>
            {
                e.HasIndex(n => new { n.UserId, n.IsRead });
                e.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(n => n.Project).WithMany().HasForeignKey(n => n.ProjectId).OnDelete(DeleteBehavior.SetNull);
            });

            // ── Planning ──────────────────────────────────────────────────────────
            b.Entity<ProjectPhase>(e =>
            {
                e.HasIndex(p => new { p.ProjectId, p.Sequence });
                e.HasOne(p => p.Project).WithMany(x => x.Phases).HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<WbsItem>(e =>
            {
                e.HasIndex(w => new { w.ProjectId, w.WbsCode });
                e.HasOne(w => w.Project).WithMany(p => p.WbsItems).HasForeignKey(w => w.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(w => w.Parent).WithMany(x => x.Children).HasForeignKey(w => w.ParentId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(w => w.Phase).WithMany(p => p.WbsItems).HasForeignKey(w => w.PhaseId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(w => w.Owner).WithMany().HasForeignKey(w => w.OwnerId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<Milestone>(e =>
            {
                e.HasIndex(m => new { m.ProjectId, m.DueDate });
                e.HasIndex(m => m.Status);
                e.HasOne(m => m.Project).WithMany(p => p.Milestones).HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(m => m.Phase).WithMany().HasForeignKey(m => m.PhaseId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(m => m.Owner).WithMany().HasForeignKey(m => m.OwnerId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<Deliverable>(e =>
            {
                e.HasIndex(d => new { d.ProjectId, d.Status });
                e.HasOne(d => d.Project).WithMany(p => p.Deliverables).HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(d => d.Milestone).WithMany().HasForeignKey(d => d.MilestoneId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(d => d.Phase).WithMany().HasForeignKey(d => d.PhaseId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(d => d.Owner).WithMany().HasForeignKey(d => d.OwnerId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(d => d.AcceptedBy).WithMany().HasForeignKey(d => d.AcceptedById).OnDelete(DeleteBehavior.Restrict);
            });

            // ── Tasks ─────────────────────────────────────────────────────────────
            b.Entity<ProjectTask>(e =>
            {
                e.HasIndex(t => new { t.ProjectId, t.Status });
                e.HasIndex(t => new { t.ProjectId, t.Column, t.BoardOrder });
                e.HasIndex(t => new { t.AssignedToId, t.DueDate });
                e.HasQueryFilter(t => !t.IsDeleted);

                e.HasOne(t => t.Project).WithMany(p => p.Tasks).HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(t => t.ParentTask).WithMany(t => t.Subtasks).HasForeignKey(t => t.ParentTaskId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(t => t.WbsItem).WithMany(w => w.Tasks).HasForeignKey(t => t.WbsItemId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(t => t.Phase).WithMany(p => p.Tasks).HasForeignKey(t => t.PhaseId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(t => t.Milestone).WithMany().HasForeignKey(t => t.MilestoneId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(t => t.AssignedTo).WithMany().HasForeignKey(t => t.AssignedToId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(t => t.Reviewer).WithMany().HasForeignKey(t => t.ReviewerId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<TaskDependency>(e =>
            {
                e.HasIndex(d => new { d.TaskId, d.PredecessorTaskId }).IsUnique();
                e.HasOne(d => d.Task).WithMany(t => t.Dependencies).HasForeignKey(d => d.TaskId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(d => d.PredecessorTask).WithMany().HasForeignKey(d => d.PredecessorTaskId).OnDelete(DeleteBehavior.NoAction);
            });

            b.Entity<TaskChecklistItem>(e =>
            {
                e.HasOne(c => c.Task).WithMany(t => t.Checklist).HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.CompletedBy).WithMany().HasForeignKey(c => c.CompletedById).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<TaskComment>(e =>
            {
                e.HasIndex(c => c.TaskId);
                e.HasOne(c => c.Task).WithMany(t => t.Comments).HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<TaskAttachment>(e =>
            {
                e.HasOne(a => a.Task).WithMany(t => t.Attachments).HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.UploadedBy).WithMany().HasForeignKey(a => a.UploadedById).OnDelete(DeleteBehavior.Restrict);
            });

            // ── Resources & time ──────────────────────────────────────────────────
            b.Entity<Resource>(e =>
            {
                e.HasIndex(r => new { r.Type, r.IsActive });
                e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(r => r.Department).WithMany().HasForeignKey(r => r.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ResourceAssignment>(e =>
            {
                e.HasIndex(a => new { a.ResourceId, a.FromDate, a.ToDate });
                e.HasOne(a => a.Resource).WithMany(r => r.Assignments).HasForeignKey(a => a.ResourceId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.Project).WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(a => a.Task).WithMany().HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.NoAction);
            });

            b.Entity<ResourceUnavailability>(e =>
            {
                e.HasIndex(u => new { u.ResourceId, u.FromDate });
                e.HasOne(u => u.Resource).WithMany(r => r.Unavailability).HasForeignKey(u => u.ResourceId).OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<TimeEntry>(e =>
            {
                e.HasIndex(t => new { t.UserId, t.WorkDate });
                e.HasIndex(t => new { t.ProjectId, t.WorkDate });
                e.HasIndex(t => t.Status);
                e.HasOne(t => t.Project).WithMany(p => p.TimeEntries).HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(t => t.Task).WithMany(x => x.TimeEntries).HasForeignKey(t => t.TaskId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(t => t.ApprovedBy).WithMany().HasForeignKey(t => t.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            });

            // ── Finance & procurement ─────────────────────────────────────────────
            b.Entity<BudgetLine>(e =>
            {
                e.HasIndex(l => new { l.ProjectId, l.Category });
                e.HasOne(l => l.Project).WithMany(p => p.BudgetLines).HasForeignKey(l => l.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(l => l.Phase).WithMany().HasForeignKey(l => l.PhaseId).OnDelete(DeleteBehavior.NoAction);
            });

            b.Entity<ProjectExpense>(e =>
            {
                e.HasIndex(x => new { x.ProjectId, x.Status });
                e.HasIndex(x => x.ExpenseDate);
                e.HasOne(x => x.Project).WithMany(p => p.Expenses).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.BudgetLine).WithMany().HasForeignKey(x => x.BudgetLineId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.SubmittedBy).WithMany().HasForeignKey(x => x.SubmittedById).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ProcurementRequest>(e =>
            {
                e.HasIndex(p => new { p.ProjectId, p.Status });
                e.HasOne(p => p.Project).WithMany().HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(p => p.BudgetLine).WithMany().HasForeignKey(p => p.BudgetLineId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(p => p.RequestedBy).WithMany().HasForeignKey(p => p.RequestedById).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(p => p.ApprovedBy).WithMany().HasForeignKey(p => p.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ProcurementQuote>(e =>
                e.HasOne(q => q.ProcurementRequest).WithMany().HasForeignKey(q => q.ProcurementRequestId).OnDelete(DeleteBehavior.Cascade));

            b.Entity<ProjectAsset>(e =>
            {
                e.HasIndex(a => new { a.ProjectId, a.ReturnedDate });
                e.HasOne(a => a.Project).WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.Asset).WithMany().HasForeignKey(a => a.AssetId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(a => a.IssuedTo).WithMany().HasForeignKey(a => a.IssuedToId).OnDelete(DeleteBehavior.Restrict);
            });

            // ── Governance ────────────────────────────────────────────────────────
            b.Entity<ProjectRisk>(e =>
            {
                e.HasIndex(r => new { r.ProjectId, r.Status });
                e.HasOne(r => r.Project).WithMany(p => p.Risks).HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(r => r.Owner).WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(r => r.CreatedBy).WithMany().HasForeignKey(r => r.CreatedById).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ProjectIssue>(e =>
            {
                e.HasIndex(i => new { i.ProjectId, i.Status });
                e.HasIndex(i => i.DueDate);
                e.HasOne(i => i.Project).WithMany(p => p.Issues).HasForeignKey(i => i.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(i => i.RaisedFromRisk).WithMany().HasForeignKey(i => i.RaisedFromRiskId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(i => i.AssignedTo).WithMany().HasForeignKey(i => i.AssignedToId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(i => i.RaisedBy).WithMany().HasForeignKey(i => i.RaisedById).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ProjectChangeRequest>(e =>
            {
                e.HasIndex(c => new { c.ProjectId, c.Status });
                e.HasOne(c => c.Project).WithMany(p => p.ChangeRequests).HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.RequestedBy).WithMany().HasForeignKey(c => c.RequestedById).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(c => c.ApprovedBy).WithMany().HasForeignKey(c => c.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<QualityCheck>(e =>
            {
                e.HasIndex(q => new { q.ProjectId, q.Result });
                e.HasOne(q => q.Project).WithMany().HasForeignKey(q => q.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(q => q.Deliverable).WithMany().HasForeignKey(q => q.DeliverableId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(q => q.Task).WithMany().HasForeignKey(q => q.TaskId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(q => q.Inspector).WithMany().HasForeignKey(q => q.InspectorId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ProjectApproval>(e =>
            {
                e.HasIndex(a => new { a.ApproverId, a.Status });
                e.HasIndex(a => new { a.Subject, a.SubjectId, a.Level });
                e.HasOne(a => a.Project).WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.Approver).WithMany().HasForeignKey(a => a.ApproverId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(a => a.RequestedBy).WithMany().HasForeignKey(a => a.RequestedById).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(a => a.DelegatedTo).WithMany().HasForeignKey(a => a.DelegatedToId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ProjectKpi>(e =>
            {
                e.HasOne(k => k.Project).WithMany().HasForeignKey(k => k.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(k => k.Owner).WithMany().HasForeignKey(k => k.OwnerId).OnDelete(DeleteBehavior.Restrict);
            });

            // ── Collaboration, templates & closure ────────────────────────────────
            b.Entity<ProjectDocument>(e =>
            {
                e.HasIndex(d => new { d.ProjectId, d.Type });
                e.HasQueryFilter(d => !d.IsDeleted);
                e.HasOne(d => d.Project).WithMany(p => p.Documents).HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(d => d.UploadedBy).WithMany().HasForeignKey(d => d.UploadedById).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(d => d.CheckedOutBy).WithMany().HasForeignKey(d => d.CheckedOutById).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(d => d.ApprovedBy).WithMany().HasForeignKey(d => d.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ProjectDocumentVersion>(e =>
            {
                // The parent document carries a query filter, so the dependent needs a matching one.
                e.HasQueryFilter(v => !v.Document!.IsDeleted);
                e.HasOne(v => v.Document).WithMany(d => d.Versions).HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(v => v.UploadedBy).WithMany().HasForeignKey(v => v.UploadedById).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ProjectMeeting>(e =>
            {
                e.HasIndex(m => new { m.ProjectId, m.ScheduledAt });
                e.HasOne(m => m.Project).WithMany().HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(m => m.Organiser).WithMany().HasForeignKey(m => m.OrganiserId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ProjectMeetingAttendee>(e =>
            {
                e.HasIndex(a => new { a.MeetingId, a.UserId });
                e.HasOne(a => a.Meeting).WithMany(m => m.Attendees).HasForeignKey(a => a.MeetingId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ProjectMeetingAction>(e =>
            {
                e.HasOne(a => a.Meeting).WithMany(m => m.Actions).HasForeignKey(a => a.MeetingId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.Owner).WithMany().HasForeignKey(a => a.OwnerId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(a => a.LinkedTask).WithMany().HasForeignKey(a => a.LinkedTaskId).OnDelete(DeleteBehavior.NoAction);
            });

            b.Entity<ProjectDiscussion>(e =>
            {
                e.HasIndex(d => new { d.ProjectId, d.CreatedAt });
                e.HasQueryFilter(d => !d.IsDeleted);
                e.HasOne(d => d.Project).WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(d => d.Parent).WithMany().HasForeignKey(d => d.ParentId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(d => d.Author).WithMany().HasForeignKey(d => d.AuthorId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<ProjectTemplate>(e =>
                e.HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict));

            b.Entity<ProjectTemplateItem>(e =>
            {
                e.HasIndex(i => new { i.TemplateId, i.Sequence });
                e.HasOne(i => i.Template).WithMany(t => t.Items).HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<ProjectClosure>(e =>
            {
                e.HasIndex(c => c.ProjectId).IsUnique();
                e.HasOne(c => c.Project).WithMany().HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.ClosedBy).WithMany().HasForeignKey(c => c.ClosedById).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<LessonLearned>(e =>
            {
                e.HasIndex(l => new { l.ProjectId, l.Category });
                e.HasOne(l => l.Project).WithMany().HasForeignKey(l => l.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(l => l.RaisedBy).WithMany().HasForeignKey(l => l.RaisedById).OnDelete(DeleteBehavior.Restrict);
            });

            // Built-in project templates — one per delivery domain the organisation runs.
            b.Entity<ProjectTemplate>().HasData(
                new ProjectTemplate { Id = 1, Name = "Software Delivery", Description = "Requirements → design → build → test → go-live, with the usual quality gates.", Category = ProjectCategory.Software, Type = ProjectType.Internal, DefaultDurationDays = 120, IsSystem = true, IsActive = true, CreatedAt = PmSeedDate },
                new ProjectTemplate { Id = 2, Name = "Construction / Site Works", Description = "Design, permits, mobilisation, construction, snagging and handover.", Category = ProjectCategory.Construction, Type = ProjectType.Capital, DefaultDurationDays = 180, IsSystem = true, IsActive = true, CreatedAt = PmSeedDate },
                new ProjectTemplate { Id = 3, Name = "Marketing Campaign", Description = "Brief, creative, production, launch and post-campaign review.", Category = ProjectCategory.Marketing, Type = ProjectType.Internal, DefaultDurationDays = 60, IsSystem = true, IsActive = true, CreatedAt = PmSeedDate },
                new ProjectTemplate { Id = 4, Name = "Research Study", Description = "Proposal, literature review, data collection, analysis and reporting.", Category = ProjectCategory.Research, Type = ProjectType.Research, DefaultDurationDays = 150, IsSystem = true, IsActive = true, CreatedAt = PmSeedDate },
                new ProjectTemplate { Id = 5, Name = "Maintenance Programme", Description = "Scheduled maintenance planning, execution, verification and close-out.", Category = ProjectCategory.Maintenance, Type = ProjectType.Maintenance, DefaultDurationDays = 45, IsSystem = true, IsActive = true, CreatedAt = PmSeedDate },
                new ProjectTemplate { Id = 6, Name = "ISO Implementation", Description = "Gap analysis, documentation, training, internal audit and certification.", Category = ProjectCategory.IsoCompliance, Type = ProjectType.Compliance, DefaultDurationDays = 240, IsSystem = true, IsActive = true, CreatedAt = PmSeedDate }
            );

            b.Entity<ProjectTemplateItem>().HasData(
                // 1 · Software Delivery
                new ProjectTemplateItem { Id = 1, TemplateId = 1, ItemType = "Phase", Name = "Requirements", Sequence = 1, StartOffsetDays = 0, DurationDays = 20 },
                new ProjectTemplateItem { Id = 2, TemplateId = 1, ItemType = "Task", Name = "Gather and document requirements", Sequence = 2, ParentSequence = 1, StartOffsetDays = 0, DurationDays = 15, EstimatedHours = 60 },
                new ProjectTemplateItem { Id = 3, TemplateId = 1, ItemType = "Milestone", Name = "Requirements approved", Sequence = 3, ParentSequence = 1, StartOffsetDays = 20, DurationDays = 0 },
                new ProjectTemplateItem { Id = 4, TemplateId = 1, ItemType = "Phase", Name = "Design", Sequence = 4, StartOffsetDays = 20, DurationDays = 20 },
                new ProjectTemplateItem { Id = 5, TemplateId = 1, ItemType = "Task", Name = "Solution and data design", Sequence = 5, ParentSequence = 4, StartOffsetDays = 20, DurationDays = 18, EstimatedHours = 80 },
                new ProjectTemplateItem { Id = 6, TemplateId = 1, ItemType = "Phase", Name = "Development", Sequence = 6, StartOffsetDays = 40, DurationDays = 45 },
                new ProjectTemplateItem { Id = 7, TemplateId = 1, ItemType = "Task", Name = "Implement features", Sequence = 7, ParentSequence = 6, StartOffsetDays = 40, DurationDays = 45, EstimatedHours = 320 },
                new ProjectTemplateItem { Id = 8, TemplateId = 1, ItemType = "Milestone", Name = "Development complete", Sequence = 8, ParentSequence = 6, StartOffsetDays = 85, DurationDays = 0 },
                new ProjectTemplateItem { Id = 9, TemplateId = 1, ItemType = "Phase", Name = "Testing", Sequence = 9, StartOffsetDays = 85, DurationDays = 25 },
                new ProjectTemplateItem { Id = 10, TemplateId = 1, ItemType = "Task", Name = "System and user acceptance testing", Sequence = 10, ParentSequence = 9, StartOffsetDays = 85, DurationDays = 25, EstimatedHours = 120 },
                new ProjectTemplateItem { Id = 11, TemplateId = 1, ItemType = "Milestone", Name = "Testing complete", Sequence = 11, ParentSequence = 9, StartOffsetDays = 110, DurationDays = 0 },
                new ProjectTemplateItem { Id = 12, TemplateId = 1, ItemType = "Phase", Name = "Go-live & handover", Sequence = 12, StartOffsetDays = 110, DurationDays = 10 },
                new ProjectTemplateItem { Id = 13, TemplateId = 1, ItemType = "Milestone", Name = "Go-live", Sequence = 13, ParentSequence = 12, StartOffsetDays = 115, DurationDays = 0 },
                new ProjectTemplateItem { Id = 14, TemplateId = 1, ItemType = "Milestone", Name = "Project closure", Sequence = 14, ParentSequence = 12, StartOffsetDays = 120, DurationDays = 0 },

                // 2 · Construction
                new ProjectTemplateItem { Id = 20, TemplateId = 2, ItemType = "Phase", Name = "Design & approvals", Sequence = 1, StartOffsetDays = 0, DurationDays = 45 },
                new ProjectTemplateItem { Id = 21, TemplateId = 2, ItemType = "Task", Name = "Detailed drawings and permits", Sequence = 2, ParentSequence = 1, StartOffsetDays = 0, DurationDays = 45, EstimatedHours = 150 },
                new ProjectTemplateItem { Id = 22, TemplateId = 2, ItemType = "Milestone", Name = "Permits issued", Sequence = 3, ParentSequence = 1, StartOffsetDays = 45, DurationDays = 0 },
                new ProjectTemplateItem { Id = 23, TemplateId = 2, ItemType = "Phase", Name = "Mobilisation", Sequence = 4, StartOffsetDays = 45, DurationDays = 15 },
                new ProjectTemplateItem { Id = 24, TemplateId = 2, ItemType = "Phase", Name = "Construction", Sequence = 5, StartOffsetDays = 60, DurationDays = 100 },
                new ProjectTemplateItem { Id = 25, TemplateId = 2, ItemType = "Phase", Name = "Snagging & handover", Sequence = 6, StartOffsetDays = 160, DurationDays = 20 },
                new ProjectTemplateItem { Id = 26, TemplateId = 2, ItemType = "Milestone", Name = "Practical completion", Sequence = 7, ParentSequence = 6, StartOffsetDays = 180, DurationDays = 0 },

                // 3 · Marketing
                new ProjectTemplateItem { Id = 30, TemplateId = 3, ItemType = "Phase", Name = "Brief & strategy", Sequence = 1, StartOffsetDays = 0, DurationDays = 10 },
                new ProjectTemplateItem { Id = 31, TemplateId = 3, ItemType = "Phase", Name = "Creative & production", Sequence = 2, StartOffsetDays = 10, DurationDays = 25 },
                new ProjectTemplateItem { Id = 32, TemplateId = 3, ItemType = "Phase", Name = "Launch", Sequence = 3, StartOffsetDays = 35, DurationDays = 15 },
                new ProjectTemplateItem { Id = 33, TemplateId = 3, ItemType = "Milestone", Name = "Campaign live", Sequence = 4, ParentSequence = 3, StartOffsetDays = 40, DurationDays = 0 },
                new ProjectTemplateItem { Id = 34, TemplateId = 3, ItemType = "Phase", Name = "Review & reporting", Sequence = 5, StartOffsetDays = 50, DurationDays = 10 },

                // 4 · Research
                new ProjectTemplateItem { Id = 40, TemplateId = 4, ItemType = "Phase", Name = "Proposal & ethics", Sequence = 1, StartOffsetDays = 0, DurationDays = 25 },
                new ProjectTemplateItem { Id = 41, TemplateId = 4, ItemType = "Phase", Name = "Literature review", Sequence = 2, StartOffsetDays = 25, DurationDays = 30 },
                new ProjectTemplateItem { Id = 42, TemplateId = 4, ItemType = "Phase", Name = "Data collection", Sequence = 3, StartOffsetDays = 55, DurationDays = 50 },
                new ProjectTemplateItem { Id = 43, TemplateId = 4, ItemType = "Phase", Name = "Analysis", Sequence = 4, StartOffsetDays = 105, DurationDays = 25 },
                new ProjectTemplateItem { Id = 44, TemplateId = 4, ItemType = "Phase", Name = "Reporting", Sequence = 5, StartOffsetDays = 130, DurationDays = 20 },
                new ProjectTemplateItem { Id = 45, TemplateId = 4, ItemType = "Milestone", Name = "Final report published", Sequence = 6, ParentSequence = 5, StartOffsetDays = 150, DurationDays = 0 },

                // 5 · Maintenance
                new ProjectTemplateItem { Id = 50, TemplateId = 5, ItemType = "Phase", Name = "Planning & scheduling", Sequence = 1, StartOffsetDays = 0, DurationDays = 10 },
                new ProjectTemplateItem { Id = 51, TemplateId = 5, ItemType = "Phase", Name = "Execution", Sequence = 2, StartOffsetDays = 10, DurationDays = 25 },
                new ProjectTemplateItem { Id = 52, TemplateId = 5, ItemType = "Phase", Name = "Verification & close-out", Sequence = 3, StartOffsetDays = 35, DurationDays = 10 },
                new ProjectTemplateItem { Id = 53, TemplateId = 5, ItemType = "Milestone", Name = "Maintenance signed off", Sequence = 4, ParentSequence = 3, StartOffsetDays = 45, DurationDays = 0 },

                // 6 · ISO implementation
                new ProjectTemplateItem { Id = 60, TemplateId = 6, ItemType = "Phase", Name = "Gap analysis", Sequence = 1, StartOffsetDays = 0, DurationDays = 30 },
                new ProjectTemplateItem { Id = 61, TemplateId = 6, ItemType = "Phase", Name = "Documentation", Sequence = 2, StartOffsetDays = 30, DurationDays = 70 },
                new ProjectTemplateItem { Id = 62, TemplateId = 6, ItemType = "Phase", Name = "Training & awareness", Sequence = 3, StartOffsetDays = 100, DurationDays = 40 },
                new ProjectTemplateItem { Id = 63, TemplateId = 6, ItemType = "Phase", Name = "Internal audit", Sequence = 4, StartOffsetDays = 140, DurationDays = 40 },
                new ProjectTemplateItem { Id = 64, TemplateId = 6, ItemType = "Milestone", Name = "Management review held", Sequence = 5, ParentSequence = 4, StartOffsetDays = 180, DurationDays = 0 },
                new ProjectTemplateItem { Id = 65, TemplateId = 6, ItemType = "Phase", Name = "Certification audit", Sequence = 6, StartOffsetDays = 180, DurationDays = 60 },
                new ProjectTemplateItem { Id = 66, TemplateId = 6, ItemType = "Milestone", Name = "Certification achieved", Sequence = 7, ParentSequence = 6, StartOffsetDays = 240, DurationDays = 0 }
            );
        }
    }
}
