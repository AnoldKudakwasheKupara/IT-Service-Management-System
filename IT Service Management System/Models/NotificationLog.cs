using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    /// <summary>What kind of record a reminder was raised about.</summary>
    public enum NotificationSubject
    {
        Certificate = 0,
        Maintenance = 1,
        Payment = 2,
        DailySummary = 3
    }

    /// <summary>
    /// Ledger of reminder emails already sent. The reminder scan runs repeatedly through the day,
    /// so before queueing anything it asks this table whether that exact reminder — this record, at
    /// this lead time — has gone out already. Without it, a certificate 30 days from expiry would be
    /// chased on every cycle. Rows are also what the "notified" column on the admin screens reads.
    /// </summary>
    public class NotificationLog
    {
        public int Id { get; set; }

        public NotificationSubject Subject { get; set; }

        /// <summary>Id of the certificate / maintenance record / payment. 0 for digests.</summary>
        public int EntityId { get; set; }

        /// <summary>
        /// Which reminder this was: the lead time in days ("30", "14"), "overdue" for the daily
        /// past-due chase, or the date for a digest. Part of the uniqueness key, so moving a due
        /// date forward legitimately re-arms the earlier thresholds.
        /// </summary>
        [StringLength(40)]
        public string TriggerKey { get; set; } = string.Empty;

        /// <summary>The due/expiry date the reminder was about, so rescheduling re-arms reminders.</summary>
        public DateTime DueOn { get; set; }

        public DateTime SentAt { get; set; } = DateTime.Now;

        /// <summary>Number of addresses the email was queued to — useful when diagnosing silence.</summary>
        public int RecipientCount { get; set; }
    }
}
