using System;
using System.Collections.Generic;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    // ========================================================================
    // Individual client preview card for Page 1
    // ========================================================================
    public class ClientCardItem
    {
        public string ClientId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public DateTime JoinedDate { get; set; }
        public string PrimaryGoal { get; set; }
        public string FitnessLevel { get; set; }
        public int? ActiveWorkoutPlanId { get; set; }
        public string ActiveWorkoutPlanTitle { get; set; }
        public int? ActiveDietPlanId { get; set; }
        public string ActiveDietPlanTitle { get; set; }
        public decimal? CurrentWeightKg { get; set; }
        public decimal? CurrentBmi { get; set; }
        public string BmiCategory { get; set; }
        public string AssignmentStatus { get; set; } = "Assigned client";
        public DateTime? LatestRecordedDate { get; set; }

        public bool HasActiveWorkout => ActiveWorkoutPlanId.HasValue || !string.IsNullOrWhiteSpace(ActiveWorkoutPlanTitle);
        public bool HasActiveDiet => ActiveDietPlanId.HasValue || !string.IsNullOrWhiteSpace(ActiveDietPlanTitle);
        public bool IsFullyConfigured => HasActiveWorkout && HasActiveDiet;
    }

    // ========================================================================
    // ViewModel for Page 1 — Assigned Clients listing (with search & filters)
    // ========================================================================
    public class AssignedClientsIndexViewModel
    {
        // Search & Filters
        public string SearchTerm { get; set; }
        public string SelectedGoal { get; set; } = "all";
        public string SelectedStatus { get; set; } = "all";
        public string SelectedBmi { get; set; } = "all";
        public string SortBy { get; set; } = "name_asc";

        // KPI Counts across all assigned clients
        public int TotalAssigned { get; set; }
        public int WithActiveWorkoutCount { get; set; }
        public int WithActiveDietCount { get; set; }
        public int NeedsAttentionCount { get; set; }

        // Filter Options
        public List<string> AvailableGoals { get; set; } = new List<string>();

        // Filtered Clients Output
        public List<ClientCardItem> Clients { get; set; } = new List<ClientCardItem>();
        public int TotalCount => Clients != null ? Clients.Count : 0;
    }

    // ========================================================================
    // Goal item for Tab 2
    // ========================================================================
    public class ClientGoalItem
    {
        public int GoalId { get; set; }
        public string GoalName { get; set; }
        public string Category { get; set; }
        public double BaselineValue { get; set; }
        public double CurrentValue { get; set; }
        public double TargetValue { get; set; }
        public string Unit { get; set; }
        public int ProgressPercentage { get; set; }
        public string StatusMessage { get; set; }
        public bool IsAchieved { get; set; }
        public DateTime? TargetDate { get; set; }
    }

    // ========================================================================
    // Progress record for Tab 2
    // ========================================================================
    public class ClientProgressItem
    {
        public DateTime RecordedDate { get; set; }
        public decimal WeightKg { get; set; }
        public decimal? Bmi { get; set; }
        public int CaloriesConsumed { get; set; }
        public int CaloriesBurned { get; set; }
        public int WaterIntakeMl { get; set; }
        public string Notes { get; set; }
    }

    // ========================================================================
    // Workout log for Tab 3
    // ========================================================================
    public class ClientWorkoutHistoryItem
    {
        public int LogId { get; set; }
        public DateTime LogDate { get; set; }
        public string SessionName { get; set; }
        public int DurationMinutes { get; set; }
        public int CaloriesBurned { get; set; }
        public bool IsCompleted { get; set; }
        public int PerceivedExertion { get; set; }
        public string ClientNotes { get; set; }
        public string CoachFeedback { get; set; }
        public string WorkoutPlanTitle { get; set; }
        public int? WorkoutPlanId { get; set; }
    }

    // ========================================================================
    // Meal log for Tab 4
    // ========================================================================
    public class ClientMealHistoryItem
    {
        public int MealLogId { get; set; }
        public DateTime LogDate { get; set; }
        public string MealType { get; set; }
        public string MealName { get; set; }
        public string PortionSize { get; set; }
        public int Calories { get; set; }
        public int ProteinGrams { get; set; }
        public int CarbsGrams { get; set; }
        public int FatsGrams { get; set; }
        public decimal WaterLiters { get; set; }
        public string ClientNotes { get; set; }
    }

    // ========================================================================
    // ViewModel for Page 2 — Client Details (Single page with 4 tabs)
    // ========================================================================
    public class ClientDetailsViewModel
    {
        public string ActiveTab { get; set; } = "profile";

        // Client core identity
        public ClientCardItem Client { get; set; }

        // Tab 1: Profile & Health
        public int? Age { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? BaselineWeightKg { get; set; }
        public decimal? Bmi { get; set; }
        public string BmiCategory { get; set; } = "Normal";
        public string Allergies { get; set; }
        public string DietaryRestrictions { get; set; }
        public string MedicalConditions { get; set; }
        public string InjuriesHistory { get; set; }
        public string FitnessLevel { get; set; }
        public int? TargetDaysPerWeek { get; set; }
        public string PreferredWorkoutType { get; set; }
        public string PreferredDietType { get; set; }

        // Tab 2: Goals & Progress
        public List<ClientGoalItem> Goals { get; set; } = new List<ClientGoalItem>();
        public List<ClientProgressItem> ProgressRecords { get; set; } = new List<ClientProgressItem>();
        public List<ClientSleepLogs> SleepLogs { get; set; } = new List<ClientSleepLogs>();
        public int TotalCompletedWorkouts { get; set; }

        // Tab 3: Workout History
        public List<ClientWorkoutHistoryItem> WorkoutLogs { get; set; } = new List<ClientWorkoutHistoryItem>();
        public int? ActiveWorkoutPlanId { get; set; }
        public string ActiveWorkoutPlanTitle { get; set; }
        public string WorkoutDateFilter { get; set; } = "30days";

        // Tab 4: Meal History
        public List<ClientMealHistoryItem> MealLogs { get; set; } = new List<ClientMealHistoryItem>();
        public int? ActiveDietPlanId { get; set; }
        public string ActiveDietPlanTitle { get; set; }
        public int? DailyCalorieTarget { get; set; }
        public string MealDateFilter { get; set; } = "30days";
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int TotalLoggedCalories { get; set; }
        public int AverageDailyCalories { get; set; }
    }
}
