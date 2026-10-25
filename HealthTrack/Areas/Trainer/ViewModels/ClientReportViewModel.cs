using System;
using System.Collections.Generic;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    public class ClientReportViewModel
    {
        public string ReportId { get; set; } = $"HT-REP-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        // Client and Trainer Information
        public string ClientId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string PrimaryGoal { get; set; } = string.Empty;
        public string CoachName { get; set; } = string.Empty;

        // Filtering parameters
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string DatePreset { get; set; } = "30days";
        public List<ClientSummary> AvailableClients { get; set; } = new List<ClientSummary>();

        // Baseline vs Current vs Goal
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
                if (CurrentBmi < 18.5) return "Underweight";
                if (CurrentBmi < 25.0) return "Normal";
                if (CurrentBmi < 30.0) return "Overweight";
                return "Obese";
            }
        }

        public double? BaselineBodyFatPct { get; set; }
        public double? CurrentBodyFatPct { get; set; }

        // Workout Performance Summary
        public int TotalWorkoutsScheduled { get; set; }
        public int TotalWorkoutsCompleted { get; set; }
        public int TotalWorkoutsMissed => Math.Max(0, TotalWorkoutsScheduled - TotalWorkoutsCompleted);
        public int WorkoutAdherencePct => TotalWorkoutsScheduled > 0 
            ? (int)Math.Round((double)TotalWorkoutsCompleted / TotalWorkoutsScheduled * 100) 
            : 0;
        public string FormattedAdherenceRate => TotalWorkoutsScheduled > 0 ? $"{WorkoutAdherencePct}%" : "N/A";
        public int TotalExerciseMinutes { get; set; }
        public int TotalCaloriesBurned { get; set; }

        // Nutrition Performance Summary
        public int PrescribedDailyCalories { get; set; }
        public int AverageConsumedCalories { get; set; }
        public int CalorieVariance => AverageConsumedCalories - PrescribedDailyCalories;
        public double PrescribedWaterLiters { get; set; }
        public double AverageWaterLiters { get; set; }

        // Goals Breakdown
        public List<ClientGoalMetric> GoalMilestones { get; set; } = new List<ClientGoalMetric>();

        // Session Logs Table
        public List<ClientWorkoutLogs> SessionLogs { get; set; } = new List<ClientWorkoutLogs>();

        // Periodic Progress Weigh-ins
        public List<ClientProgressRecords> MetricRecords { get; set; } = new List<ClientProgressRecords>();

        // Professional Coach Clinical Summary
        public string CoachEvaluationNotes { get; set; } = string.Empty;
        public string NextPhaseRecommendations { get; set; } = string.Empty;
    }
}
