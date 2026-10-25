using System;
using System.Collections.Generic;

namespace HealthTrack.Areas.Admin.ViewModels
{
    /// ViewModel for Admin Analytics and System Reports.
    public class AdminReportsViewModel
    {
        // 1. Date Filter Properties
        public string DateRange { get; set; } = "30days"; // "7days", "30days", "90days", "year", "all", "custom"
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        // 2. User Registration Statistics
        public int TotalClients { get; set; }
        public int ActiveClients { get; set; }
        public int InactiveClients { get; set; }
        public int NewClientsInPeriod { get; set; }

        public int TotalTrainers { get; set; }
        public int ActiveTrainers { get; set; }
        public int InactiveTrainers { get; set; }
        public int NewTrainersInPeriod { get; set; }

        // 3. Appointment Statistics
        public int TotalAppointments { get; set; }
        public int PendingAppointments { get; set; }
        public int ApprovedAppointments { get; set; }
        public int CompletedAppointments { get; set; }
        public int CancelledAppointments { get; set; }

        // 4. Workout Engagement & System Usage
        public int TotalWorkoutLogs { get; set; }
        public long TotalCaloriesBurned { get; set; }
        public long TotalStepsLogged { get; set; }
        public int TotalMealLogs { get; set; }
        public int TotalSecurityEvents { get; set; }

        // 5. Chart Data: Monthly/Weekly Registration Trend
        public List<string> TrendLabels { get; set; } = new List<string>();
        public List<int> ClientTrendData { get; set; } = new List<int>();
        public List<int> TrainerTrendData { get; set; } = new List<int>();

        // 6. Trainer Activity Table & Chart
        public List<TrainerActivityItem> TrainerActivities { get; set; } = new List<TrainerActivityItem>();

        // 7. Recent Workout & System Engagement Activity
        public List<EngagementActivityItem> RecentActivities { get; set; } = new List<EngagementActivityItem>();
    }

    /// Holds activity and workload performance metrics for an individual trainer.
    public class TrainerActivityItem
    {
        public string TrainerId { get; set; }
        public string FullName { get; set; }
        public string Specialization { get; set; }
        public int AssignedClientsCount { get; set; }
        public int MaxCapacity { get; set; }
        public int TotalAppointmentsCount { get; set; }
        public int CompletedSessionsCount { get; set; }
        public int UtilizationPercentage { get; set; }
        public bool IsActive { get; set; }
    }

    /// Represents a single logged workout, meal, or consultation activity in the system.
    public class EngagementActivityItem
    {
        public DateTime ActivityDate { get; set; }
        public string ClientName { get; set; }
        public string ActivityType { get; set; } // "Workout", "Meal", "Appointment", "Progress"
        public string Title { get; set; }
        public string DetailSummary { get; set; }
        public string Status { get; set; }
    }

    /// <summary>
    /// View model for an individual client goal progress entry (FR-A-26)
    /// </summary>
    public class ClientGoalProgressItem
    {
        public string GoalName { get; set; }
        public string Category { get; set; }
        public double BaselineValue { get; set; }
        public double CurrentValue { get; set; }
        public double TargetValue { get; set; }
        public string Unit { get; set; }
        public int ProgressPercentage { get; set; }
        public string StatusMessage { get; set; }
    }

    /// <summary>
    /// View model for Administrator Client Performance Reports (FR-A-26)
    /// </summary>
    public class AdminClientPerformanceReportViewModel
    {
        // Selected Client Details
        public string SelectedClientId { get; set; }
        public string ClientName { get; set; }
        public string ClientEmail { get; set; }
        public string Gender { get; set; }
        public string PrimaryGoal { get; set; }
        public string AssignedTrainerName { get; set; }
        public string AssignedTrainerId { get; set; }
        public DateTime ClientRegisteredDate { get; set; }

        // Dropdown client options
        public List<System.Web.Mvc.SelectListItem> AvailableClients { get; set; } = new List<System.Web.Mvc.SelectListItem>();

        // Date-Range Filters
        public string DatePreset { get; set; } = "30days"; // "7days", "30days", "90days", "all", "custom"
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string ValidationError { get; set; }

        // 1. Health Measurement History and Progress
        public double BaselineWeightKg { get; set; }
        public double CurrentWeightKg { get; set; }
        public double TargetWeightKg { get; set; }
        public double WeightChangeKg => Math.Round(CurrentWeightKg - BaselineWeightKg, 1);
        public double CurrentBmi { get; set; }
        public string BmiCategory { get; set; }
        public double? CurrentBodyFatPct { get; set; }
        public string BloodPressure { get; set; }
        public int? RestingHeartRateBpm { get; set; }

        // 2. Workout Completion and Engagement
        public int TotalWorkoutsScheduled { get; set; }
        public int TotalWorkoutsCompleted { get; set; }
        public int WorkoutCompletionRatePct => TotalWorkoutsScheduled > 0 ? (int)Math.Round((double)TotalWorkoutsCompleted / TotalWorkoutsScheduled * 100) : 0;
        public string FormattedCompletionRate => TotalWorkoutsScheduled > 0 ? $"{WorkoutCompletionRatePct}%" : "N/A";
        public long TotalCaloriesBurned { get; set; }
        public int TotalExerciseMinutes { get; set; }

        // 3. Recorded Health Goal Progress
        public List<ClientGoalProgressItem> GoalProgressItems { get; set; } = new List<ClientGoalProgressItem>();

        // 4. Meal Tracking or Dietary Adherence
        public int PrescribedDailyCalories { get; set; }
        public int AverageConsumedCalories { get; set; }
        public double PrescribedWaterLiters { get; set; }
        public double AverageWaterLiters { get; set; }
        public int TotalMealsLogged { get; set; }

        // Chart & Detailed History
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<double> WeightHistory { get; set; } = new List<double>();
        public List<int> CaloriesBurnedHistory { get; set; } = new List<int>();
        public List<int> CaloriesConsumedHistory { get; set; } = new List<int>();

        public List<HealthTrack.Models.ClientProgressRecords> ProgressRecords { get; set; } = new List<HealthTrack.Models.ClientProgressRecords>();
        public List<HealthTrack.Models.ClientWorkoutLogs> WorkoutLogs { get; set; } = new List<HealthTrack.Models.ClientWorkoutLogs>();
        public List<HealthTrack.Models.ClientMealLogs> MealLogs { get; set; } = new List<HealthTrack.Models.ClientMealLogs>();
    }

    /// <summary>
    /// View model for Administrator Popular Workout Routine Reports (FR-A-27)
    /// Reuses established FR-T-49 ranking logic across authorized system routines
    /// </summary>
    public class AdminPopularRoutinesReportViewModel
    {
        public List<HealthTrack.Areas.Trainer.ViewModels.PopularRoutineItemViewModel> RankedRoutines { get; set; } = new List<HealthTrack.Areas.Trainer.ViewModels.PopularRoutineItemViewModel>();

        // Executive Aggregate Metrics
        public int TotalUniqueRoutines { get; set; }
        public int TotalValidAssignments { get; set; }
        public int TotalClientsServed { get; set; }
        public string TopRoutineTitle { get; set; }
        public int TopRoutineAssignments { get; set; }

        // Filters
        public string SelectedGoal { get; set; }
        public string SelectedTrainerId { get; set; }
        public string StatusFilter { get; set; } = "active"; // "active", "all", "cancelled"
        public string DatePreset { get; set; } = "all"; // "all", "30days", "90days", "custom"
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        // Dropdown selection lists
        public List<string> AvailableGoals { get; set; } = new List<string>
        {
            "All Goals", "Weight Loss", "Muscle Building", "Endurance", "Strength", "General Fitness"
        };
        public List<System.Web.Mvc.SelectListItem> AvailableTrainers { get; set; } = new List<System.Web.Mvc.SelectListItem>();
    }

    /// <summary>
    /// View model for an assigned client item distinguishing active vs historical (FR-A-28)
    /// </summary>
    public class TrainerClientAssignmentSummaryItem
    {
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string ClientEmail { get; set; }
        public string AssignmentStatus { get; set; } // "Active", "Historical"
        public DateTime? AssignmentDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int CompletedWorkoutsCount { get; set; }
        public int CompletedAppointmentsCount { get; set; }
    }

    /// <summary>
    /// View model for Administrator Trainer Activity and Engagement Reports (FR-A-28)
    /// Combines FR-T-50 Workout Engagement and FR-T-51 Appointment Activity calculations
    /// </summary>
    public class AdminTrainerActivityReportViewModel
    {
        public string SelectedTrainerId { get; set; }
        public string TrainerName { get; set; }
        public string TrainerEmail { get; set; }
        public string Specialization { get; set; }
        public int MaxClientCapacity { get; set; }
        public bool IsActiveTrainer { get; set; }

        public List<System.Web.Mvc.SelectListItem> AvailableTrainers { get; set; } = new List<System.Web.Mvc.SelectListItem>();

        // Date-Range Filters
        public string DatePreset { get; set; } = "30days"; // "7days", "30days", "90days", "all", "custom"
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string ValidationError { get; set; }

        // Client Assignment Activity (distinguishing active vs historical)
        public int ActiveClientsCount { get; set; }
        public int HistoricalClientsCount { get; set; }
        public int TotalClientsServed => ActiveClientsCount + HistoricalClientsCount;
        public int CapacityRemaining => Math.Max(0, MaxClientCapacity - ActiveClientsCount);

        // Workout Engagement Metrics (FR-T-50 calculations)
        public int ScheduledWorkoutsCount { get; set; }
        public int CompletedWorkoutsCount { get; set; }
        public int? WorkoutCompletionRatePct { get; set; } // Null if cannot be calculated (e.g. 0 scheduled)
        public string FormattedCompletionRate => WorkoutCompletionRatePct.HasValue ? $"{WorkoutCompletionRatePct.Value}%" : "N/A";
        public long TotalCaloriesBurned { get; set; }
        public int TotalExerciseMinutes { get; set; }

        // Appointment Activity by Status (FR-T-51 calculations)
        public int TotalAppointments { get; set; }
        public int PendingAppointments { get; set; }
        public int ApprovedAppointments { get; set; }
        public int CompletedAppointments { get; set; }
        public int CancelledAppointments { get; set; }
        public int RejectedAppointments { get; set; }

        // Detailed Lists
        public List<TrainerClientAssignmentSummaryItem> ClientRoster { get; set; } = new List<TrainerClientAssignmentSummaryItem>();
        public List<HealthTrack.Areas.Trainer.ViewModels.AppointmentActivityDetailItem> AppointmentDetails { get; set; } = new List<HealthTrack.Areas.Trainer.ViewModels.AppointmentActivityDetailItem>();
    }
}
