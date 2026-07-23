using IT_Service_Management_System.Models.Itsm;

namespace IT_Service_Management_System.Helpers
{
    /// <summary>
    /// The major-incident state machine. Pure, dependency-free logic so the allowed lifecycle
    /// transitions can be unit-tested and shared by the controller and views.
    /// </summary>
    public static class MajorIncidentWorkflow
    {
        /// <summary>Forward transitions permitted from each status. Any open status may also be
        /// closed (cancelled) directly; resolution timestamps are enforced separately.</summary>
        private static readonly Dictionary<MajorIncidentStatus, MajorIncidentStatus[]> Forward = new()
        {
            [MajorIncidentStatus.Declared]      = new[] { MajorIncidentStatus.Investigating },
            [MajorIncidentStatus.Investigating] = new[] { MajorIncidentStatus.Identified, MajorIncidentStatus.Recovering },
            [MajorIncidentStatus.Identified]    = new[] { MajorIncidentStatus.Recovering },
            [MajorIncidentStatus.Recovering]    = new[] { MajorIncidentStatus.Resolved },
            [MajorIncidentStatus.Resolved]      = new[] { MajorIncidentStatus.Review },
            [MajorIncidentStatus.Review]        = new[] { MajorIncidentStatus.Closed },
            [MajorIncidentStatus.Closed]        = Array.Empty<MajorIncidentStatus>(),
        };

        /// <summary>Whether <paramref name="to"/> is a legal next status from <paramref name="from"/>.</summary>
        public static bool CanTransition(MajorIncidentStatus from, MajorIncidentStatus to)
        {
            if (from == to) return false;
            if (from == MajorIncidentStatus.Closed) return false;            // closed is terminal
            if (to == MajorIncidentStatus.Closed) return true;              // any open incident may be closed
            return Forward.TryGetValue(from, out var next) && Array.IndexOf(next, to) >= 0;
        }

        /// <summary>The statuses an incident may legally move to next (excluding a direct close).</summary>
        public static IReadOnlyList<MajorIncidentStatus> NextStages(MajorIncidentStatus from) =>
            Forward.TryGetValue(from, out var next) ? next : Array.Empty<MajorIncidentStatus>();

        /// <summary>True once the incident has reached a state where service is considered restored.</summary>
        public static bool IsResolvedState(MajorIncidentStatus status) =>
            status is MajorIncidentStatus.Resolved or MajorIncidentStatus.Review or MajorIncidentStatus.Closed;

        /// <summary>Mean-time-to-resolve across the resolved incidents supplied, in minutes (null if none).</summary>
        public static double? MeanTimeToResolveMinutes(IEnumerable<MajorIncident> incidents)
        {
            var durations = incidents
                .Where(i => i.ResolvedAt.HasValue)
                .Select(i => (i.ResolvedAt!.Value - i.DeclaredAt).TotalMinutes)
                .Where(m => m >= 0)
                .ToList();
            return durations.Count == 0 ? null : durations.Average();
        }
    }
}
