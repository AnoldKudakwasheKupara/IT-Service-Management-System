namespace IT_Service_Management_System.ViewModels
{
    /// <summary>Which store a notification came from, so mark-as-read can route back to it.</summary>
    public enum NotificationSource
    {
        /// <summary>Employee File Management (DocumentNotifications).</summary>
        Documents,
        /// <summary>Integrated Management System / ISO (IsoNotifications).</summary>
        Iso
    }

    /// <summary>One notification, normalised across the per-module notification tables.</summary>
    public class NotificationItem
    {
        public NotificationSource Source { get; set; }
        public int Id { get; set; }
        public string Kind { get; set; } = "";      // the module's own notification type, as text
        public string Title { get; set; } = "";
        public string? Message { get; set; }
        public string? Url { get; set; }
        public string Icon { get; set; } = "fa-bell";
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }

        /// <summary>"5 minutes ago" style age, for the inbox list.</summary>
        public string Age
        {
            get
            {
                var d = DateTime.Now - CreatedAt;
                if (d.TotalMinutes < 1) return "just now";
                if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes}m ago";
                if (d.TotalHours < 24) return $"{(int)d.TotalHours}h ago";
                if (d.TotalDays < 7) return $"{(int)d.TotalDays}d ago";
                return CreatedAt.ToString("dd MMM yyyy");
            }
        }
    }

    /// <summary>The unified notification centre: every module's notifications in one list.</summary>
    public class NotificationInboxVm
    {
        public List<NotificationItem> Items { get; set; } = new();
        public int UnreadCount => Items.Count(i => !i.IsRead);
        public int DocumentsUnread => Items.Count(i => !i.IsRead && i.Source == NotificationSource.Documents);
        public int IsoUnread => Items.Count(i => !i.IsRead && i.Source == NotificationSource.Iso);

        /// <summary>Only unread are shown when true (the default view is everything).</summary>
        public bool UnreadOnly { get; set; }
    }
}
