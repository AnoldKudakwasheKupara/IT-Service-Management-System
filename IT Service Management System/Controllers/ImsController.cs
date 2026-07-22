using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.ViewModels.Ims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// The Integrated Management System (ISO 9001:2015 &amp; ISO/IEC 27001:2022) executive dashboard —
    /// a live roll-up of documents, acknowledgements, CAPAs, risks, audits, training, suppliers,
    /// objectives and compliance across the whole module.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class ImsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ImsController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var vm = new ImsDashboardVm();

            // ── Documents ──
            var docs = _db.IsoDocuments;
            vm.TotalDocuments = await docs.CountAsync();
            vm.PublishedDocuments = await docs.CountAsync(d => d.Status == DocumentStatus.Published);
            vm.DraftDocuments = await docs.CountAsync(d => d.Status == DocumentStatus.Draft);
            vm.InWorkflowDocuments = await docs.CountAsync(d =>
                d.Status == DocumentStatus.DepartmentReview || d.Status == DocumentStatus.QualityReview || d.Status == DocumentStatus.ManagementApproval);
            var today = DateTime.Now.Date;
            vm.DocumentsDueReview = await docs.CountAsync(d => d.Status == DocumentStatus.Published && d.ReviewDate != null && d.ReviewDate <= today);
            vm.ExpiredDocuments = await docs.CountAsync(d => d.ExpiryDate != null && d.ExpiryDate < today);
            vm.Policies = await docs.CountAsync(d => d.Type == DocumentType.Policy);
            vm.Procedures = await docs.CountAsync(d => d.Type == DocumentType.Procedure);
            vm.Forms = await docs.CountAsync(d => d.Type == DocumentType.Form);

            // ── Acknowledgements ──
            vm.TotalAcknowledgements = await _db.IsoDocumentAcknowledgements.CountAsync();
            vm.CompletedAcknowledgements = await _db.IsoDocumentAcknowledgements.CountAsync(a => a.Status == AcknowledgementStatus.Acknowledged);
            vm.AcknowledgementPercent = Percent(vm.CompletedAcknowledgements, vm.TotalAcknowledgements);

            // ── CAPA / NC ──
            vm.OpenCapas = await _db.Capas.CountAsync(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified);
            vm.ClosedCapas = await _db.Capas.CountAsync(c => c.Status == CapaStatus.Closed || c.Status == CapaStatus.Verified);
            vm.OverdueCapas = await _db.Capas.CountAsync(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified && c.DueDate != null && c.DueDate < today);
            vm.OpenNonConformances = await _db.NonConformances.CountAsync(n => n.Status != NcStatus.Closed);

            // ── Audits & findings ──
            vm.OpenAudits = await _db.Audits.CountAsync(a => a.Status != AuditStatus.Closed && a.Status != AuditStatus.Cancelled);
            vm.CompletedAudits = await _db.Audits.CountAsync(a => a.Status == AuditStatus.Completed || a.Status == AuditStatus.Closed);
            vm.OpenFindings = await _db.AuditFindings.CountAsync(f => f.Status != FindingStatus.Closed);

            // ── Risk ──
            var openRisks = await _db.Risks.Where(r => r.Status != RiskStatus.Closed)
                .Select(r => new { r.Likelihood, r.Impact }).ToListAsync();
            vm.OpenRisks = openRisks.Count;
            foreach (var r in openRisks)
            {
                var l = Math.Clamp(r.Likelihood, 1, 5);
                var i = Math.Clamp(r.Impact, 1, 5);
                vm.RiskHeatmap[(i - 1) * 5 + (l - 1)]++;
                if (RiskScoring.BandFor(l * i) == RiskBand.Critical) vm.CriticalRisks++;
            }

            // ── Training ──
            vm.TrainingRecords = await _db.TrainingRecords.CountAsync();
            vm.TrainingCompleted = await _db.TrainingRecords.CountAsync(t => t.Status == AttendanceStatus.Completed);
            vm.TrainingCompletionPercent = Percent(vm.TrainingCompleted, vm.TrainingRecords);
            vm.ExpiringCertificates = await _db.TrainingRecords.CountAsync(t =>
                t.CertificateExpiry != null && t.CertificateExpiry >= today && t.CertificateExpiry <= today.AddDays(60));

            // ── Suppliers ──
            vm.Suppliers = await _db.Suppliers.CountAsync();
            var evals = await _db.SupplierEvaluations
                .Select(e => new { e.QualityScore, e.DeliveryScore, e.PricingScore, e.SupportScore, e.ComplianceScore })
                .ToListAsync();
            if (evals.Count > 0)
                vm.AverageSupplierScore = (int)Math.Round(evals.Average(e =>
                    (e.QualityScore + e.DeliveryScore + e.PricingScore + e.SupportScore + e.ComplianceScore) / 5.0));

            // ── Objectives / compliance / improvement ──
            vm.ActiveObjectives = await _db.Objectives.CountAsync(o => o.Status != ObjectiveStatus.Closed && o.Status != ObjectiveStatus.Draft);
            vm.ObjectivesAtRisk = await _db.Objectives.CountAsync(o => o.Status == ObjectiveStatus.AtRisk || o.Status == ObjectiveStatus.NotAchieved);
            vm.ComplianceObligations = await _db.ComplianceObligations.CountAsync();
            var compliant = await _db.ComplianceObligations.CountAsync(o => o.Status == ComplianceStatus.Compliant);
            vm.CompliancePercent = Percent(compliant, vm.ComplianceObligations);
            vm.OpenImprovements = await _db.Improvements.CountAsync(i =>
                i.Status != ImprovementStatus.Implemented && i.Status != ImprovementStatus.Rejected && i.Status != ImprovementStatus.Closed);

            // ── Panels ──
            vm.UpcomingReviews = await docs
                .Where(d => d.Status == DocumentStatus.Published && d.ReviewDate != null)
                .OrderBy(d => d.ReviewDate)
                .Take(6)
                .ToListAsync();
            vm.CapaWatchlist = await _db.Capas
                .Where(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified)
                .Include(c => c.Responsible)
                .OrderBy(c => c.DueDate ?? DateTime.MaxValue)
                .Take(6)
                .ToListAsync();
            vm.TopRisks = await _db.Risks
                .Where(r => r.Status != RiskStatus.Closed)
                .Include(r => r.Owner)
                .ToListAsync();
            vm.TopRisks = vm.TopRisks.OrderByDescending(r => r.Score).Take(6).ToList();

            return View(vm);
        }

        private static int Percent(int part, int total) => total <= 0 ? 0 : (int)Math.Round(part * 100.0 / total);
    }
}
