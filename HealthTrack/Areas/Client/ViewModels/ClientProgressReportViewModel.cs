using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Areas.Client.ViewModels
{
    public class ClientProgressReportViewModel
    {
        public string ReportId { get; set; } = $"HT-CPR-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        // 1. Client Identity & Trainer Dossier
        public string ClientId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string PrimaryGoal { get; set; } = string.Empty;
        public string DietaryRestrictions { get; set; } = string.Empty;
        public string Allergies { get; set; } = string.Empty;
        public string MedicalConditions { get; set; } = string.Empty;

        public string TrainerName { get; set; } = string.Empty;
        public string TrainerSpecialization { get; set; } = string.Empty;
        public string TrainerEmail { get; set; } = string.Empty;

        // 2. Reporting Date Range Parameters
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string DatePreset { get; set; } = "30days";
        public DateTime? FilterFromDate { get; set; }
        public DateTime? FilterToDate { get; set; }
        public bool HasActiveFilter => DatePreset != "all";

        // 3. Biometric & Weight Progress Metrics
        public double BaselineWeightKg { get; set; }
        public double CurrentWeightKg { get; set; }
        public double TargetWeightKg { get; set; }
        public double WeightChangeKg => Math.Round(CurrentWeightKg - BaselineWeightKg, 1);

        public double HeightCm { get; set; }
        public double BaselineBmi { get; set; }
        public double CurrentBmi { get; set; }
        public string BmiCategory
        {
            get
            {
                if (CurrentBmi <= 0) return "Not Recorded";
                if (CurrentBmi < 18.5) return "Underweight";
                if (CurrentBmi < 25.0) return "Normal";
                if (CurrentBmi < 30.0) return "Overweight";
                return "Obese";
            }
        }

        public double? BaselineBodyFatPct { get; set; }
        public double? CurrentBodyFatPct { get; set; }

        // 4. Workout Adherence & Physical Performance
        public int TotalWorkoutsScheduled { get; set; }
        public int TotalWorkoutsCompleted { get; set; }
        public int TotalWorkoutsMissed => Math.Max(0, TotalWorkoutsScheduled - TotalWorkoutsCompleted);
        public int WorkoutAdherencePct => TotalWorkoutsScheduled > 0
            ? (int)Math.Min(100, Math.Round((double)TotalWorkoutsCompleted / TotalWorkoutsScheduled * 100))
            : (TotalWorkoutsCompleted > 0 ? 100 : 0);
        public int TotalExerciseMinutes { get; set; }
        public int TotalCaloriesBurned { get; set; }

        // 5. Nutrition & Hydration Compliance
        public int PrescribedDailyCalories { get; set; }
        public int AverageDailyCaloriesConsumed { get; set; }
        public int CalorieVariance => AverageDailyCaloriesConsumed - PrescribedDailyCalories;
        public double PrescribedWaterLiters { get; set; }
        public double AverageDailyWaterLiters { get; set; }

        public int TargetProteinGrams { get; set; }
        public int TargetCarbsGrams { get; set; }
        public int TargetFatsGrams { get; set; }

        public int AverageProteinGrams { get; set; }
        public int AverageCarbsGrams { get; set; }
        public int AverageFatsGrams { get; set; }

        // 6. Active Health Goals & Milestones
        public List<ProgressGoalMetricDto> Goals { get; set; } = new List<ProgressGoalMetricDto>();

        // 7. Their Plans: Active Workout Plan Details
        public bool HasActiveWorkoutPlan { get; set; }
        public string WorkoutPlanName { get; set; } = string.Empty;
        public string WorkoutObjective { get; set; } = string.Empty;
        public string WorkoutDifficulty { get; set; } = string.Empty;
        public int WorkoutDurationWeeks { get; set; }
        public string WorkoutTrainerNotes { get; set; } = string.Empty;
        public string WorkoutProgressionStrategy { get; set; } = string.Empty;
        public string WorkoutRecoveryGuidelines { get; set; } = string.Empty;
        public List<WorkoutPlanDayDto> WorkoutDays { get; set; } = new List<WorkoutPlanDayDto>();

        // 8. Their Plans: Active Diet & Nutrition Plan Details
        public bool HasActiveDietPlan { get; set; }
        public string DietPlanName { get; set; } = string.Empty;
        public string DietObjective { get; set; } = string.Empty;
        public string DietSpecialInstructions { get; set; } = string.Empty;
        public List<DietPlanMealDto> ScheduledMeals { get; set; } = new List<DietPlanMealDto>();

        // 9. Historical Workout Sessions Logs (Recent)
        public List<ProgressWorkoutLogDto> RecentWorkoutLogs { get; set; } = new List<ProgressWorkoutLogDto>();

        // 10. Periodic Biometric Measurement Logs
        public List<ProgressBiometricLogDto> BiometricHistory { get; set; } = new List<ProgressBiometricLogDto>();

        // 11. Coach Clinical Evaluation & Next Phase
        public string CoachEvaluationNotes { get; set; } = string.Empty;
        public string NextPhaseRecommendations { get; set; } = string.Empty;

        // 12. Chart Data Points (For Interactive Trends)
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<double> WeightHistory { get; set; } = new List<double>();
        public List<double> BmiHistory { get; set; } = new List<double>();
        public List<int> CaloriesBurnedHistory { get; set; } = new List<int>();
    }

    public class ProgressGoalMetricDto
    {
        public string GoalName { get; set; }
        public string Category { get; set; }
        public double BaselineValue { get; set; }
        public double CurrentValue { get; set; }
        public double TargetValue { get; set; }
        public int ProgressPercentage { get; set; }
        public string Unit { get; set; }
        public string StatusMessage { get; set; }
        public bool IsAchieved { get; set; }
    }

    public class WorkoutPlanDayDto
    {
        public string DayOfWeek { get; set; }
        public string FocusTitle { get; set; }
        public int TotalExercises { get; set; }
        public int EstimatedMinutes { get; set; }
        public List<WorkoutPlanExerciseDto> Exercises { get; set; } = new List<WorkoutPlanExerciseDto>();
    }

    public class WorkoutPlanExerciseDto
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string TargetMuscle { get; set; }
        public string Equipment { get; set; }
        public int Sets { get; set; }
        public int? Reps { get; set; }
        public int? DurationMinutes { get; set; }
        public int RestSeconds { get; set; }
        public string Instructions { get; set; }
    }

    public class DietPlanMealDto
    {
        public string DayOfWeek { get; set; }
        public string MealType { get; set; } // Breakfast, Lunch, Dinner, Snack
        public string ScheduledTime { get; set; }
        public string MealName { get; set; }
        public string IngredientsAndPortion { get; set; }
        public int Calories { get; set; }
        public int ProteinGrams { get; set; }
        public int CarbsGrams { get; set; }
        public int FatsGrams { get; set; }
        public string Instructions { get; set; }
    }

    public class ProgressWorkoutLogDto
    {
        public DateTime Date { get; set; }
        public string RoutineName { get; set; }
        public int DurationMinutes { get; set; }
        public int CaloriesBurned { get; set; }
        public int? PerceivedExertion { get; set; }
        public string ClientNotes { get; set; }
    }

    public class ProgressBiometricLogDto
    {
        public int RecordId { get; set; }
        public DateTime RecordedDate { get; set; }
        public double WeightKg { get; set; }
        public double HeightCm { get; set; }
        public double? Bmi { get; set; }
        public double? BodyFatPct { get; set; }
        public int? HeartRateBpm { get; set; }
        public int? StepsCount { get; set; }
        public int CaloriesConsumed { get; set; }
        public int CaloriesBurned { get; set; }
        public int WaterIntakeMl { get; set; }
        public string Notes { get; set; }
    }

    public class EditHealthMeasurementViewModel
    {
        public int RecordId { get; set; }
        public string ClientId { get; set; }
        public DateTime RecordedDate { get; set; }

        [Required(ErrorMessage = "Weight is required.")]
        [Range(20.0, 400.0, ErrorMessage = "Weight must be between 20 and 400 kg.")]
        [Display(Name = "Weight (kg)")]
        public decimal WeightKg { get; set; }

        [Required(ErrorMessage = "Height is required.")]
        [Range(50.0, 260.0, ErrorMessage = "Height must be between 50 and 260 cm.")]
        [Display(Name = "Height (cm)")]
        public decimal HeightCm { get; set; }

        [Display(Name = "BMI")]
        public decimal? Bmi { get; set; }

        [Range(30, 250, ErrorMessage = "Heart rate must be between 30 and 250 bpm.")]
        [Display(Name = "Heart Rate (bpm)")]
        public int? HeartRateBpm { get; set; }

        [Range(0, 100000, ErrorMessage = "Steps count must be between 0 and 100,000.")]
        [Display(Name = "Steps Count")]
        public int? StepsCount { get; set; }

        [Range(0, 20000, ErrorMessage = "Calories consumed must be between 0 and 20,000 kcal.")]
        [Display(Name = "Calories Consumed (kcal)")]
        public int CaloriesConsumed { get; set; }

        [Range(0, 20000, ErrorMessage = "Calories burned must be between 0 and 20,000 kcal.")]
        [Display(Name = "Calories Burned (kcal)")]
        public int CaloriesBurned { get; set; }

        [Range(0, 20000, ErrorMessage = "Water intake must be between 0 and 20,000 ml.")]
        [Display(Name = "Water Intake (ml)")]
        public int WaterIntakeMl { get; set; }

        [Range(1.0, 70.0, ErrorMessage = "Body fat percentage must be between 1% and 70%.")]
        [Display(Name = "Body Fat (%)")]
        public decimal? BodyFatPercentage { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        [Display(Name = "Notes")]
        public string Notes { get; set; }
    }
}

