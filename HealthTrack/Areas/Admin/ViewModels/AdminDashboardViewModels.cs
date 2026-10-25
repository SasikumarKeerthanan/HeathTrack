using System;
using System.Collections.Generic;

namespace HealthTrack.Areas.Admin.ViewModels
{
    public class AdminDashboardViewModel
    {
        // KPI Counters
        public int TotalClients { get; set; }
        public int ActiveClients { get; set; }
        public int InactiveClients { get; set; }
        public int UnassignedClients { get; set; }

        public int TotalTrainers { get; set; }
        public int ActiveTrainers { get; set; }
        public int InactiveTrainers { get; set; }
        public int PendingInviteTrainers { get; set; }

        public int TotalAppointments { get; set; }
        public int ScheduledAppointments { get; set; }
        public int CompletedAppointments { get; set; }

        public int PendingTrainerChangeRequests { get; set; }

        // Trainer Capacity Summaries
        public List<TrainerWorkloadSummaryItem> TrainerWorkloads { get; set; } = new List<TrainerWorkloadSummaryItem>();

        // Recent Audit / Security Activity Feed
        public List<RecentActivityItem> RecentActivities { get; set; } = new List<RecentActivityItem>();
    }

    public class TrainerWorkloadSummaryItem
    {
        public string TrainerId { get; set; }
        public string FullName { get; set; }
        public string Specialization { get; set; }
        public int ActiveClientsCount { get; set; }
        public int MaxCapacity { get; set; }
        public int UtilizationPercentage { get; set; }
        public bool IsActive { get; set; }
    }

    public class RecentActivityItem
    {
        public long LogId { get; set; }
        public string UserId { get; set; }
        public string UserRole { get; set; }
        public string EventType { get; set; }
        public string Details { get; set; }
        public DateTime TimestampUtc { get; set; }
    }
}
