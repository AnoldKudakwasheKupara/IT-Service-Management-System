using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.ViewModels.Ecie;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Ecie.Specialists
{
    /// <summary>
    /// The ISO/IEC 27001:2022 information-security advisor. Explains security clauses and surfaces the
    /// approved security documents and open information-security risks that ground the answer.
    /// </summary>
    public class SecurityAdvisorSpecialist : IEcieSpecialist
    {
        public string Name => "Security Advisor";
        public string Icon => "fa-lock";

        public int Score(EcieContext ctx)
        {
            var s = 0;
            if (ctx.Mentions("27001", "information security", "infosec", "security control",
                    "access control", "password", "encryption", "confidential", "security")) s += 40;
            if (ctx.Clause != null && s > 0) s += 10;
            return s;
        }

        public async Task<EcieResponse> HandleAsync(EcieContext ctx)
        {
            var r = new EcieResponse { Specialist = Name, SpecialistIcon = Icon };

            // ── Clause-scoped question ──────────────────────────────────────────────
            if (ctx.Clause != null)
            {
                var (clauses, docs, evidence, findings) = await ctx.Graph.ExpandClauseAsync(ctx.Role, ctx.Clause);
                // Prefer the ISO 27001 clause when the same number exists in more than one standard.
                var iso = clauses.Where(c => c.Standard == IsoStandard.Iso27001).ToList();
                var scoped = iso.Count > 0 ? iso : clauses;

                if (scoped.Count == 0 && docs.Count == 0 && evidence.Count == 0)
                {
                    r.Summary = $"No approved organisational evidence currently exists for ISO 27001 clause {ctx.Clause}.";
                    r.NextActions.Add($"Tag a published security document or upload evidence against clause {ctx.Clause}.");
                    return r;
                }

                foreach (var c in scoped) r.EvidenceUsed.Add(EvidenceGraphService.ClauseRef(c));
                r.RelatedDocuments = docs.Take(8).Select(EvidenceGraphService.Doc).ToList();
                r.EvidenceUsed.AddRange(r.RelatedDocuments);
                r.EvidenceUsed.AddRange(evidence.Take(8).Select(EvidenceGraphService.EvidenceItem));
                r.RelatedAudits = findings.Take(6).Select(EvidenceGraphService.Finding).ToList();

                var primary = scoped.FirstOrDefault();
                r.Summary = primary != null
                    ? $"{IsoStandards.Label(primary.Standard)} Clause {primary.ClauseNumber} — {primary.Title}."
                    : $"ISO 27001 clause {ctx.Clause}: {docs.Count} approved document(s) and {evidence.Count} evidence item(s) are linked.";
                if (!string.IsNullOrWhiteSpace(primary?.Description)) r.Answer.Add(primary!.Description!);
                r.Answer.Add($"{docs.Count} approved document(s) and {evidence.Count} evidence item(s) are tagged to this clause; {findings.Count} audit finding(s) reference it.");

                if (docs.Count == 0) r.Recommendations.Add($"No approved document is tagged to clause {ctx.Clause} — create or label one.");
                if (evidence.Count == 0) r.Recommendations.Add($"Upload objective evidence for clause {ctx.Clause} in the Evidence Repository.");
                r.NextActions.Add($"Assemble the security documents and evidence for clause {ctx.Clause} ahead of your ISO 27001 audit.");
                return r;
            }

            // ── General ISO 27001 posture ───────────────────────────────────────────
            var found = await ctx.Graph.SearchApprovedDocumentsAsync(ctx.Role, ctx.Tokens);
            var securityDocs = found
                .Where(d => d.Standard == IsoStandard.Iso27001 || d.Standard == IsoStandard.Both)
                .ToList();

            var openRisks = await ctx.Db.Risks
                .Include(x => x.Owner).Include(x => x.Department)
                .Where(x => x.Category == RiskCategory.InformationSecurity && x.Status != RiskStatus.Closed)
                .ToListAsync();
            openRisks = openRisks.OrderByDescending(x => x.Score).ToList();

            if (securityDocs.Count == 0 && openRisks.Count == 0)
            {
                r.Summary = "No approved organisational evidence currently exists for ISO 27001.";
                r.NextActions.Add("Publish your information-security policies and record information-security risks in the register.");
                return r;
            }

            r.EvidenceUsed.AddRange(securityDocs.Take(8).Select(EvidenceGraphService.Doc));
            r.EvidenceUsed.AddRange(openRisks.Take(6).Select(EvidenceGraphService.RiskRef));
            r.RelatedDocuments = securityDocs.Take(8).Select(EvidenceGraphService.Doc).ToList();
            r.RelatedRisks = openRisks.Take(8).Select(EvidenceGraphService.RiskRef).ToList();

            var crit = openRisks.Count(x => x.Band is RiskBand.Critical or RiskBand.High);
            r.Summary = $"ISO 27001: {securityDocs.Count} approved security document(s) and {openRisks.Count} open information-security risk(s).";

            foreach (var d in securityDocs.Take(6))
                r.Answer.Add($"{d.DocumentNumber} — {d.Title} (v{d.CurrentVersion}, {d.Status}).");
            foreach (var x in openRisks.Take(4))
                r.Answer.Add($"{x.Reference} — {x.Title} (score {x.Score}, {x.Band}; owner {x.Owner?.FullName ?? "unassigned"}).");

            r.Recommendations.Add("Review your Access Control policy and confirm it is current and acknowledged.");
            if (crit > 0)
            {
                r.Recommendations.Add("Prioritise treatment of critical/high information-security risks.");
                r.NextActions.Add("Assign owners and treatment plans to untreated critical information-security risks.");
            }
            r.NextActions.Add("Map your approved security documents to the ISO 27001 Annex A controls for the SoA.");
            r.PotentialRisks.AddRange(openRisks.Where(x => x.Band == RiskBand.Critical).Take(3)
                .Select(x => $"{x.Reference} ({x.Title}) is a critical information-security risk requiring treatment."));
            return r;
        }
    }

    /// <summary>
    /// The ISO 9001:2015 quality advisor. Explains quality clauses and surfaces the approved quality
    /// documents that ground the answer.
    /// </summary>
    public class QualityAdvisorSpecialist : IEcieSpecialist
    {
        public string Name => "Quality Advisor";
        public string Icon => "fa-award";

        public int Score(EcieContext ctx)
        {
            // A purely information-security query belongs to the Security Advisor, not here.
            var isSecurity = ctx.Mentions("27001", "iso27001", "information security", "infosec", "security control");
            if (isSecurity && !ctx.Mentions("9001", "iso9001", "quality", "qms")) return 0;

            var s = 0;
            if (ctx.Mentions("9001", "quality", "qms", "customer", "nonconformity quality", "process")) s += 40;
            if (ctx.Clause != null && s > 0) s += 10;
            return s;
        }

        public async Task<EcieResponse> HandleAsync(EcieContext ctx)
        {
            var r = new EcieResponse { Specialist = Name, SpecialistIcon = Icon };

            // ── Clause-scoped question ──────────────────────────────────────────────
            if (ctx.Clause != null)
            {
                var (clauses, docs, evidence, findings) = await ctx.Graph.ExpandClauseAsync(ctx.Role, ctx.Clause);
                // Prefer the ISO 9001 clause when the same number exists in more than one standard.
                var iso = clauses.Where(c => c.Standard == IsoStandard.Iso9001).ToList();
                var scoped = iso.Count > 0 ? iso : clauses;

                if (scoped.Count == 0 && docs.Count == 0 && evidence.Count == 0)
                {
                    r.Summary = $"No approved organisational evidence currently exists for ISO 9001 clause {ctx.Clause}.";
                    r.NextActions.Add($"Tag a published quality document or upload evidence against clause {ctx.Clause}.");
                    return r;
                }

                foreach (var c in scoped) r.EvidenceUsed.Add(EvidenceGraphService.ClauseRef(c));
                r.RelatedDocuments = docs.Take(8).Select(EvidenceGraphService.Doc).ToList();
                r.EvidenceUsed.AddRange(r.RelatedDocuments);
                r.EvidenceUsed.AddRange(evidence.Take(8).Select(EvidenceGraphService.EvidenceItem));
                r.RelatedAudits = findings.Take(6).Select(EvidenceGraphService.Finding).ToList();

                var primary = scoped.FirstOrDefault();
                r.Summary = primary != null
                    ? $"{IsoStandards.Label(primary.Standard)} Clause {primary.ClauseNumber} — {primary.Title}."
                    : $"ISO 9001 clause {ctx.Clause}: {docs.Count} approved document(s) and {evidence.Count} evidence item(s) are linked.";
                if (!string.IsNullOrWhiteSpace(primary?.Description)) r.Answer.Add(primary!.Description!);
                r.Answer.Add($"{docs.Count} approved document(s) and {evidence.Count} evidence item(s) are tagged to this clause; {findings.Count} audit finding(s) reference it.");

                if (docs.Count == 0) r.Recommendations.Add($"No approved document is tagged to clause {ctx.Clause} — create or label one.");
                if (evidence.Count == 0) r.Recommendations.Add($"Upload objective evidence for clause {ctx.Clause} in the Evidence Repository.");
                r.NextActions.Add($"Assemble the quality documents and evidence for clause {ctx.Clause} ahead of your ISO 9001 audit.");
                return r;
            }

            // ── General ISO 9001 posture ────────────────────────────────────────────
            var found = await ctx.Graph.SearchApprovedDocumentsAsync(ctx.Role, ctx.Tokens);
            var qualityDocs = found
                .Where(d => d.Standard == IsoStandard.Iso9001 || d.Standard == IsoStandard.Both)
                .ToList();

            if (qualityDocs.Count == 0)
            {
                r.Summary = "No approved organisational evidence currently exists for ISO 9001.";
                r.NextActions.Add("Publish your quality policies and procedures in Document Control.");
                return r;
            }

            r.EvidenceUsed = qualityDocs.Take(10).Select(EvidenceGraphService.Doc).ToList();
            r.RelatedDocuments = r.EvidenceUsed;
            r.RelatedPolicies = qualityDocs.Where(d => d.Type == DocumentType.Policy).Take(6).Select(EvidenceGraphService.Doc).ToList();
            r.RelatedProcedures = qualityDocs.Where(d => d.Type == DocumentType.Procedure).Take(6).Select(EvidenceGraphService.Doc).ToList();

            r.Summary = $"ISO 9001: {qualityDocs.Count} approved quality document(s).";
            foreach (var d in qualityDocs.Take(6))
                r.Answer.Add($"{d.DocumentNumber} — {d.Title} (v{d.CurrentVersion}, {d.Status}).");

            r.Recommendations.Add("Confirm your Quality Manual and quality policy reflect the current process scope.");
            if (qualityDocs.Any(d => d.IsReviewDue || d.IsExpired))
                r.Recommendations.Add("Schedule reviews for quality documents that are due or expired.");
            r.NextActions.Add("Map your approved quality documents to the ISO 9001 clauses ahead of your next audit.");
            r.PotentialRisks.AddRange(qualityDocs.Where(d => d.IsExpired).Take(3)
                .Select(d => $"{d.DocumentNumber} is past its expiry date and may be obsolete."));
            return r;
        }
    }
}
