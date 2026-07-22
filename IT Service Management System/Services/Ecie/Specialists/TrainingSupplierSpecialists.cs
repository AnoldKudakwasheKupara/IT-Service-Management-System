using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.ViewModels.Ecie;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Ecie.Specialists
{
    /// <summary>
    /// Advises on training, competency and awareness (ISO 9001 / 27001 cl. 7.2). Answers three grounded
    /// sub-cases — who has not acknowledged an approved document, which certificates are expiring, and where
    /// competency requirements are not met — plus a generic training-catalogue fallback.
    /// </summary>
    public class TrainingAdvisorSpecialist : IEcieSpecialist
    {
        public string Name => "Training Advisor";
        public string Icon => "fa-graduation-cap";

        public int Score(EcieContext ctx)
        {
            var s = 0;
            if (ctx.Mentions("training", "course", "competency", "competence", "certificate",
                    "expire", "expiring", "acknowledg", "awareness")) s += 40;
            return s;
        }

        public async Task<EcieResponse> HandleAsync(EcieContext ctx)
        {
            var r = new EcieResponse();
            var _db = ctx.Db;
            var today = DateTime.Now.Date;

            // ── a) Acknowledgement — who has not acknowledged a named approved document ──
            if (ctx.Mentions("acknowledg"))
            {
                var matches = await ctx.Graph.SearchApprovedDocumentsAsync(ctx.Role, ctx.Tokens);
                var doc = matches.FirstOrDefault();
                if (doc == null)
                {
                    r.Summary = "No approved document matches that name.";
                    r.NextActions.Add("Publish the relevant controlled document in Document Control, then assign it for acknowledgement.");
                    return r;
                }

                var pending = await _db.IsoDocumentAcknowledgements
                    .Include(a => a.User)
                    .Where(a => a.IsoDocumentId == doc.Id && a.Status != AcknowledgementStatus.Acknowledged)
                    .ToListAsync();

                r.EvidenceUsed.Add(EvidenceGraphService.Doc(doc));
                foreach (var a in pending)
                {
                    r.EvidenceUsed.Add(new EvidenceRef
                    {
                        Kind = "Employee",
                        Reference = "",
                        Title = a.User?.FullName ?? "Unknown",
                        Subtitle = "Pending acknowledgement",
                        Icon = "fa-user",
                        Url = null
                    });
                }
                r.RelatedDocuments.Add(EvidenceGraphService.Doc(doc));

                r.Summary = $"{pending.Count} employee(s) have not yet acknowledged \"{doc.Title}\" ({doc.DocumentNumber}).";
                foreach (var a in pending.Take(15))
                    r.Answer.Add($"{a.User?.FullName ?? "Unknown"} — pending since {a.AssignedAt:dd MMM yyyy}.");

                if (pending.Count > 0)
                {
                    r.Recommendations.Add("Follow up with the outstanding staff to complete their acknowledgement.");
                    r.NextActions.Add($"Send acknowledgement reminders for \"{doc.Title}\" ({doc.DocumentNumber}).");
                    r.PotentialRisks.Add($"Unacknowledged staff may be unaware of {doc.DocumentNumber}, weakening the audit trail for cl. 7.2/7.5.");
                }
                else
                {
                    r.Answer.Add($"All assigned employees have acknowledged \"{doc.Title}\".");
                }
                return r;
            }

            // ── b) Certificate expiry ────────────────────────────────────────────────
            if (ctx.Mentions("expire", "expiring", "certificate"))
            {
                var cutoff = ctx.Mentions("this month") ? today.AddDays(30) : today.AddDays(60);
                var records = await _db.TrainingRecords
                    .Include(t => t.User)
                    .Include(t => t.TrainingCourse)
                    .Where(t => t.CertificateExpiry != null && t.CertificateExpiry <= cutoff)
                    .OrderBy(t => t.CertificateExpiry)
                    .ToListAsync();

                if (records.Count == 0)
                {
                    r.Summary = "No training certificates are expiring in the selected window.";
                    return r;
                }

                r.EvidenceUsed = records.Take(15).Select(t => new EvidenceRef
                {
                    Kind = "Training",
                    Reference = "",
                    Title = $"{t.User?.FullName ?? "Unknown"} — {t.CertificateName ?? t.TrainingCourse?.Title ?? "Certificate"}",
                    Subtitle = $"Expires {t.CertificateExpiry:dd MMM yyyy}",
                    Icon = "fa-graduation-cap",
                    Badge = t.IsCertificateExpired ? "Expired" : "Expiring",
                    Url = "/Training"
                }).ToList();
                r.RelatedTraining = r.EvidenceUsed;

                var expired = records.Count(t => t.IsCertificateExpired);
                r.Summary = $"{records.Count} training certificate(s) are expired or expiring soon" +
                            (expired > 0 ? $", including {expired} already expired." : ".");
                foreach (var t in records.Take(10))
                    r.Answer.Add($"{t.User?.FullName ?? "Unknown"} — {t.CertificateName ?? t.TrainingCourse?.Title} " +
                                 $"({(t.IsCertificateExpired ? "expired" : "expires")} {t.CertificateExpiry:dd MMM yyyy}).");

                r.Recommendations.Add("Schedule refresher / recertification training before these certificates lapse.");
                r.NextActions.Add("Book renewal sessions and update the training records with the new expiry dates.");
                r.PotentialRisks.AddRange(records.Where(t => t.IsCertificateExpired).Take(3)
                    .Select(t => $"{t.User?.FullName ?? "Unknown"} holds an expired certificate ({t.CertificateName ?? t.TrainingCourse?.Title})."));
                return r;
            }

            // ── c) Competency gaps ───────────────────────────────────────────────────
            if (ctx.Mentions("competency", "competence", "gap"))
            {
                var all = await _db.UserCompetencies
                    .Include(u => u.User)
                    .Include(u => u.Competency)
                    .ToListAsync();
                var gaps = all.Where(u => !u.MeetsRequirement).ToList();

                if (gaps.Count == 0)
                {
                    r.Summary = "No competency gaps are recorded — all assessed employees meet their required levels.";
                    return r;
                }

                r.EvidenceUsed = gaps.Take(15).Select(u => new EvidenceRef
                {
                    Kind = "Competency",
                    Reference = "",
                    Title = $"{u.User?.FullName ?? "Unknown"} — {u.Competency?.Name ?? "Competency"}",
                    Subtitle = $"{u.Level} (requires {u.RequiredLevel})",
                    Icon = "fa-user-graduate",
                    Url = null
                }).ToList();

                r.Summary = $"{gaps.Count} competency gap(s) found where assessed level is below the required level.";
                foreach (var u in gaps.Take(10))
                    r.Answer.Add($"{u.User?.FullName ?? "Unknown"} — {u.Competency?.Name}: {u.Level} vs required {u.RequiredLevel}.");

                r.Recommendations.Add("Assign targeted training or mentoring to close the competency gaps.");
                r.NextActions.Add("Update the competency matrix after re-assessing the affected employees.");
                return r;
            }

            // ── Default — generic training catalogue ────────────────────────────────
            var courses = await _db.TrainingCourses
                .Include(t => t.Records)
                .ToListAsync();

            if (courses.Count == 0)
            {
                r.Summary = "No training courses are recorded.";
                r.NextActions.Add("Create the training courses required for your competency and awareness plan.");
                return r;
            }

            r.EvidenceUsed = courses.Take(10).Select(EvidenceGraphService.Training).ToList();
            r.RelatedTraining = r.EvidenceUsed;
            r.Summary = $"{courses.Count} training course(s) are on the register.";
            foreach (var c in courses.Take(8))
                r.Answer.Add($"{c.Reference} — {c.Title} ({c.Type}); {c.Records.Count} attendance record(s).");
            r.NextActions.Add("Review attendance and completion against your training plan.");
            return r;
        }
    }

    /// <summary>
    /// Advises on supplier performance and approval (ISO 9001 cl. 8.4). Grounds every statement in the latest
    /// stored evaluation for each supplier and highlights the poorest performers on request.
    /// </summary>
    public class SupplierAdvisorSpecialist : IEcieSpecialist
    {
        public string Name => "Supplier Advisor";
        public string Icon => "fa-truck-field";

        public int Score(EcieContext ctx) =>
            ctx.Mentions("supplier", "vendor", "supplier rating", "supplier performance") ? 40 : 0;

        public async Task<EcieResponse> HandleAsync(EcieContext ctx)
        {
            var r = new EcieResponse();
            var _db = ctx.Db;

            var suppliers = await _db.Suppliers
                .Include(s => s.Evaluations)
                .ToListAsync();

            if (suppliers.Count == 0)
            {
                r.Summary = "No suppliers are recorded.";
                r.NextActions.Add("Register your approved suppliers and record their first evaluation.");
                return r;
            }

            // latest evaluation + overall score (both [NotMapped] — evaluated in memory)
            var scored = suppliers.Select(s =>
            {
                var latest = s.Evaluations.OrderByDescending(e => e.EvaluationDate).FirstOrDefault();
                return new
                {
                    Supplier = s,
                    Latest = latest,
                    Score = latest?.OverallScore,
                    Rating = latest?.Rating
                };
            }).ToList();

            var wantWorst = ctx.Mentions("poorest", "worst", "lowest", "deteriorat");

            // suppliers with an evaluation first, ordered ascending by score for "worst"
            var ordered = wantWorst
                ? scored.OrderBy(x => x.Score.HasValue ? 0 : 1).ThenBy(x => x.Score ?? int.MaxValue).ToList()
                : scored.OrderByDescending(x => x.Score ?? -1).ToList();

            r.EvidenceUsed = ordered.Take(10)
                .Select(x => EvidenceGraphService.SupplierRef(x.Supplier, x.Score)).ToList();

            if (wantWorst)
            {
                var worst = ordered.FirstOrDefault(x => x.Score.HasValue);
                if (worst != null)
                    r.Summary = $"{worst.Supplier.Name} has the poorest rating at {worst.Score}/100 ({worst.Rating}).";
                else
                    r.Summary = $"{suppliers.Count} supplier(s) are recorded but none have been evaluated yet.";
            }
            else
            {
                var evaluated = scored.Count(x => x.Score.HasValue);
                r.Summary = $"{suppliers.Count} supplier(s) recorded; {evaluated} have at least one performance evaluation.";
            }

            foreach (var x in ordered.Take(8))
            {
                r.Answer.Add(x.Score.HasValue
                    ? $"{x.Supplier.Name} ({x.Supplier.Status}) — latest score {x.Score}/100 ({x.Rating}), evaluated {x.Latest!.EvaluationDate:dd MMM yyyy}."
                    : $"{x.Supplier.Name} ({x.Supplier.Status}) — no evaluation recorded yet.");
            }

            var poor = scored.Where(x => x.Score.HasValue && x.Score < 50).ToList();
            if (poor.Count > 0)
            {
                r.Recommendations.Add("Re-evaluate low-scoring suppliers and place them on a formal improvement plan.");
                r.NextActions.Add("Schedule a review meeting with each supplier scoring below 50.");
                r.PotentialRisks.AddRange(poor.Take(3)
                    .Select(x => $"{x.Supplier.Name} is scoring {x.Score}/100 ({x.Rating}) — supply-quality risk."));
            }
            r.PotentialRisks.AddRange(suppliers.Where(s => s.CertificateExpiringSoon).Take(3)
                .Select(s => $"{s.Name}'s certificate expires {s.CertificateExpiry:dd MMM yyyy} — request an updated certificate."));

            if (scored.Any(x => !x.Score.HasValue))
                r.Recommendations.Add("Complete an initial evaluation for suppliers that have never been assessed.");

            return r;
        }
    }
}
