using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.ViewModels.Ecie;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Ecie.Specialists
{
    /// <summary>Advises on Corrective &amp; Preventive Actions and non-conformities — open, overdue and by department.</summary>
    public class CapaAdvisorSpecialist : IEcieSpecialist
    {
        public string Name => "CAPA Advisor";
        public string Icon => "fa-screwdriver-wrench";

        public int Score(EcieContext ctx)
        {
            var s = 0;
            if (ctx.Mentions("capa", "corrective", "preventive", "non-conformity", "nonconformity",
                    "non conformance", "nonconformance")) s += 40;
            if (ctx.Mentions("overdue", "open", "assigned")) s += 5;
            return s;
        }

        public async Task<EcieResponse> HandleAsync(EcieContext ctx)
        {
            var r = new EcieResponse();

            // Non-conformity questions read from the Non-Conformance register instead of CAPAs.
            if (ctx.Mentions("non-conformity", "nonconformity", "unresolved non"))
            {
                var ncQuery = ctx.Db.NonConformances.Include(n => n.Department).AsQueryable();
                if (ctx.Department != null) ncQuery = ncQuery.Where(n => n.DepartmentId == ctx.Department.Id);
                if (ctx.Mentions("unresolved", "open")) ncQuery = ncQuery.Where(n => n.Status != NcStatus.Closed);
                var ncs = (await ncQuery.ToListAsync()).OrderByDescending(n => n.Severity).ToList();

                if (ncs.Count == 0)
                {
                    r.Summary = ctx.Department != null
                        ? $"No unresolved non-conformities are recorded for {ctx.Department.Name}."
                        : "No unresolved non-conformities are recorded.";
                    r.NextActions.Add("Raise a non-conformance record when a deviation is detected.");
                    return r;
                }

                r.EvidenceUsed = ncs.Take(10).Select(n => new EvidenceRef
                {
                    Kind = "Non-Conformance",
                    Reference = n.Reference,
                    Title = n.Title,
                    Subtitle = $"{n.Status} · {n.Severity}",
                    Url = $"/NonConformances/Details/{n.Id}",
                    Icon = "fa-bug",
                    Badge = n.Severity.ToString()
                }).ToList();

                var ncCritical = ncs.Count(n => n.Severity == NcSeverity.Critical);
                var ncMajor = ncs.Count(n => n.Severity == NcSeverity.Major);
                var ncScope = ctx.Department != null ? $" in {ctx.Department.Name}" : "";
                r.Summary = $"{ncs.Count} unresolved non-conformit{(ncs.Count == 1 ? "y" : "ies")}{ncScope}, "
                          + $"including {ncMajor} major and {ncCritical} critical.";
                foreach (var n in ncs.Take(6))
                    r.Answer.Add($"{n.Reference} — {n.Title} ({n.Severity}, {n.Status}; detected {n.DetectedDate:dd MMM yyyy}).");

                if (ncCritical + ncMajor > 0)
                {
                    r.Recommendations.Add("Raise a CAPA against each major or critical non-conformity to drive it to closure.");
                    r.NextActions.Add("Assign an owner and containment action to every unresolved non-conformity.");
                }
                r.PotentialRisks.AddRange(ncs.Where(n => n.Severity == NcSeverity.Critical).Take(3)
                    .Select(n => $"{n.Reference} ({n.Title}) is a critical non-conformity that remains unresolved."));
                return r;
            }

            // Default: the CAPA register.
            var query = ctx.Db.Capas.Include(c => c.Responsible).Include(c => c.Department).AsQueryable();
            if (ctx.Department != null) query = query.Where(c => c.DepartmentId == ctx.Department.Id);
            var capas = await query.ToListAsync();

            if (ctx.Mentions("overdue")) capas = capas.Where(c => c.IsOverdue).ToList();
            if (ctx.Mentions("open")) capas = capas.Where(c => !c.IsClosed).ToList();
            capas = capas.OrderByDescending(c => c.IsOverdue).ThenBy(c => c.DueDate).ToList();

            if (capas.Count == 0)
            {
                r.Summary = ctx.Department != null
                    ? $"No CAPAs match your criteria for {ctx.Department.Name}."
                    : "No CAPAs match your criteria.";
                r.NextActions.Add("Raise a CAPA from an audit finding, non-conformance or incident when action is required.");
                return r;
            }

            r.EvidenceUsed = capas.Take(10).Select(EvidenceGraphService.Capa).ToList();
            r.RelatedCapas = r.EvidenceUsed;

            var overdue = capas.Count(c => c.IsOverdue);
            var open = capas.Count(c => !c.IsClosed);
            var preventive = capas.Count(c => c.Type == CapaType.Preventive);
            var scope = ctx.Department != null ? $" assigned to {ctx.Department.Name}" : "";
            r.Summary = $"{open} open CAPA{(open == 1 ? "" : "s")}{scope}, {overdue} overdue"
                      + (preventive > 0 ? $"; {preventive} preventive." : ".");

            foreach (var c in capas.Take(6))
                r.Answer.Add($"{c.Reference} — {c.Title} ({c.Type}, {c.Status}; owner {c.Responsible?.FullName ?? "unassigned"}"
                           + (c.DueDate.HasValue ? $", due {c.DueDate:dd MMM yyyy}" : "")
                           + (c.IsOverdue ? " — OVERDUE" : "") + ").");

            if (overdue > 0)
            {
                r.Recommendations.Add("Escalate the overdue CAPAs and confirm revised due dates with their owners.");
                r.NextActions.Add("Review each overdue CAPA and progress it toward verification.");
            }
            foreach (var c in capas.Where(c => !c.IsClosed && c.Responsible == null).Take(3))
                r.NextActions.Add($"Assign a responsible person to {c.Reference} ({c.Title}).");
            foreach (var c in capas.Where(c => c.Status == CapaStatus.PendingVerification).Take(3))
                r.NextActions.Add($"Verify effectiveness of {c.Reference} ({c.Title}) and close it out.");

            r.PotentialRisks.AddRange(capas.Where(c => c.IsOverdue).Take(3)
                .Select(c => $"{c.Reference} ({c.Title}) is overdue and may indicate an unresolved non-conformity."));
            return r;
        }
    }

    /// <summary>The internal auditor — assembles clause evidence and surfaces open audit findings.</summary>
    public class InternalAuditorSpecialist : IEcieSpecialist
    {
        public string Name => "Internal Auditor";
        public string Icon => "fa-user-secret";

        public int Score(EcieContext ctx)
        {
            var s = 0;
            if (ctx.Mentions("audit finding", "findings", "evidence for", "unresolved",
                    "non-conformit", "gap", "surveillance")) s += 38;
            return s;
        }

        public async Task<EcieResponse> HandleAsync(EcieContext ctx)
        {
            var r = new EcieResponse();

            // "Generate evidence for clause X" — assemble the clause's documents, evidence and findings.
            if (ctx.Clause != null)
            {
                var (clauses, docs, evidence, findings) = await ctx.Graph.ExpandClauseAsync(ctx.Role, ctx.Clause);
                if (docs.Count == 0 && evidence.Count == 0 && findings.Count == 0)
                {
                    r.Summary = $"No approved organisational evidence currently exists for clause {ctx.Clause}.";
                    r.NextActions.Add($"Tag a published document or upload objective evidence against clause {ctx.Clause}.");
                    return r;
                }

                r.RelatedDocuments = docs.Take(8).Select(EvidenceGraphService.Doc).ToList();
                r.EvidenceUsed.AddRange(r.RelatedDocuments);
                r.EvidenceUsed.AddRange(evidence.Take(8).Select(EvidenceGraphService.EvidenceItem));
                r.EvidenceUsed.AddRange(findings.Take(8).Select(EvidenceGraphService.Finding));
                r.RelatedAudits = findings.Take(6).Select(EvidenceGraphService.Finding).ToList();

                var openFindings = findings.Count(f => f.Status != FindingStatus.Closed);
                r.Summary = $"Clause {ctx.Clause}: {docs.Count} approved document(s), {evidence.Count} evidence item(s) "
                          + $"and {findings.Count} audit finding(s) ({openFindings} still open).";
                r.Answer.Add($"{docs.Count} approved document(s) and {evidence.Count} objective evidence item(s) are tagged to clause {ctx.Clause}.");
                if (findings.Count > 0)
                    r.Answer.Add($"{findings.Count} audit finding(s) reference this clause, of which {openFindings} remain open.");

                if (docs.Count == 0) r.Recommendations.Add($"No approved document is tagged to clause {ctx.Clause} — create or label one.");
                if (evidence.Count == 0) r.Recommendations.Add($"Upload objective evidence for clause {ctx.Clause} to demonstrate conformity.");
                if (openFindings > 0) r.Recommendations.Add($"Close the {openFindings} open finding(s) against clause {ctx.Clause} before the surveillance audit.");
                r.NextActions.Add($"Assemble the documents, evidence and closed findings for clause {ctx.Clause} into the audit pack.");
                return r;
            }

            // Otherwise: list open audit findings as the evidence base.
            var query = ctx.Db.AuditFindings.Include(f => f.Audit).Include(f => f.Department)
                .Where(f => f.Status != FindingStatus.Closed);
            if (ctx.Department != null) query = query.Where(f => f.DepartmentId == ctx.Department.Id);
            var open = await query.ToListAsync();
            open = open.OrderByDescending(f => f.Type).ThenBy(f => f.DueDate).ToList();

            if (open.Count == 0)
            {
                r.Summary = ctx.Department != null
                    ? $"No open audit findings are recorded for {ctx.Department.Name}."
                    : "No open audit findings are recorded.";
                r.NextActions.Add("Record findings against an audit as evidence is gathered.");
                return r;
            }

            r.EvidenceUsed = open.Take(10).Select(EvidenceGraphService.Finding).ToList();
            r.RelatedAudits = r.EvidenceUsed;

            var nonConformities = open.Count(f => f.IsNonConformance);
            var overdue = open.Count(f => f.IsOverdue);
            var scope = ctx.Department != null ? $" in {ctx.Department.Name}" : "";
            r.Summary = $"{open.Count} open finding{(open.Count == 1 ? "" : "s")}{scope}; "
                      + $"{nonConformities} {(nonConformities == 1 ? "is a non-conformity" : "are non-conformities")}.";

            foreach (var f in open.Take(6))
                r.Answer.Add($"{f.Reference} — {f.Description.Split('.').FirstOrDefault()?.Trim()} "
                           + $"({f.Type}, {f.Status}{(f.ClauseReference != null ? $", clause {f.ClauseReference}" : "")}"
                           + $"; audit {f.Audit?.Reference ?? "n/a"}{(f.IsOverdue ? ", OVERDUE" : "")}).");

            if (nonConformities > 0)
            {
                r.Recommendations.Add("Raise a CAPA for each open non-conformity and drive it to verified closure.");
                r.NextActions.Add("Confirm root cause and corrective action for every open non-conformity finding.");
            }
            if (overdue > 0)
                r.NextActions.Add($"Escalate the {overdue} overdue finding(s) that have passed their due date.");
            r.PotentialRisks.AddRange(open.Where(f => f.Type == FindingType.MajorNonConformance).Take(3)
                .Select(f => $"{f.Reference} is a major non-conformity that could affect certification if left open."));
            return r;
        }
    }

    /// <summary>Root-cause analyst — detects recurring finding patterns by clause and department hot-spots.</summary>
    public class RootCauseAnalystSpecialist : IEcieSpecialist
    {
        public string Name => "Root Cause Analyst";
        public string Icon => "fa-diagram-project";

        public int Score(EcieContext ctx) =>
            ctx.Mentions("recurring", "root cause", "pattern", "repeat", "trend", "recurrence") ? 42 : 0;

        public async Task<EcieResponse> HandleAsync(EcieContext ctx)
        {
            var r = new EcieResponse();

            var findings = await ctx.Db.AuditFindings.Include(f => f.Audit).Include(f => f.Department).ToListAsync();
            if (ctx.Department != null) findings = findings.Where(f => f.DepartmentId == ctx.Department.Id).ToList();

            // Recurring clauses: any clause with two or more findings.
            var clauseGroups = findings
                .Where(f => !string.IsNullOrWhiteSpace(f.ClauseReference))
                .GroupBy(f => f.ClauseReference!.Trim())
                .Select(g => new
                {
                    Clause = g.Key,
                    Count = g.Count(),
                    Audits = g.Select(f => f.AuditId).Distinct().Count(),
                    Items = g.ToList()
                })
                .Where(g => g.Count >= 2)
                .OrderByDescending(g => g.Count)
                .ToList();

            if (clauseGroups.Count == 0)
            {
                r.Summary = "No recurring finding patterns detected in the current audit data.";
                r.Answer.Add($"{findings.Count} finding(s) reviewed; none share a clause reference more than once.");
                r.NextActions.Add("Continue monitoring findings for emerging clause or department trends.");
                return r;
            }

            var top = clauseGroups.First();
            r.EvidenceUsed = top.Items.Take(10).Select(EvidenceGraphService.Finding).ToList();
            r.RelatedAudits = r.EvidenceUsed;

            var scope = ctx.Department != null ? $" in {ctx.Department.Name}" : "";
            r.Summary = $"Clause {top.Clause} shows a recurring pattern{scope}: {top.Count} findings across {top.Audits} audit(s).";

            foreach (var g in clauseGroups.Take(6))
                r.Answer.Add($"Clause {g.Clause}: {g.Count} findings across {g.Audits} audit(s) — recurring non-conformity pattern.");

            // Department hot-spots.
            var deptGroups = findings
                .Where(f => f.DepartmentId.HasValue)
                .GroupBy(f => f.Department)
                .Select(g => new { Dept = g.Key, Count = g.Count() })
                .Where(g => g.Count >= 2)
                .OrderByDescending(g => g.Count)
                .ToList();
            foreach (var g in deptGroups.Take(3))
                r.Answer.Add($"{g.Dept?.Name ?? "Unassigned"} is a hot-spot with {g.Count} findings.");

            r.Recommendations.Add($"Raise a systemic CAPA for clause {top.Clause} rather than treating each finding in isolation.");
            r.Recommendations.Add("Add the recurring pattern to the next Management Review agenda for effectiveness review.");
            r.NextActions.Add($"Investigate a common root cause behind the {top.Count} findings against clause {top.Clause}.");
            if (deptGroups.Count > 0)
                r.NextActions.Add($"Focus corrective effort on {deptGroups.First().Dept?.Name ?? "the leading department"} as the recurring hot-spot.");

            r.PotentialRisks.Add($"Repeated findings on clause {top.Clause} suggest an ineffective corrective action and risk an audit escalation.");
            return r;
        }
    }
}
