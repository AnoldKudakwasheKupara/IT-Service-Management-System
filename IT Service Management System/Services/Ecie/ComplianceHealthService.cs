using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.ViewModels.Ecie;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Ecie
{
    /// <summary>
    /// Continuously computes the enterprise Compliance Health Score from live IMS data — ten dimensions
    /// plus a weighted overall, and a set of actionable alerts. Every figure is derived from stored
    /// records (no estimation), so the dashboard is itself audit evidence.
    /// </summary>
    public class ComplianceHealthService
    {
        private readonly ApplicationDbContext _db;
        public ComplianceHealthService(ApplicationDbContext db) => _db = db;

        private static int Pct(int part, int total) => total <= 0 ? 100 : (int)Math.Round(part * 100.0 / total);
        private static int Clamp(int v) => Math.Clamp(v, 0, 100);

        public async Task<ComplianceHealthVm> ComputeAsync()
        {
            var vm = new ComplianceHealthVm();
            var today = DateTime.Now.Date;

            // ── Document compliance ──
            var published = await _db.IsoDocuments.CountAsync(d => d.Status == DocumentStatus.Published);
            var docProblems = await _db.IsoDocuments.CountAsync(d => d.Status == DocumentStatus.Published &&
                ((d.ReviewDate != null && d.ReviewDate <= today) || (d.ExpiryDate != null && d.ExpiryDate < today)));
            var docScore = Pct(Math.Max(0, published - docProblems), published);

            // ── Training compliance ──
            var trainingTotal = await _db.TrainingRecords.CountAsync();
            var trainingDone = await _db.TrainingRecords.CountAsync(t => t.Status == AttendanceStatus.Completed);
            var trainingScore = Pct(trainingDone, trainingTotal);

            // ── Supplier compliance ──
            var evals = await _db.SupplierEvaluations
                .Select(e => new { e.QualityScore, e.DeliveryScore, e.PricingScore, e.SupportScore, e.ComplianceScore }).ToListAsync();
            int supplierScore;
            if (evals.Count > 0)
                supplierScore = Clamp((int)Math.Round(evals.Average(e =>
                    (e.QualityScore + e.DeliveryScore + e.PricingScore + e.SupportScore + e.ComplianceScore) / 5.0)));
            else
            {
                var suppTotal = await _db.Suppliers.CountAsync();
                var suppApproved = await _db.Suppliers.CountAsync(s => s.Status == SupplierStatus.Approved);
                supplierScore = Pct(suppApproved, suppTotal);
            }

            // ── Risk compliance ──
            var openRisks = await _db.Risks.Where(r => r.Status != RiskStatus.Closed).Select(r => new { r.Likelihood, r.Impact }).ToListAsync();
            var criticalOpen = openRisks.Count(r => RiskScoring.BandFor(r.Likelihood * r.Impact) == RiskBand.Critical);
            var riskScore = openRisks.Count == 0 ? 100 : Clamp((int)Math.Round((openRisks.Count - criticalOpen * 1.5) / openRisks.Count * 100));

            // ── CAPA effectiveness ──
            var capaTotal = await _db.Capas.CountAsync();
            var capaEffective = await _db.Capas.CountAsync(c => c.Status == CapaStatus.Verified || c.Status == CapaStatus.Closed);
            var capaOverdue = await _db.Capas.CountAsync(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified && c.DueDate != null && c.DueDate < today);
            var capaScore = Clamp(Pct(capaEffective, capaTotal) - capaOverdue * 5);

            // ── Audit readiness ──
            var openFindings = await _db.AuditFindings.CountAsync(f => f.Status != FindingStatus.Closed);
            var auditsRecently = await _db.Audits.CountAsync(a => (a.Status == AuditStatus.Completed || a.Status == AuditStatus.Closed) && a.ActualEndDate != null && a.ActualEndDate >= today.AddYears(-1));
            var auditScore = Clamp(100 - openFindings * 3 - capaOverdue * 4 - docProblems * 2 - (auditsRecently == 0 ? 25 : 0));

            // ── Security readiness (ISO 27001) ──
            var infosecCritical = (await _db.Risks.Where(r => r.Category == RiskCategory.InformationSecurity && r.Status != RiskStatus.Closed)
                .Select(r => new { r.Likelihood, r.Impact }).ToListAsync())
                .Count(r => RiskScoring.BandFor(r.Likelihood * r.Impact) == RiskBand.Critical);
            var ismsDocs = await _db.IsoDocuments.CountAsync(d => d.Status == DocumentStatus.Published &&
                (d.Standard == IsoStandard.Iso27001 || d.Standard == IsoStandard.Both));
            var securityScore = Clamp(100 - infosecCritical * 10 - (ismsDocs == 0 ? 30 : 0));

            // ── Management review completion ──
            var reviewsHeldRecently = await _db.ManagementReviews.CountAsync(m =>
                (m.Status == ReviewMeetingStatus.Held || m.Status == ReviewMeetingStatus.Closed) && m.MeetingDate >= today.AddYears(-1));
            var overdueActions = await _db.ManagementReviewActions.CountAsync(a =>
                a.Status != ReviewActionStatus.Completed && a.Status != ReviewActionStatus.Cancelled && a.DueDate != null && a.DueDate < today);
            var reviewScore = reviewsHeldRecently > 0 ? Clamp(100 - overdueActions * 5) : 30;

            // ── Employee awareness ──
            var ackTotal = await _db.IsoDocumentAcknowledgements.CountAsync();
            var ackDone = await _db.IsoDocumentAcknowledgements.CountAsync(a => a.Status == AcknowledgementStatus.Acknowledged);
            var awarenessScore = Pct(ackDone, ackTotal);

            vm.Scores = new List<ScoreItem>
            {
                new() { Name = "Document Compliance",  Score = docScore,       Icon = "fa-file-shield",     Detail = $"{docProblems} of {published} published documents need review/renewal" },
                new() { Name = "Training Compliance",  Score = trainingScore,  Icon = "fa-graduation-cap",  Detail = $"{trainingDone} of {trainingTotal} training records completed" },
                new() { Name = "Supplier Compliance",  Score = supplierScore,  Icon = "fa-truck-field",     Detail = $"Average supplier performance score" },
                new() { Name = "Risk Compliance",      Score = riskScore,      Icon = "fa-gauge-high",      Detail = $"{criticalOpen} critical of {openRisks.Count} open risks" },
                new() { Name = "CAPA Effectiveness",   Score = capaScore,      Icon = "fa-screwdriver-wrench", Detail = $"{capaEffective} of {capaTotal} CAPAs verified/closed · {capaOverdue} overdue" },
                new() { Name = "Audit Readiness",      Score = auditScore,     Icon = "fa-clipboard-check", Detail = $"{openFindings} open findings · {(auditsRecently == 0 ? "no audit in 12 months" : "audited within 12 months")}" },
                new() { Name = "Security Readiness",   Score = securityScore,  Icon = "fa-lock",            Detail = $"{infosecCritical} critical info-sec risks · {ismsDocs} ISMS documents" },
                new() { Name = "Mgmt Review",          Score = reviewScore,    Icon = "fa-people-group",    Detail = $"{reviewsHeldRecently} review(s) in 12 months · {overdueActions} overdue actions" },
                new() { Name = "Employee Awareness",   Score = awarenessScore, Icon = "fa-user-check",      Detail = $"{ackDone} of {ackTotal} acknowledgements complete" },
            };

            vm.Overall = (int)Math.Round(vm.Scores.Average(s => s.Score));

            // ── Alerts ──
            if (docProblems > 0) vm.Alerts.Add($"{docProblems} published document(s) are overdue for review or expired.");
            if (capaOverdue > 0) vm.Alerts.Add($"{capaOverdue} CAPA(s) are past their due date.");
            if (criticalOpen > 0) vm.Alerts.Add($"{criticalOpen} critical risk(s) are open.");
            if (openFindings > 0) vm.Alerts.Add($"{openFindings} audit finding(s) remain open.");
            if (ackTotal - ackDone > 0) vm.Alerts.Add($"{ackTotal - ackDone} document acknowledgement(s) are pending.");
            if (reviewsHeldRecently == 0) vm.Alerts.Add("No management review has been held in the last 12 months.");
            var expiringCerts = await _db.TrainingRecords.CountAsync(t => t.CertificateExpiry != null && t.CertificateExpiry >= today && t.CertificateExpiry <= today.AddDays(30));
            if (expiringCerts > 0) vm.Alerts.Add($"{expiringCerts} training certificate(s) expire within 30 days.");

            return vm;
        }
    }
}
