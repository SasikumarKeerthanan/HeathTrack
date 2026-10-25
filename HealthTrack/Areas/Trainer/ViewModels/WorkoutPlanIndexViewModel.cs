using System;
using System.Collections.Generic;
using System.Linq;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    public class WorkoutPlanIndexViewModel
    {
        // Personalized plans list
        public List<WorkoutPlans> Plans { get; set; } = new List<WorkoutPlans>();

        // Clients assigned to this trainer
        public List<ClientSummary> AssignedClients { get; set; } = new List<ClientSummary>();

        // Filter / Search inputs
        public string SearchTerm { get; set; }
        public string SelectedGoal { get; set; }
        public string SelectedStatus { get; set; } = "all"; // "all", "active", "deactivated"
        public string SelectedClientId { get; set; }

        // All plans (unfiltered, for global dashboard counters)
        public List<WorkoutPlans> AllPlans { get; set; } = new List<WorkoutPlans>();

        // Summary Counters
        public int TotalClientsCount => AssignedClients.Count;
        public int ActivePlansCount => (AllPlans.Any() ? AllPlans : Plans).Count(p => p.IsActive);
        public int DeactivatedPlansCount => (AllPlans.Any() ? AllPlans : Plans).Count(p => !p.IsActive);
        public int ClientsWithoutPlanCount => AssignedClients.Count(c => !(AllPlans.Any() ? AllPlans : Plans).Any(p => p.ClientId == c.Id && p.IsActive));

        public List<string> Goals { get; set; } = new List<string>
        {
            "All Goals", "Weight Loss", "Muscle Building", "Endurance", "Strength", "General Fitness"
        };
    }
}
