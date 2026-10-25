using System;
using System.Collections.Generic;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    /// Represents a single notification item for a trainer.
    public class TrainerNotificationItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Category { get; set; } // Appointments, Clients, Workouts, System
        public string Icon { get; set; } = "bi-bell-fill";
        public string Color { get; set; } = "primary"; // primary, success, warning, info, danger
        public DateTime Timestamp { get; set; }
        public string ActionUrl { get; set; }
        public bool IsRead { get; set; }

        /// Calculates relative time for display such as "5m ago" or "2h ago".
        public string TimeAgo
        {
            get
            {
                var t = Timestamp.Kind == DateTimeKind.Utc ? Timestamp.ToLocalTime() : Timestamp;
                var span = DateTime.Now - t;
                if (span.TotalMinutes < 1) return "Just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
                return t.ToString("MMM dd, yyyy");
            }
        }

        /// Formats full readable date and time string.
        public string FormattedTime
        {
            get
            {
                var t = Timestamp.Kind == DateTimeKind.Utc ? Timestamp.ToLocalTime() : Timestamp;
                return t.ToString("MMM dd, yyyy - hh:mm tt");
            }
        }
    }

    /// View model for the Trainer Notification Center page.
    public class TrainerNotificationIndexViewModel
    {
        public List<TrainerNotificationItem> Notifications { get; set; } = new List<TrainerNotificationItem>();
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public string Filter { get; set; } = "all";
    }
}
