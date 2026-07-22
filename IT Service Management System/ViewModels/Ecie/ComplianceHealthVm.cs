namespace IT_Service_Management_System.ViewModels.Ecie
{
    public class ScoreItem
    {
        public string Name { get; set; } = "";
        public int Score { get; set; }
        public string Icon { get; set; } = "fa-gauge";
        public string Detail { get; set; } = "";
        public string Band => Score >= 85 ? "green" : Score >= 60 ? "amber" : "red";
    }

    /// <summary>The continuously-calculated Compliance Health Score set for the executive dashboard.</summary>
    public class ComplianceHealthVm
    {
        public int Overall { get; set; }
        public string OverallBand => Overall >= 85 ? "green" : Overall >= 60 ? "amber" : "red";
        public string OverallLabel => Overall >= 85 ? "Strong" : Overall >= 60 ? "Fair" : "At Risk";

        public List<ScoreItem> Scores { get; set; } = new();
        public List<string> Alerts { get; set; } = new();
        public bool AiEnabled { get; set; }
    }
}
