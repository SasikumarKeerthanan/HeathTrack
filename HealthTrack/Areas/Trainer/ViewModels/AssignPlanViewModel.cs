using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    public class ClientSummary
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PrimaryGoal { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public decimal HeightCm { get; set; }
        
        // Health & Dietary Constraints
        public string Allergies { get; set; } = "None";
        public string DietaryRestrictions { get; set; } = "Balanced";
        public string MedicalConditions { get; set; } = "None";

        // Current & Target Body Metrics
        public decimal? BaselineWeightKg { get; set; }
        public decimal? CurrentWeightKg { get; set; }
        public decimal? TargetWeightKg { get; set; }
        public decimal? CurrentBmi { get; set; }
        public string BmiCategory { get; set; } = "Normal";
        public int? CalorieTarget { get; set; }
    }

    public class AssignPlanViewModel
    {
        [Required]
        public int WorkoutPlanId { get; set; }
        public string WorkoutPlanTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a client to assign")]
        [Display(Name = "Client")]
        public string ClientId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Start Date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Program Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "End Date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Program End Date")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(28);

        [Display(Name = "Custom Notes / Trainer Instructions")]
        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        // List of available clients for dropdown (Integration with Member 1 & 3)
        public List<ClientSummary> AvailableClients { get; set; } = new List<ClientSummary>();
    }
}
