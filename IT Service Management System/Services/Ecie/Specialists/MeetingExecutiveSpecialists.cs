using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.ViewModels.Ecie;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Ecie.Specialists
{
    /// <summary>Summarises management reviews, tracks actions and suggests grounded agendas (ISO cl. 9.3).</summary>
    public class MeetingAdvisorSpecialist : IEcieSpecialist
    {
        public string Name => "Meeting Advisor";
        public string Icon => "fa-people-group";

        public int Score(EcieContext ctx) =>
            ctx.Mentions("meeting", "management review", "minutes", "agenda", "action item", "review meeting") ? 40 : 0;

        public async Task<EcieResponse> HandleAsync(EcieContext ctx)
        {
            var r = new EcieResponse();
            var today = DateTime.Now.Date;

            // Overdue actions
            if (ctx.Mentions("overdue action", "outstanding action", "overdue actions"))
            {
                var actions = await ctx.Db.ManagementReviewActions
                    .Include(a => a.ManagementReview).Include(a => a.AssignedTo).ToListAsync();
                var overdue = actions.Where(a => a.IsOverdue).ToList();
                if (overdue.Count == 0) { r.Summary = "No management-review actions are currently overdue."; return r; }

                foreach (var a in overdue)
                    r.EvidenceUsed.Add(new EvidenceRef
                    {
                        Kind = "Action", Reference = a.Reference, Title = a.Description,
                        Subtitle = $"due {a.DueDate:dd MMM yyyy} · {a.AssignedTo?.FullName ?? "unassigned"}",
                        Icon = "fa-list-check", Url = $"/ManagementReviews/Details/{a.ManagementReviewId}"
                    });
                r.Summary = $"{overdue.Count} management-review action(s) are overdue.";
                r.NextActions.Add("Follow up with the owners of the overdue actions.");
                return r;
            }

            // Suggested agenda (grounded in live data)
            if (ctx.Mentions("agenda", "next", "prepare"))
            {
                var prior = await ctx.Db.ManagementReviews.OrderByDescending(m => m.MeetingDate).FirstOrDefaultAsync();
                var openActions = await ctx.Db.ManagementReviewActions.CountAsync(a => a.Status != ReviewActionStatus.Completed && a.Status != ReviewActionStatus.Cancelled);
                var openFindings = await ctx.Db.AuditFindings.CountAsync(f => f.Status != FindingStatus.Closed);
                var overdueCapa = await ctx.Db.Capas.CountAsync(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified && c.DueDate != null && c.DueDate < today);
                var risks = await ctx.Db.Risks.Where(x => x.Status != RiskStatus.Closed).Select(x => new { x.Likelihood, x.Impact }).ToListAsync();
                var critical = risks.Count(x => RiskScoring.BandFor(x.Likelihood * x.Impact) == RiskBand.Critical);
                var docsDue = await ctx.Db.IsoDocuments.CountAsync(d => d.Status == DocumentStatus.Published && d.ReviewDate != null && d.ReviewDate <= today);

                r.Summary = "Suggested management-review agenda (ISO 9.3), annotated with live data:";
                r.Answer.Add($"1. Status of actions from previous reviews — {openActions} open.");
                r.Answer.Add("2. Changes in external and internal issues relevant to the management system.");
                r.Answer.Add($"3. Internal audit results — {openFindings} open finding(s).");
                r.Answer.Add($"4. Nonconformities & corrective actions — {overdueCapa} CAPA(s) overdue.");
                r.Answer.Add($"5. Risks & opportunities — {critical} critical risk(s).");
                r.Answer.Add($"6. Adequacy of documented information — {docsDue} document(s) due for review.");
                r.Answer.Add("7. Objectives/KPIs, training, supplier performance and customer feedback.");
                r.Answer.Add("8. Opportunities for improvement and resource needs.");

                if (prior != null) r.EvidenceUsed.Add(EvidenceGraphService.Meeting(prior));
                foreach (var c in await ctx.Db.Capas.Where(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified).Take(3).ToListAsync())
                    r.EvidenceUsed.Add(EvidenceGraphService.Capa(c));
                r.NextActions.Add("Schedule the review and circulate the management-review pack.");
                return r;
            }

            // Summarise the latest review
            var review = await ctx.Db.ManagementReviews
                .Include(m => m.Attendees).Include(m => m.Inputs).Include(m => m.Actions)
                .OrderByDescending(m => m.MeetingDate).FirstOrDefaultAsync();
            if (review == null) { r.Summary = "No management reviews are recorded."; return r; }

            r.EvidenceUsed.Add(EvidenceGraphService.Meeting(review));
            r.RelatedMeetings.Add(EvidenceGraphService.Meeting(review));
            var openA = review.Actions.Count(a => a.Status != ReviewActionStatus.Completed && a.Status != ReviewActionStatus.Cancelled);
            r.Summary = $"Management review {review.Reference} on {review.MeetingDate:dd MMM yyyy}: {review.Inputs.Count} input(s) reviewed, {review.Actions.Count} action(s) ({openA} open).";
            r.Answer.Add($"Attendees: {review.Attendees.Count}.");
            foreach (var i in review.Inputs.OrderBy(i => i.Sequence)) r.Answer.Add($"{i.Category}: {i.Summary}");
            if (!string.IsNullOrWhiteSpace(review.Decisions)) r.Answer.Add($"Decisions: {review.Decisions}");
            if (!string.IsNullOrWhiteSpace(review.Conclusions)) r.Answer.Add($"Conclusions: {review.Conclusions}");
            if (openA > 0) r.NextActions.Add("Progress the open actions arising from this review.");
            return r;
        }
    }

    /// <summary>Produces grounded management summaries and audit-readiness verdicts for leadership.</summary>
    public class ExecutiveAdvisorSpecialist : IEcieSpecialist
    {
        public string Name => "Executive Advisor";
        public string Icon => "fa-chart-line";

        public int Score(EcieContext ctx) =>
            ctx.Mentions("executive", "summary", "overview", "surveillance", "audit readiness", "prepare",
                "board", "management summary", "how compliant", "compliance status") ? 35 : 0;

        public async Task<EcieResponse> HandleAsync(EcieContext ctx)
        {
            var r = new EcieResponse();
            var today = DateTime.Now.Date;

            var published = await ctx.Db.IsoDocuments.CountAsync(d => d.Status == DocumentStatus.Published);
            var docsDue = await ctx.Db.IsoDocuments.CountAsync(d => d.Status == DocumentStatus.Published &&
                ((d.ReviewDate != null && d.ReviewDate <= today) || (d.ExpiryDate != null && d.ExpiryDate < today)));
            var openCapas = await ctx.Db.Capas.CountAsync(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified);
            var overdueCapa = await ctx.Db.Capas.CountAsync(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified && c.DueDate != null && c.DueDate < today);
            var openFindings = await ctx.Db.AuditFindings.CountAsync(f => f.Status != FindingStatus.Closed);
            var risks = await ctx.Db.Risks.Where(x => x.Status != RiskStatus.Closed).Select(x => new { x.Likelihood, x.Impact }).ToListAsync();
            var critical = risks.Count(x => RiskScoring.BandFor(x.Likelihood * x.Impact) == RiskBand.Critical);
            var ackTotal = await ctx.Db.IsoDocumentAcknowledgements.CountAsync();
            var ackDone = await ctx.Db.IsoDocumentAcknowledgements.CountAsync(a => a.Status == AcknowledgementStatus.Acknowledged);
            var reviewsRecently = await ctx.Db.ManagementReviews.CountAsync(m =>
                (m.Status == ReviewMeetingStatus.Held || m.Status == ReviewMeetingStatus.Closed) && m.MeetingDate >= today.AddYears(-1));

            if (published == 0 && openCapas == 0 && openFindings == 0 && risks.Count == 0)
            { r.Summary = EcieResponse.NoEvidence; return r; }

            r.Answer.Add($"Documents: {published} published; {docsDue} due for review or expired.");
            r.Answer.Add($"CAPAs: {openCapas} open ({overdueCapa} overdue).");
            r.Answer.Add($"Audit findings: {openFindings} open.");
            r.Answer.Add($"Risks: {risks.Count} open ({critical} critical).");
            r.Answer.Add($"Acknowledgements: {ackDone}/{ackTotal} complete.");
            r.Answer.Add($"Management review: {(reviewsRecently > 0 ? "held within the last 12 months" : "none in the last 12 months")}.");

            foreach (var c in await ctx.Db.Capas.Include(c => c.Responsible)
                .Where(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified)
                .OrderBy(c => c.DueDate).Take(3).ToListAsync())
                r.EvidenceUsed.Add(EvidenceGraphService.Capa(c));
            foreach (var f in await ctx.Db.AuditFindings.Include(f => f.Audit)
                .Where(f => f.Status != FindingStatus.Closed).Take(3).ToListAsync())
                r.EvidenceUsed.Add(EvidenceGraphService.Finding(f));
            var critRisks = (await ctx.Db.Risks.Include(x => x.Owner).Where(x => x.Status != RiskStatus.Closed).ToListAsync())
                .Where(x => x.Band == RiskBand.Critical).Take(3).ToList();
            foreach (var x in critRisks) r.EvidenceUsed.Add(EvidenceGraphService.RiskRef(x));

            var issues = new List<string>();
            if (overdueCapa > 0) issues.Add($"{overdueCapa} overdue CAPA(s)");
            if (openFindings > 0) issues.Add($"{openFindings} open finding(s)");
            if (docsDue > 0) issues.Add($"{docsDue} document(s) past review");
            if (critical > 0) issues.Add($"{critical} critical risk(s)");
            r.Summary = issues.Count == 0
                ? "Audit readiness is strong — no overdue CAPAs, open findings, critical risks or overdue documents."
                : "Audit readiness: " + string.Join(", ", issues) + " need attention before the surveillance audit.";

            if (overdueCapa > 0) r.NextActions.Add("Close or re-plan the overdue CAPAs.");
            if (docsDue > 0) r.NextActions.Add("Review and re-approve documents past their review date.");
            if (critical > 0) r.NextActions.Add("Ensure treatment plans exist for every critical risk.");
            if (reviewsRecently == 0) r.NextActions.Add("Hold a management review — none recorded in the last 12 months.");
            r.Recommendations.Add("Assemble the evidence pack per ISO clause ahead of the audit.");
            return r;
        }
    }
}
