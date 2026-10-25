using System;
using System.Collections.Generic;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    public class ProgressAnalyticsViewModel
    {
        public string SelectedClientId { get; set; } = string.Empty;
        public ClientSummary SelectedClient { get; set; }
        public List<ClientSummary> AvailableClients { get; set; } = new List<ClientSummary>();

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string DatePreset { get; set; } = "30days"; // "7days", "30days", "90days", "all"

        // KPI Summary Metrics
        public double CurrentWeight { get; set; }
        public double BaselineWeight { get; set; }
        public double WeightChangeKg => Math.Round(CurrentWeight - BaselineWeight, 1);
        public double TargetWeight { get; set; }

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

        public int TotalWorkoutsScheduled { get; set; }
        public int TotalWorkoutsCompleted { get; set; }
        public int WorkoutCompletionRatePct => TotalWorkoutsScheduled > 0 
            ? (int)Math.Round((double)TotalWorkoutsCompleted / TotalWorkoutsScheduled * 100) 
            : 0;
        public string FormattedCompletionRate => TotalWorkoutsScheduled > 0 ? $"{WorkoutCompletionRatePct}%" : "N/A";

        public int TotalCaloriesBurned { get; set; }
        public int AvgDailyCaloriesConsumed { get; set; }
        public int DailyCalorieTarget { get; set; }
        public int TotalActiveMinutes { get; set; }

        // Chart Data Arrays
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<double> WeightHistory { get; set; } = new List<double>();
        public List<double> BmiHistory { get; set; } = new List<double>();
        public List<int> CaloriesConsumedHistory { get; set; } = new List<int>();
        public List<int> CaloriesBurnedHistory { get; set; } = new List<int>();
        public List<int> ActivityMinutesHistory { get; set; } = new List<int>();

        // Goal Tracking
        public List<ClientGoalMetric> Goals { get; set; } = new List<ClientGoalMetric>();

        // Recent Activity Logs
        public List<ClientWorkoutLogs> RecentLogs { get; set; } = new List<ClientWorkoutLogs>();
    }
}
