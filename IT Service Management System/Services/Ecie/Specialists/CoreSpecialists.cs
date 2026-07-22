using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.ViewModels.Ecie;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Ecie.Specialists
{
    /// <summary>Searches the approved document library and answers "show me X", "which policies belong to Y", "overdue policies".</summary>
    public class DocumentExpertSpecialist : IEcieSpecialist
    {
        public string Name => "Document Expert";
        public string Icon => "fa-file-shield";

        public int Score(EcieContext ctx)
        {
            var s = 1; // grounded fallback for general questions
            if (ctx.Mentions("policy", "policies", "procedure", "procedures", "document", "documents",
                    "work instruction", "form", "record", "library", "manual", "guideline")) s += 40;
            if (ctx.Mentions("show me", "find", "search", "belong")) s += 5;
            return s;
        }

        public async Task<EcieResponse> HandleAsync(EcieContext ctx)
        {
            var r = new EcieResponse();
            DocumentType? type = ctx.Mentions("procedure") ? DocumentType.Procedure
                : ctx.Mentions("work instruction") ? DocumentType.WorkInstruction
                : ctx.Mentions("form") ? DocumentType.Form
                : ctx.Mentions("record") ? DocumentType.Record
                : ctx.Mentions("policy", "policies") ? DocumentType.Policy
                : null;
            var overdue = ctx.Mentions("overdue", "due for review", "expiring", "expired", "pending review", "review");

            var docs = await ctx.Graph.SearchApprovedDocumentsAsync(ctx.Role, ctx.Tokens, ctx.Department?.Id, type);
            if (overdue && ctx.Mentions("overdue", "expired", "expiring", "due for review", "pending review"))
                docs = docs.Where(d => d.IsReviewDue || d.IsExpired).ToList();

            if (docs.Count == 0)
            {
                r.Summary = EcieResponse.NoEvidence;
                r.NextActions.Add("Create or publish the relevant controlled document in Document Control.");
                return r;
            }

            r.EvidenceUsed = docs.Take(10).Select(EvidenceGraphService.Doc).ToList();
            r.RelatedDocuments = r.EvidenceUsed;
            r.RelatedPolicies = docs.Where(d => d.Type == DocumentType.Policy).Take(6).Select(EvidenceGraphService.Doc).ToList();
            r.RelatedProcedures = docs.Where(d => d.Type == DocumentType.Procedure).Take(6).Select(EvidenceGraphService.Doc).ToList();

            var top = docs.First();
            r.Summary = type == DocumentType.Policy && ctx.Department != null
                ? $"{ctx.Department.Name} has {docs.Count} approved policy/policies."
                : docs.Any(d => d.IsReviewDue || d.IsExpired) && ctx.Mentions("overdue", "expired", "due for review")
                    ? $"{docs.Count(d => d.IsReviewDue || d.IsExpired)} approved document(s) are due for review or expired."
                    : $"Found {docs.Count} approved document(s). Closest match: \"{top.Title}\" ({top.DocumentNumber}), version {top.CurrentVersion}.";

            foreach (var d in docs.Take(6))
                r.Answer.Add($"{d.DocumentNumber} — {d.Title} (v{d.CurrentVersion}, {d.Status}{(d.ReviewDate.HasValue ? $", review {d.ReviewDate:dd MMM yyyy}" : "")}).");

            if (docs.Any(d => d.IsReviewDue || d.IsExpired))
            {
                r.Recommendations.Add("Schedule reviews for documents that are due or expired.");
                r.NextActions.Add("Open each flagged document and record a review outcome.");
            }
            r.PotentialRisks.AddRange(docs.Where(d => d.IsExpired).Take(3)
                .Select(d => $"{d.DocumentNumber} is past its expiry date and may be obsolete."));
            return r;
        }
    }

    /// <summary>Explains ISO clauses and generates the evidence tagged to a clause.</summary>
    public class IsoConsultantSpecialist : IEcieSpecialist
    {
        public string Name => "ISO Consultant";
        public string Icon => "fa-certificate";

        public int Score(EcieContext ctx)
        {
            var s = 0;
            if (ctx.Clause != null) s += 45;
            if (ctx.Mentions("clause", "iso 9001", "iso 27001", "iso9001", "iso27001", "standard", "requirement", "require", "shall")) s += 18;
            return s;
        }

        public async Task<EcieResponse> HandleAsync(EcieContext ctx)
        {
            var r = new EcieResponse();
            if (ctx.Clause == null)
            {
                r.Summary = "Please specify an ISO clause number (for example 7.5, 8.5 or 6.1).";
                return r;
            }

            var (clauses, docs, evidence, findings) = await ctx.Graph.ExpandClauseAsync(ctx.Role, ctx.Clause);
            if (clauses.Count == 0 && docs.Count == 0 && evidence.Count == 0)
            {
                r.Summary = $"No approved organisational evidence currently exists for clause {ctx.Clause}.";
                r.NextActions.Add($"Tag a published document or upload evidence against clause {ctx.Clause}.");
                return r;
            }

            foreach (var c in clauses) r.EvidenceUsed.Add(EvidenceGraphService.ClauseRef(c));
            r.RelatedDocuments = docs.Take(8).Select(EvidenceGraphService.Doc).ToList();
            r.EvidenceUsed.AddRange(r.RelatedDocuments);
            r.EvidenceUsed.AddRange(evidence.Take(8).Select(EvidenceGraphService.EvidenceItem));
            r.RelatedAudits = findings.Take(6).Select(EvidenceGraphService.Finding).ToList();

            var primary = clauses.FirstOrDefault();
            r.Summary = primary != null
                ? $"{IsoStandards.Label(primary.Standard)} Clause {primary.ClauseNumber} — {primary.Title}."
                : $"Clause {ctx.Clause}: {docs.Count} approved document(s) and {evidence.Count} evidence item(s) are linked.";
            if (!string.IsNullOrWhiteSpace(primary?.Description)) r.Answer.Add(primary!.Description!);
            r.Answer.Add($"{docs.Count} approved document(s) and {evidence.Count} evidence item(s) are tagged to this clause; {findings.Count} audit finding(s) reference it.");

            if (docs.Count == 0) r.Recommendations.Add($"No approved document is tagged to clause {ctx.Clause} — create or label one.");
            if (evidence.Count == 0) r.Recommendations.Add($"Upload objective evidence for clause {ctx.Clause} in the Evidence Repository.");
            r.NextActions.Add($"Assemble the documents and evidence for clause {ctx.Clause} ahead of your audit.");
            return r;
        }
    }

    /// <summary>Analyses the risk register — by department, criticality and treatment.</summary>
    public class RiskAdvisorSpecialist : IEcieSpecialist
    {
        public string Name => "Risk Advisor";
        public string Icon => "fa-gauge-high";

        public int Score(EcieContext ctx) => ctx.Mentions("risk", "risks", "threat", "vulnerability", "heat map") ? 40 : 0;

        public async Task<EcieResponse> HandleAsync(EcieContext ctx)
        {
            var r = new EcieResponse();
            var query = ctx.Db.Risks.Include(x => x.Owner).Include(x => x.Department).Where(x => x.Status != RiskStatus.Closed);
            if (ctx.Department != null) query = query.Where(x => x.DepartmentId == ctx.Department.Id);
            var risks = await query.ToListAsync();

            if (ctx.Mentions("critical", "high", "severe"))
                risks = risks.Where(x => x.Band is RiskBand.Critical or RiskBand.High).ToList();
            risks = risks.OrderByDescending(x => x.Score).ToList();

            if (risks.Count == 0)
            {
                r.Summary = ctx.Department != null ? $"No open risks are recorded for {ctx.Department.Name}." : "No open risks are recorded.";
                return r;
            }

            r.EvidenceUsed = risks.Take(10).Select(EvidenceGraphService.RiskRef).ToList();
            r.RelatedRisks = r.EvidenceUsed;
            var scope = ctx.Department != null ? $" affecting {ctx.Department.Name}" : "";
            var crit = risks.Count(x => x.Band == RiskBand.Critical);
            r.Summary = $"{risks.Count} open risk(s){scope}, including {crit} critical.";
            foreach (var x in risks.Take(6))
                r.Answer.Add($"{x.Reference} — {x.Title} (L{x.Likelihood}×I{x.Impact} = {x.Score}, {x.Band}; owner {x.Owner?.FullName ?? "unassigned"}).");

            if (crit > 0)
            {
                r.Recommendations.Add("Prioritise treatment plans for the critical risks.");
                r.NextActions.Add("Assign owners and review dates to untreated critical risks.");
            }
            r.PotentialRisks.AddRange(risks.Where(x => x.Band == RiskBand.Critical).Take(3)
                .Select(x => $"{x.Reference} ({x.Title}) is a critical risk requiring treatment."));
            return r;
        }
    }
}
