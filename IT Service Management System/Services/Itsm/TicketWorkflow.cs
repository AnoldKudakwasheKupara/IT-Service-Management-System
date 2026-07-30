using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Services.Itsm
{
    /// <summary>
    /// Central transition rules for helpdesk tickets — the ticket counterpart of
    /// <see cref="ServiceRequestWorkflow"/>. Without it any status could be set from any other, so a
    /// closed ticket could jump straight back to In Progress (bypassing Reopen and its audit entry),
    /// or a resolved one could be parked on hold with no active work left to pause.
    /// </summary>
    public static class TicketWorkflow
    {
        public static bool CanTransition(TicketStatus current, TicketStatus next)
        {
            if (current == next) return true;   // re-saving the same status is a no-op, not a move

            return current switch
            {
                // Fresh work: pick it up, park it, resolve it, or close it outright (duplicate/spam).
                TicketStatus.Open => next is TicketStatus.InProgress or TicketStatus.OnHold
                    or TicketStatus.Resolved or TicketStatus.Closed,

                // Being worked: hand back to the queue, park, resolve, or close.
                TicketStatus.InProgress => next is TicketStatus.Open or TicketStatus.OnHold
                    or TicketStatus.Resolved or TicketStatus.Closed,

                // Parked with the SLA clock paused: resume, or finish it if the wait settled the issue.
                TicketStatus.OnHold => next is TicketStatus.Open or TicketStatus.InProgress
                    or TicketStatus.Resolved or TicketStatus.Closed,

                // Fixed, awaiting confirmation: close it out, or reopen if the fix didn't hold.
                // Never OnHold — there is no active work left to pause.
                TicketStatus.Resolved => next is TicketStatus.Closed or TicketStatus.Open
                    or TicketStatus.InProgress,

                // Terminal. The only way back is a reopen, which returns it to the queue.
                TicketStatus.Closed => next is TicketStatus.Open,

                _ => false
            };
        }

        /// <summary>
        /// The statuses a ticket may legitimately move to from <paramref name="current"/>. Views use this
        /// so the quick-change control only ever offers moves the service will accept.
        /// </summary>
        public static IReadOnlyList<TicketStatus> NextFrom(TicketStatus current) =>
            Enum.GetValues<TicketStatus>()
                .Where(s => s != current && CanTransition(current, s))
                .ToArray();

        /// <summary>Message shown when a rejected transition is attempted.</summary>
        public static string Describe(TicketStatus current, TicketStatus next) =>
            next == TicketStatus.Open
                ? $"A {current} ticket cannot be moved to {next}."
                : $"A {current} ticket cannot be moved to {next}. Reopen it first.";
    }
}
