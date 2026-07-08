namespace IT_Service_Management_System.Services.Ecie
{
    /// <summary>
    /// Optional AI phrasing/semantic seam for the Compliance Intelligence Engine. It is deliberately
    /// constrained: an implementation may only rephrase text that is already grounded in retrieved
    /// evidence, or rank evidence snippets — it is NEVER the source of facts. The default
    /// <see cref="NullAiProvider"/> makes no external calls, so nothing leaves the organisation and the
    /// engine cannot hallucinate. Wire a real provider (Azure OpenAI / Anthropic) later without changing
    /// any specialist by registering a different IAiProvider.
    /// </summary>
    public interface IAiProvider
    {
        /// <summary>True when a real model is configured. Specialists fall back to templated prose when false.</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Rephrase a grounded draft into fluent prose using ONLY the supplied evidence context.
        /// Implementations must not add facts beyond <paramref name="evidenceContext"/>.
        /// Returns null to signal the caller should keep its templated text.
        /// </summary>
        Task<string?> RephraseAsync(string groundedDraft, IReadOnlyList<string> evidenceContext, CancellationToken ct = default);

        /// <summary>Rank candidate snippets by semantic relevance to the query. Returns indices best-first, or null if unsupported.</summary>
        Task<int[]?> RankAsync(string query, IReadOnlyList<string> candidates, CancellationToken ct = default);
    }

    /// <summary>
    /// The default, fully-offline provider. Makes no network calls and returns no additional content, so
    /// the engine stays deterministic and evidence-grounded. This is what runs unless an AI key is configured.
    /// </summary>
    public sealed class NullAiProvider : IAiProvider
    {
        public bool IsEnabled => false;
        public Task<string?> RephraseAsync(string groundedDraft, IReadOnlyList<string> evidenceContext, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
        public Task<int[]?> RankAsync(string query, IReadOnlyList<string> candidates, CancellationToken ct = default)
            => Task.FromResult<int[]?>(null);
    }
}
