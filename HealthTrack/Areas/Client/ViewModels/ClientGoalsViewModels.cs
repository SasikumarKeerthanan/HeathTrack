using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HealthTrack.Models;

namespace HealthTrack.Areas.Client.ViewModels
{
    public class ClientGoalChartData
    {
        public List<string> Labels { get; set; } = new List<string>();
        public List<decimal> CurrentValues { get; set; } = new List<decimal>();
        public List<decimal> TargetValues { get; set; } = new List<decimal>();
        public List<int> ProgressPercentages { get; set; } = new List<int>();
    }

    public class ClientWorkoutChartData
    {
        public List<string> Dates { get; set; } = new List<string>();
        public List<int> CompletedCount { get; set; } = new List<int>();
        public List<int> DurationMinutes { get; set; } = new List<int>();
        public List<int> CaloriesBurned { get; set; } = new List<int>();
    }

    public class ClientGoalsIndexViewModel
    {
        public string ClientName { get; set; }
        public List<ClientGoals> ActiveGoals { get; set; } = new List<ClientGoals>();
        public List<ClientGoals> CompletedGoals { get; set; } = new List<ClientGoals>();
        public List<ClientGoals> CancelledGoals { get; set; } = new List<ClientGoals>();

        public int TotalGoalsCount => (ActiveGoals?.Count ?? 0) + (CompletedGoals?.Count ?? 0) + (CancelledGoals?.Count ?? 0);
        public int ActiveGoalsCount => ActiveGoals?.Count ?? 0;
        public int CompletedGoalsCount => CompletedGoals?.Count ?? 0;
        private int? _cancelledGoalsCount;
        public int CancelledGoalsCount
        {
            get { return _cancelledGoalsCount ?? (CancelledGoals?.Count ?? 0); }
            set { _cancelledGoalsCount = value; }
        }
        public int OverallCompletionRate => (ActiveGoalsCount + CompletedGoalsCount) > 0 
            ? (int)Math.Round(((double)CompletedGoalsCount / (ActiveGoalsCount + CompletedGoalsCount)) * 100) 
            : 0;

        public ClientGoalChartData GoalChartData { get; set; } = new ClientGoalChartData();
        public ClientWorkoutChartData WorkoutChartData { get; set; } = new ClientWorkoutChartData();
    }

    public class CreateGoalInputModel
    {
        [Required(ErrorMessage = "Goal name is required")]
        [StringLength(120, ErrorMessage = "Goal name cannot exceed 120 characters")]
        [Display(Name = "Goal Name")]
        public string GoalName { get; set; }

        [Required(ErrorMessage = "Category is required")]
        [StringLength(30)]
        public string Category { get; set; }

        [Required(ErrorMessage = "Baseline value is required")]
        [Range(typeof(decimal), "0", "99999999", ErrorMessage = "Please enter a valid starting value")]
        [Display(Name = "Baseline / Starting Value")]
        public decimal BaselineValue { get; set; }

        [Required(ErrorMessage = "Current value is required")]
        [Range(typeof(decimal), "0", "99999999", ErrorMessage = "Please enter a valid current value")]
        [Display(Name = "Current Value")]
        public decimal CurrentValue { get; set; }

        [Required(ErrorMessage = "Target value is required")]
        [Range(typeof(decimal), "0.01", "99999999", ErrorMessage = "Target value must be greater than 0")]
        [Display(Name = "Target Value")]
        public decimal TargetValue { get; set; }

        [Required(ErrorMessage = "Unit is required")]
        [StringLength(20)]
        [Display(Name = "Unit (kg, steps, hrs, etc.)")]
        public string Unit { get; set; }

        [Display(Name = "Target Completion Date")]
        public DateTime? TargetDate { get; set; }

        [StringLength(250)]
        [Display(Name = "Notes / Status Message")]
        public string StatusMessage { get; set; }
    }

    public class EditGoalInputModel
    {
        [Required]
        public int GoalId { get; set; }

        [Required(ErrorMessage = "Goal name is required")]
        [StringLength(120)]
        public string GoalName { get; set; }

        [Required]
        [StringLength(30)]
        public string Category { get; set; }

        [Required]
        [Range(typeof(decimal), "0", "99999999")]
        public decimal CurrentValue { get; set; }

        [Required]
        [Range(typeof(decimal), "0.01", "99999999")]
        public decimal TargetValue { get; set; }

        [Required]
        [StringLength(20)]
        public string Unit { get; set; }

        public DateTime? TargetDate { get; set; }

        [StringLength(250)]
        public string StatusMessage { get; set; }
    }
}
