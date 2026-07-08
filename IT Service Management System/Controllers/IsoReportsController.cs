using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers.Ims;
using IT_Service_Management_System.Models.Ims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// ISO Reports — Excel (ClosedXML) register exports and PDF (QuestPDF) report packs across the IMS:
    /// documents, CAPAs, risks, audit findings, training, suppliers, plus the management-review pack.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class IsoReportsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public IsoReportsController(ApplicationDbContext db) => _db = db;

        private bool Can(ImsPermission p) => ImsAccess.Can(HttpContext.Session.GetString("UserRole"), p);
        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        private static string Stamp => DateTime.Now.ToString("yyyyMMdd");
        private static string D(DateTime? d) => d?.ToString("yyyy-MM-dd") ?? "";

        public IActionResult Index() => View();

        // ── Excel register exports ──────────────────────────────────────────────────
        public async Task<IActionResult> DocumentsXlsx()
        {
            if (!Can(ImsPermission.ViewReports)) return Denied();
            var docs = await _db.IsoDocuments.Include(d => d.Category).Include(d => d.Department).Include(d => d.Owner)
                .OrderBy(d => d.DocumentNumber).ToListAsync();
            var rows = docs.Select(d => new[]
            {
                d.DocumentNumber, d.Title, d.Type.ToString(), d.Category?.Name ?? "", d.Department?.Name ?? "",
                d.Owner?.FullName ?? "", d.CurrentVersion, d.Status.ToString(), d.Classification.ToString(),
                IsoStandards.Label(d.Standard), d.IsoClause ?? "", D(d.EffectiveDate), D(d.ReviewDate)
            });
            var bytes = ImsExport.Xlsx("Documents", new[]
            {
                "Document No.", "Title", "Type", "Category", "Department", "Owner", "Version", "Status",
                "Classification", "Standard", "Clause", "Effective", "Next Review"
            }, rows);
            return File(bytes, ImsExport.XlsxContentType, $"ISO_Documents_{Stamp}.xlsx");
        }

        public async Task<IActionResult> CapaXlsx()
        {
            if (!Can(ImsPermission.ViewReports)) return Denied();
            var capas = await _db.Capas.Include(c => c.Responsible).Include(c => c.Department).OrderBy(c => c.Id).ToListAsync();
            var rows = capas.Select(c => new[]
            {
                c.Reference, c.Title, c.Type.ToString(), c.Source.ToString(), c.Responsible?.FullName ?? "",
                c.Department?.Name ?? "", D(c.DueDate), c.Status.ToString(), c.IsOverdue ? "Yes" : "No"
            });
            var bytes = ImsExport.Xlsx("CAPA", new[]
            { "Ref", "Title", "Type", "Source", "Responsible", "Department", "Due", "Status", "Overdue" }, rows);
            return File(bytes, ImsExport.XlsxContentType, $"ISO_CAPA_{Stamp}.xlsx");
        }

        public async Task<IActionResult> RiskXlsx()
        {
            if (!Can(ImsPermission.ViewReports)) return Denied();
            var risks = await _db.Risks.Include(r => r.Owner).Include(r => r.Department).OrderByDescending(r => r.Id).ToListAsync();
            var rows = risks.Select(r => new[]
            {
                r.Reference, r.Title, r.Category.ToString(), r.Likelihood.ToString(), r.Impact.ToString(),
                r.Score.ToString(), r.Band.ToString(), r.Treatment.ToString(), r.Owner?.FullName ?? "",
                r.ResidualScore?.ToString() ?? "", r.Status.ToString()
            });
            var bytes = ImsExport.Xlsx("Risk Register", new[]
            { "Ref", "Title", "Category", "Likelihood", "Impact", "Score", "Band", "Treatment", "Owner", "Residual", "Status" }, rows);
            return File(bytes, ImsExport.XlsxContentType, $"ISO_Risks_{Stamp}.xlsx");
        }

        public async Task<IActionResult> FindingsXlsx()
        {
            if (!Can(ImsPermission.ViewReports)) return Denied();
            var findings = await _db.AuditFindings.Include(f => f.Audit).Include(f => f.AssignedTo).Include(f => f.Department)
                .OrderByDescending(f => f.Id).ToListAsync();
            var rows = findings.Select(f => new[]
            {
                f.Reference, f.Audit?.Reference ?? "", f.Type.ToString(), f.ClauseReference ?? "",
                f.Department?.Name ?? "", f.AssignedTo?.FullName ?? "", D(f.DueDate), f.Status.ToString()
            });
            var bytes = ImsExport.Xlsx("Findings", new[]
            { "Ref", "Audit", "Type", "Clause", "Department", "Assigned", "Due", "Status" }, rows);
            return File(bytes, ImsExport.XlsxContentType, $"ISO_Findings_{Stamp}.xlsx");
        }

        public async Task<IActionResult> TrainingXlsx()
        {
            if (!Can(ImsPermission.ViewReports)) return Denied();
            var records = await _db.TrainingRecords.Include(t => t.TrainingCourse).Include(t => t.User)
                .OrderByDescending(t => t.Id).ToListAsync();
            var rows = records.Select(t => new[]
            {
                t.TrainingCourse?.Reference ?? "", t.TrainingCourse?.Title ?? "", t.User?.FullName ?? "",
                t.Status.ToString(), D(t.CompletedDate), t.CertificateName ?? "", D(t.CertificateExpiry)
            });
            var bytes = ImsExport.Xlsx("Training", new[]
            { "Course Ref", "Course", "Employee", "Status", "Completed", "Certificate", "Expiry" }, rows);
            return File(bytes, ImsExport.XlsxContentType, $"ISO_Training_{Stamp}.xlsx");
        }

        public async Task<IActionResult> SuppliersXlsx()
        {
            if (!Can(ImsPermission.ViewReports)) return Denied();
            var suppliers = await _db.Suppliers.Include(s => s.Evaluations).OrderBy(s => s.Name).ToListAsync();
            var rows = suppliers.Select(s =>
            {
                var latest = s.Evaluations.OrderByDescending(e => e.EvaluationDate).FirstOrDefault();
                return new[]
                {
                    s.Reference, s.Name, s.Category.ToString(), s.Status.ToString(),
                    latest?.OverallScore.ToString() ?? "", latest?.Rating ?? "", D(s.ContractEnd), D(s.CertificateExpiry)
                };
            });
            var bytes = ImsExport.Xlsx("Suppliers", new[]
            { "Ref", "Name", "Category", "Status", "Latest Score", "Rating", "Contract End", "Cert Expiry" }, rows);
            return File(bytes, ImsExport.XlsxContentType, $"ISO_Suppliers_{Stamp}.xlsx");
        }

        // ── PDF: executive summary / management-review pack ─────────────────────────
        public async Task<IActionResult> ExecutiveSummaryPdf()
        {
            if (!Can(ImsPermission.ViewReports)) return Denied();
            var today = DateTime.Now.Date;

            var docRows = new[]
            {
                new[] { "Published", (await _db.IsoDocuments.CountAsync(d => d.Status == DocumentStatus.Published)).ToString() },
                new[] { "In Workflow", (await _db.IsoDocuments.CountAsync(d => d.Status == DocumentStatus.DepartmentReview || d.Status == DocumentStatus.QualityReview || d.Status == DocumentStatus.ManagementApproval)).ToString() },
                new[] { "Due for Review", (await _db.IsoDocuments.CountAsync(d => d.Status == DocumentStatus.Published && d.ReviewDate != null && d.ReviewDate <= today)).ToString() }
            };

            var capaRows = new[]
            {
                new[] { "Open", (await _db.Capas.CountAsync(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified)).ToString() },
                new[] { "Overdue", (await _db.Capas.CountAsync(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified && c.DueDate != null && c.DueDate < today)).ToString() },
                new[] { "Closed", (await _db.Capas.CountAsync(c => c.Status == CapaStatus.Closed || c.Status == CapaStatus.Verified)).ToString() }
            };

            var topRisks = (await _db.Risks.Where(r => r.Status != RiskStatus.Closed).ToListAsync())
                .OrderByDescending(r => r.Score).Take(8)
                .Select(r => new[] { r.Reference, r.Title, r.Score.ToString(), r.Band.ToString() });

            var openFindings = await _db.AuditFindings.Include(f => f.Audit)
                .Where(f => f.Status != FindingStatus.Closed)
                .OrderByDescending(f => f.Id).Take(10)
                .Select(f => new[] { f.Audit != null ? f.Audit.Reference : "", f.Type.ToString(), f.ClauseReference ?? "" })
                .ToListAsync();

            var sections = new List<(string, string[], IEnumerable<string[]>)>
            {
                ("Documents", new[] { "Metric", "Count" }, docRows),
                ("Corrective &  Preventive Actions", new[] { "Metric", "Count" }, capaRows),
                ("Top Risks", new[] { "Ref", "Title", "Score", "Band" }, topRisks),
                ("Open Audit Findings", new[] { "Audit", "Type", "Clause" }, openFindings)
            };

            var bytes = ImsExport.SummaryPdf("ISO Executive Summary",
                $"Integrated Management System — {DateTime.Now:dd MMMM yyyy}", sections);
            return File(bytes, "application/pdf", $"ISO_Executive_Summary_{Stamp}.pdf");
        }
    }
}
