using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Areas.Admin.ViewModels
{
    public class AdminNotificationItemViewModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Category { get; set; }
        public string Icon { get; set; } = "bi-bell-fill";
        public string Color { get; set; } = "primary";
        public DateTime Timestamp { get; set; }
        public string ActionUrl { get; set; }
        public bool IsRead { get; set; }

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

        public string FormattedTime
        {
            get
            {
                var t = Timestamp.Kind == DateTimeKind.Utc ? Timestamp.ToLocalTime() : Timestamp;
                return t.ToString("MMM dd, yyyy - hh:mm tt");
            }
        }
    }

    public class CreateAnnouncementViewModel
    {
        [Required(ErrorMessage = "Announcement title is required.")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        [Display(Name = "Announcement Title")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Announcement message is required.")]
        [StringLength(4000, ErrorMessage = "Message cannot exceed 4000 characters.")]
        [Display(Name = "Message Content")]
        public string Message { get; set; }

        [Display(Name = "Clients / Members")]
        public bool TargetClients { get; set; } = true;

        [Display(Name = "Fitness Trainers / Coaches")]
        public bool TargetTrainers { get; set; } = true;

        [Display(Name = "System Administrators")]
        public bool TargetAdmins { get; set; } = false;

        public bool HasSelectedRole => TargetClients || TargetTrainers || TargetAdmins;
    }

    public class AnnouncementListItemViewModel
    {
        public int AnnouncementId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string TargetRoles { get; set; }
        public string CreatedByAdminId { get; set; }
        public string CreatedByAdminName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalDelivered { get; set; }
        public int ReadCount { get; set; }

        public string FormattedRoles
        {
            get
            {
                if (string.IsNullOrWhiteSpace(TargetRoles)) return "All Users";
                return TargetRoles.Replace(",", ", ");
            }
        }

        public string TimeAgo
        {
            get
            {
                var t = CreatedAt.Kind == DateTimeKind.Utc ? CreatedAt.ToLocalTime() : CreatedAt;
                var span = DateTime.Now - t;
                if (span.TotalMinutes < 1) return "Just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
                return t.ToString("MMM dd, yyyy");
            }
        }
    }

    public class AdminNotificationIndexViewModel
    {
        public List<AdminNotificationItemViewModel> Notifications { get; set; } = new List<AdminNotificationItemViewModel>();
        public List<AnnouncementListItemViewModel> PublishedAnnouncements { get; set; } = new List<AnnouncementListItemViewModel>();
        public CreateAnnouncementViewModel CreateAnnouncementForm { get; set; } = new CreateAnnouncementViewModel();

        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public string Filter { get; set; } = "all";

        public int ActiveClientsCount { get; set; }
        public int ActiveTrainersCount { get; set; }
        public int ActiveAdminsCount { get; set; }
    }
}
