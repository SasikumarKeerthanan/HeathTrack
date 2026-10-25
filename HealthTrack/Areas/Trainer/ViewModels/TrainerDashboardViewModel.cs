using System;
using System.Collections.Generic;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    /// ViewModel for the Trainer Dashboard.
    public class TrainerDashboardViewModel
    {
        public Trainers Trainer { get; set; }

        // Live Database Metrics
        public int AssignedClientCount { get; set; }
        public int ActiveWorkoutPlansCount { get; set; }
        public int ActiveDietPlansCount { get; set; }
        public int TotalExercisesCount { get; set; }

        // Computed Database Summaries
        public int AverageDietPlanCalories { get; set; }
        public string CommonWorkoutSplit { get; set; }
        public int StrengthExercisesCount { get; set; }
        public int CardioExercisesCount { get; set; }
        public int CoreExercisesCount { get; set; }

        // Assigned Clients
        public List<Clients> AssignedClients { get; set; } = new List<Clients>();

        // Live Client Activity Feed
        public List<ClientWorkoutLogs> RecentWorkoutLogs { get; set; } = new List<ClientWorkoutLogs>();
        public List<ClientMealLogs> RecentMealLogs { get; set; } = new List<ClientMealLogs>();
        public List<TrainerFeedback> RecentFeedbackHistory { get; set; } = new List<TrainerFeedback>();

        // Appointments Metrics (FR-T-03)
        public int TodayAppointmentsCount { get; set; }
        public int PendingAppointmentsCount { get; set; }
        public List<Appointments> UpcomingAppointments { get; set; } = new List<Appointments>();
    }
}
