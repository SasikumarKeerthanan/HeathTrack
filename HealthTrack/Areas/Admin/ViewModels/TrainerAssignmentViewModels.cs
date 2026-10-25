using System;
using System.Collections.Generic;

namespace HealthTrack.Areas.Admin.ViewModels
{
    /// ViewModel for the main Client-Trainer Assignment Screen.
    public class ClientAssignmentIndexViewModel
    {
        public List<ClientAssignmentItemViewModel> Clients { get; set; } = new List<ClientAssignmentItemViewModel>();
        public ClientAssignmentItemViewModel SelectedClient { get; set; }
        public List<TrainerMatchCardViewModel> TrainerMatches { get; set; } = new List<TrainerMatchCardViewModel>();

        public string Filter { get; set; } = "unassigned"; // "unassigned", "assigned", "all"
        public string SearchTerm { get; set; }

        public int TotalClientsCount { get; set; }
        public int UnassignedClientsCount { get; set; }
        public int AssignedClientsCount { get; set; }
        public int TotalTrainersCount { get; set; }
    }

    /// Summary of each client shown in the client list sidebar.
    public class ClientAssignmentItemViewModel
    {
        public string ClientId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Gender { get; set; }
        public string AssignedTrainerId { get; set; }
        public string AssignedTrainerName { get; set; }
        public bool IsActive { get; set; }

        // Preferences from ClientPreferences table
        public string PrimaryGoal { get; set; }
        public string PreferredWorkoutType { get; set; }
        public string FitnessLevel { get; set; }
        public string PreferredTrainerGender { get; set; }
        public string PreferredDietType { get; set; }
        public int? TargetDaysPerWeek { get; set; }

        public bool IsAssigned => !string.IsNullOrEmpty(AssignedTrainerId);
    }

    /// Trainer recommendation card showing compatibility match score.
    public class TrainerMatchCardViewModel
    {
        public string TrainerId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Gender { get; set; }
        public string PrimarySpecialization { get; set; }
        public string SupportedWorkoutTypes { get; set; }
        public string SupportedFitnessLevels { get; set; }
        public int? ExperienceYears { get; set; }
        public int MaxClientCapacity { get; set; }
        public int ActiveClientsCount { get; set; }
        public int CapacityRemaining => Math.Max(0, MaxClientCapacity - ActiveClientsCount);
        public bool IsAtCapacity => ActiveClientsCount >= MaxClientCapacity;
        public bool IsCurrentlyAssigned { get; set; }

        // Match Score Calculation (0 - 100%)
        public int MatchPercentage { get; set; }

        // Individual Criteria Checks
        public bool GoalMatch { get; set; }
        public bool WorkoutTypeMatch { get; set; }
        public bool FitnessLevelMatch { get; set; }
        public bool GenderMatch { get; set; }
        public bool CapacityMatch { get; set; }

        // Presentation helpers
        public string MatchTier { get; set; }       // "Excellent", "Great", "Moderate", "Low"
        public string MatchBadgeClass { get; set; }  // "success", "info", "warning", "secondary"
    }

    /// Payload for assigning a trainer to a client.
    public class AssignTrainerRequestModel
    {
        public string ClientId { get; set; }
        public string TrainerId { get; set; }
    }

    /// <summary>
    /// Represents an individual historical or active assignment record (FR-A-19)
    /// </summary>
    public class AssignmentHistoryItemViewModel
    {
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string ClientEmail { get; set; }
        public string TrainerId { get; set; }
        public string TrainerName { get; set; }
        public string TrainerSpecialization { get; set; }
        public DateTime AssignmentDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } // "Active", "Ended", "Reassigned"
        public string AdminActionBy { get; set; }
        public string Details { get; set; }
        public bool IsActive => string.Equals(Status, "Active", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// View model for Administrator Trainer-Client Assignment History (FR-A-19)
    /// </summary>
    public class AssignmentHistoryIndexViewModel
    {
        public List<AssignmentHistoryItemViewModel> HistoryRecords { get; set; } = new List<AssignmentHistoryItemViewModel>();
        public string StatusFilter { get; set; } = "all"; // "all", "active", "ended"
        public string SearchTerm { get; set; }

        public int TotalAssignmentsCount { get; set; }
        public int ActiveAssignmentsCount { get; set; }
        public int EndedAssignmentsCount { get; set; }
    }
}
