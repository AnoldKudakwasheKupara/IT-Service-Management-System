namespace IT_Service_Management_System.Helpers
{
    /// <summary>
    /// Single source of truth for asset action/event types and the status /
    /// assignment transitions they produce. Used by the Assets and
    /// AssetHistories controllers and surfaced to the views.
    /// </summary>
    public static class AssetWorkflow
    {
        public const string InStock = "In Stock";
        public const string Issued = "Issued";
        public const string Returned = "Returned";
        public const string InRepair = "In Repair";
        public const string Retired = "Retired";
        public const string Stolen = "Stolen";

        /// <summary>The action types a user can record, in display order.</summary>
        public static readonly string[] EventTypes =
        {
            InStock, Issued, Returned, InRepair, Retired, Stolen
        };

        /// <summary>The status an asset has after the given action.</summary>
        public static string StatusFor(string? eventType) => eventType switch
        {
            Issued => "Issued",
            Returned => "In Stock",
            InStock => "In Stock",
            InRepair => "In Repair",
            Retired => "Retired",
            Stolen => "Stolen",
            _ => "In Stock"
        };

        /// <summary>Only an "Issued" action needs a target user.</summary>
        public static bool RequiresUser(string? eventType) => eventType == Issued;

        /// <summary>
        /// The asset's current holder after the action: assigned on Issue,
        /// retained during Repair, otherwise cleared (back to stock).
        /// </summary>
        public static int? ResolveHolder(string? eventType, int? currentUserId, int? chosenUserId) => eventType switch
        {
            Issued => chosenUserId,
            InRepair => currentUserId,
            _ => null
        };
    }
}
