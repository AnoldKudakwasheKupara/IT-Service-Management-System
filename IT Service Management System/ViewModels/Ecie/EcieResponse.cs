namespace IT_Service_Management_System.ViewModels.Ecie
{
    /// <summary>Confidence tier for a grounded answer — derived from how much approved evidence backs it.</summary>
    public enum EcieConfidence { None, Low, Medium, High }

    /// <summary>A clickable reference to a stored record used as evidence or shown as a related item.</summary>
    public class EvidenceRef
    {
        public string Kind { get; set; } = "";        // Document, Policy, Procedure, Risk, CAPA, Audit, Finding, Meeting, Training, Supplier, Clause, Objective, Evidence…
        public string Reference { get; set; } = "";     // e.g. QMS-POL-0001, CAPA-00007
        public string Title { get; set; } = "";
        public string? Subtitle { get; set; }
        public string? Url { get; set; }
        public string Icon { get; set; } = "fa-file-lines";
        public string? Badge { get; set; }
    }

    /// <summary>
    /// The single, structured envelope every ECIE specialist returns. Answers are assembled ONLY from the
    /// records listed in <see cref="EvidenceUsed"/> — if that is empty the engine states that no approved
    /// evidence exists rather than inventing an answer.
    /// </summary>
    public class EcieResponse
    {
        public string Query { get; set; } = "";
        public string Specialist { get; set; } = "Compliance Intelligence";
        public string SpecialistIcon { get; set; } = "fa-brain";

        public string Summary { get; set; } = "";
        public List<string> Answer { get; set; } = new();   // paragraphs / bullet points, each grounded in evidence

        public EcieConfidence Confidence { get; set; } = EcieConfidence.None;
        public int ConfidencePercent { get; set; }

        public List<EvidenceRef> EvidenceUsed { get; set; } = new();
        public List<EvidenceRef> RelatedDocuments { get; set; } = new();
        public List<EvidenceRef> RelatedPolicies { get; set; } = new();
        public List<EvidenceRef> RelatedProcedures { get; set; } = new();
        public List<EvidenceRef> RelatedRisks { get; set; } = new();
        public List<EvidenceRef> RelatedCapas { get; set; } = new();
        public List<EvidenceRef> RelatedAudits { get; set; } = new();
        public List<EvidenceRef> RelatedMeetings { get; set; } = new();
        public List<EvidenceRef> RelatedTraining { get; set; } = new();

        public List<string> Recommendations { get; set; } = new();
        public List<string> NextActions { get; set; } = new();
        public List<string> PotentialRisks { get; set; } = new();

        public List<string> Suggestions { get; set; } = new();

        public const string NoEvidence = "No approved organisational evidence currently exists.";

        public bool HasEvidence => EvidenceUsed.Count > 0;

        /// <summary>Sets confidence tier + percent from the amount and quality of evidence gathered.</summary>
        public void ScoreConfidence()
        {
            var n = EvidenceUsed.Count;
            (Confidence, ConfidencePercent) = n switch
            {
                0 => (EcieConfidence.None, 0),
                1 => (EcieConfidence.Low, 45),
                <= 3 => (EcieConfidence.Medium, 68),
                <= 7 => (EcieConfidence.High, 86),
                _ => (EcieConfidence.High, 95)
            };
        }
    }
}
