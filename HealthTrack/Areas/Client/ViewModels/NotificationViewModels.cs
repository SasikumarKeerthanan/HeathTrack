using System;
using System.Collections.Generic;

namespace HealthTrack.Areas.Client.ViewModels
{
    public class ClientNotificationItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Category { get; set; } // Coach, Requests, Workouts, Diet, Milestones, Biometrics
        public DateTime Timestamp { get; set; }
        public string Url { get; set; }
        public string Icon { get; set; }
        public string Color { get; set; } // primary, success, warning, danger, info
        public bool IsRead { get; set; }

        public string TimeAgo
        {
            get
            {
                var span = DateTime.UtcNow - Timestamp;
                if (span.TotalMinutes < 1) return "Just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
                return Timestamp.ToString("MMM dd");
            }
        }
    }

    public class ClientNotificationsPageViewModel
    {
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public string ActiveFilter { get; set; } = "all";
        public List<ClientNotificationItem> Notifications { get; set; } = new List<ClientNotificationItem>();
    }
}
