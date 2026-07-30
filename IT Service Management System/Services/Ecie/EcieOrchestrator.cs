using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.ViewModels.Ecie;

namespace IT_Service_Management_System.Services.Ecie
{
    /// <summary>
    /// The Compliance Intelligence Engine's router. It resolves query context once (tokens, clause,
    /// department), routes to the best-matching specialist, and enforces the grounding contract:
    /// an answer with no evidence is replaced by the standard "no approved evidence" statement.
    /// </summary>
    public class EcieOrchestrator
    {
        private readonly ApplicationDbContext _db;
        private readonly EvidenceGraphService _graph;
        private readonly IAiProvider _ai;
        private readonly IEnumerable<IEcieSpecialist> _specialists;

        public EcieOrchestrator(ApplicationDbContext db, EvidenceGraphService graph, IAiProvider ai, IEnumerable<IEcieSpecialist> specialists)
        {
            _db = db;
            _graph = graph;
            _ai = ai;
            _specialists = specialists;
        }

        public static readonly string[] SampleQuestions =
        {
            "What does Clause 7.5 require?",
            "Which policies belong to HR?",
            "Which policies are overdue?",
            "List all CAPAs assigned to Finance",
            "Show risks affecting Finance",
            "Which supplier has the poorest rating?",
            "Show employees who haven't acknowledged the Information Security Policy",
            "Which training courses expire this month?",
            "Summarise our last Management Review",
            "Find recurring audit findings",
            "Show unresolved Non-Conformities",
            "Prepare for our ISO Surveillance Audit"
        };

        public async Task<EcieResponse> AnswerAsync(string query, string? role, int? userId)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new EcieResponse { Summary = "Please ask a question.", Suggestions = SampleQuestions.ToList() };

            var ctx = new EcieContext
            {
                Query = query.Trim(),
                Lower = query.ToLowerInvariant(),
                Tokens = EvidenceGraphService.Tokenize(query),
                Role = role,
                UserId = userId,
                Clause = EvidenceGraphService.ExtractClause(query),
                Db = _db,
                Graph = _graph,
                Ai = _ai
            };
            ctx.Department = await _graph.ResolveDepartmentAsync(query);

            // Route to the best specialist (deterministic; ties broken by declaration order).
            var best = _specialists
                .Select(s => (Specialist: s, Score: s.Score(ctx)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            EcieResponse response;
            if (best.Specialist == null)
                response = new EcieResponse
                {
                    Query = ctx.Query,
                    Summary = "I can answer questions about your approved documents, CAPAs, risks, audits, findings, training, suppliers, meetings, objectives and compliance. Try one of the suggestions below.",
                };
            else
            {
                response = await best.Specialist.HandleAsync(ctx);
                response.Specialist = best.Specialist.Name;
                response.SpecialistIcon = best.Specialist.Icon;
            }

            response.Query = ctx.Query;
            response.ScoreConfidence();

            // Grounding guarantee: never present an answer without evidence — and state it once.
            // A specialist that found nothing usually sets a more specific line of its own
            // ("No open risks are recorded for Finance."), which we keep in preference to the
            // generic sentence; only fall back when it said nothing at all.
            if (!response.HasEvidence && string.IsNullOrWhiteSpace(response.Summary))
                response.Summary = EcieResponse.NoEvidence;

            if (response.Suggestions.Count == 0) response.Suggestions = SampleQuestions.Take(6).ToList();

            // Optional AI phrasing (off by default; constrained to already-retrieved evidence).
            if (_ai.IsEnabled && response.HasEvidence)
            {
                var evidenceContext = response.EvidenceUsed.Select(e => $"{e.Kind} {e.Reference}: {e.Title}. {e.Subtitle}").ToList();
                var draft = string.Join(" ", new[] { response.Summary }.Concat(response.Answer));
                var rephrased = await _ai.RephraseAsync(draft, evidenceContext);
                if (!string.IsNullOrWhiteSpace(rephrased)) response.Summary = rephrased!;
            }

            return response;
        }
    }
}
