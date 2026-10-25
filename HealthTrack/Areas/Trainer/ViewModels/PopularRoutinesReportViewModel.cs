using System;
using System.Collections.Generic;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    /// <summary>
    /// Represents a single workout routine ranked by client assignment count (FR-T-49)
    /// </summary>
    public class PopularRoutineItemViewModel
    {
        public int Rank { get; set; }
        public string RoutineTitle { get; set; }
        public string Goal { get; set; }
        public string DifficultyLevel { get; set; }
        public int DurationWeeks { get; set; }
        public int DaysPerWeek { get; set; }
        public int TotalAssignments { get; set; }
        public int UniqueClientsCount { get; set; }
        public DateTime? LatestAssignedDate { get; set; }
    }

    /// <summary>
    /// View model for the Trainer Popular Workout Routines Report (FR-T-49)
    /// </summary>
    public class PopularRoutinesReportViewModel
    {
        public List<PopularRoutineItemViewModel> RankedRoutines { get; set; } = new List<PopularRoutineItemViewModel>();

        // High-level aggregate metrics
        public int TotalUniqueRoutines { get; set; }
        public int TotalValidAssignments { get; set; }
        public int TotalClientsServed { get; set; }
        public string TopRoutineTitle { get; set; }
        public int TopRoutineAssignments { get; set; }

        // Filter parameters
        public string SelectedGoal { get; set; }
        public string StatusFilter { get; set; } = "active"; // "active" (default, excluding cancelled/deactivated), "all"
        public string DatePreset { get; set; } = "all"; // "all", "30days", "90days", "custom"
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // Available filter options
        public List<string> AvailableGoals { get; set; } = new List<string>
        {
            "All Goals", "Weight Loss", "Muscle Building", "Endurance", "Strength", "General Fitness"
        };
    }
}
