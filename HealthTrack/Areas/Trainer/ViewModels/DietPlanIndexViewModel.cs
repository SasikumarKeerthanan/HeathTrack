using System;
using System.Collections.Generic;
using System.Linq;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    public class DietPlanIndexViewModel
    {
        public List<DietPlans> Plans { get; set; } = new List<DietPlans>();
        public List<ClientSummary> AssignedClients { get; set; } = new List<ClientSummary>();

        public string SearchTerm { get; set; }
        public string SelectedPreference { get; set; }
        public string SelectedStatus { get; set; } = "all";
        public string SelectedClientId { get; set; }

        public List<DietPlans> AllPlans { get; set; } = new List<DietPlans>();

        public int TotalClientsCount => AssignedClients.Count;
        public int ActivePlansCount => (AllPlans.Any() ? AllPlans : Plans).Count(p => p.IsActive);
        public int DeactivatedPlansCount => (AllPlans.Any() ? AllPlans : Plans).Count(p => !p.IsActive);
        public int ClientsWithoutPlanCount => AssignedClients.Count(c => !(AllPlans.Any() ? AllPlans : Plans).Any(p => p.ClientId == c.Id && p.IsActive));

        public List<string> DietaryPreferences { get; set; } = new List<string>
        {
            "All Preferences", "Balanced", "High Protein", "Keto", "Low Carb", "Vegetarian", "Vegan", "Mediterranean"
        };
    }
}
