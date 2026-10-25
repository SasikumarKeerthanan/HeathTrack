using System;
using System.Collections.Generic;
using HealthTrack.Models;

namespace HealthTrack.Areas.Client.ViewModels
{
    /// ViewModel for the Client Dashboard.
    public class ClientDashboardViewModel
    {
        // 1. Basic User Info
        public Clients Client { get; set; }
        public Trainers AssignedTrainer { get; set; }
        public ClientHealthProfile HealthProfile { get; set; }
        public ClientPreferences Preferences { get; set; }

        // 2. Health & Weight Statistics (Fetched from ClientProgressRecords)
        public bool HasWeightRecords { get; set; }
        public decimal CurrentWeightKg { get; set; }
        public decimal BaselineWeightKg { get; set; }
        public decimal WeightChangeKg { get; set; }
        public decimal TargetWeightKg { get; set; }
        public int WeightGoalProgressPct { get; set; }
        public decimal HeightCm { get; set; }
        public decimal Bmi { get; set; }
        public string BmiCategory { get; set; }
        public int BmiBarPct { get; set; }

        // 3. Goals Progress (Fetched from ClientGoals)
        public List<ClientGoals> Goals { get; set; } = new List<ClientGoals>();

        // 4. Today's Workout Summary (Fetched from WorkoutPlans & ClientWorkoutLogs)
        public WorkoutPlans ActiveWorkoutPlan { get; set; }
        public string TodayWorkoutDay { get; set; }
        public List<WorkoutPlanExercises> TodayWorkoutExercises { get; set; } = new List<WorkoutPlanExercises>();
        public bool IsTodayWorkoutCompleted { get; set; }
        public ClientWorkoutLogs TodayWorkoutLog { get; set; }
        public int EstimatedWorkoutDurationMinutes { get; set; }
        public int EstimatedCaloriesBurned { get; set; }

        // 5. Today's Diet Summary (Fetched from DietPlans & ClientMealLogs)
        public DietPlans ActiveDietPlan { get; set; }
        public List<DietMeals> PrescribedMeals { get; set; } = new List<DietMeals>();
        public List<ClientMealLogs> TodayMealLogs { get; set; } = new List<ClientMealLogs>();
        public int DailyCalorieTarget { get; set; }
        public int CaloriesConsumedToday { get; set; }
        public int CaloriesRemainingToday { get; set; }
        public int CalorieAdherencePct { get; set; }

        // Macro breakdown calculated dynamically from DB
        public int TargetProteinGrams { get; set; }
        public int ProteinConsumedGrams { get; set; }
        public int ProteinAdherencePct { get; set; }

        public int TargetCarbsGrams { get; set; }
        public int CarbsConsumedGrams { get; set; }
        public int CarbsAdherencePct { get; set; }

        public int TargetFatsGrams { get; set; }
        public int FatsConsumedGrams { get; set; }
        public int FatsAdherencePct { get; set; }

        public decimal TargetWaterLiters { get; set; }
        public decimal WaterConsumedLiters { get; set; }
        public int WaterAdherencePct { get; set; }

        // 6. Recent Activity & Coach Guidance
        public List<ClientWorkoutLogs> RecentWorkoutLogs { get; set; } = new List<ClientWorkoutLogs>();
        public List<TrainerFeedback> RecentCoachFeedback { get; set; } = new List<TrainerFeedback>();

        // 7. Upcoming Appointment & Coach Availability (FR-C-04, FR-C-15, FR-C-17, FR-C-21)
        public Appointments NextUpcomingAppointment { get; set; }
        public int UpcomingAppointmentsCount { get; set; }
        public int AvailableCoachSlotsCount { get; set; }
    }
}
