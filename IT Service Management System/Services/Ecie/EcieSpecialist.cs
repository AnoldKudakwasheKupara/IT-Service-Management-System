using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.ViewModels.Ecie;

namespace IT_Service_Management_System.Services.Ecie
{
    /// <summary>Everything a specialist needs to answer a question, resolved once by the orchestrator.</summary>
    public class EcieContext
    {
        public string Query { get; init; } = "";
        public string Lower { get; init; } = "";
        public List<string> Tokens { get; init; } = new();
        public string? Role { get; init; }
        public int? UserId { get; init; }
        public string? Clause { get; set; }
        public Department? Department { get; set; }

        public ApplicationDbContext Db { get; init; } = default!;
        public EvidenceGraphService Graph { get; init; } = default!;
        public IAiProvider Ai { get; init; } = default!;

        public bool Mentions(params string[] words) => words.Any(w => Lower.Contains(w));
    }

    /// <summary>
    /// One of the domain "AI specialists" (ISO Consultant, Risk Advisor, Auditor, …). Each scores how well
    /// it fits a question; the orchestrator routes to the top scorer. Handlers ground every answer in the
    /// Evidence Graph — they must never assert anything not present in <see cref="EcieResponse.EvidenceUsed"/>.
    /// </summary>
    public interface IEcieSpecialist
    {
        string Name { get; }
        string Icon { get; }
        /// <summary>0 = not applicable; higher = stronger match.</summary>
        int Score(EcieContext ctx);
        Task<EcieResponse> HandleAsync(EcieContext ctx);
    }
}
