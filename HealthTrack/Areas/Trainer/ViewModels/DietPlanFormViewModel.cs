using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    public class DietMealFormItem
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Schedule Day is required")]
        public string DayOfWeek { get; set; } = "Daily Routine"; // Monday..Sunday or Daily Routine

        [Required(ErrorMessage = "Meal Type is required")]
        public string MealType { get; set; } = "Breakfast"; // Breakfast, Lunch, Dinner, Snack

        public string ScheduledTime { get; set; } = string.Empty;

        [Required(ErrorMessage = "Meal name or food items are required")]
        public string MealName { get; set; } = string.Empty;

        public string IngredientsAndPortion { get; set; }

        [Required(ErrorMessage = "Calories are required")]
        [Range(10, 3000, ErrorMessage = "Calories must be between 10 and 3000 kcal")]
        public int? Calories { get; set; }

        [Range(0, 300, ErrorMessage = "Protein: 0-300g")]
        public int? ProteinGrams { get; set; }

        [Range(0, 500, ErrorMessage = "Carbs: 0-500g")]
        public int? CarbsGrams { get; set; }

        [Range(0, 200, ErrorMessage = "Fats: 0-200g")]
        public int? FatsGrams { get; set; }

        public string Instructions { get; set; }
        public int OrderIndex { get; set; } = 1;

        public string ImagePath { get; set; }
        public HttpPostedFileBase ImageFile { get; set; }
        public string ServingSize { get; set; }
        public string Description { get; set; }
        public string Ingredients { get; set; }
        public string Allergens { get; set; }
        public string DietaryInfo { get; set; }
        public string TrainerNotes { get; set; }
    }

    public class DietPlanFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please select the assigned client")]
        [Display(Name = "Assigned Client")]
        public string ClientId { get; set; } = string.Empty;

        public string ClientName { get; set; }
        public string ClientEmail { get; set; }
        public string ClientAllergies { get; set; } = "None";
        public string ClientDietaryRestrictions { get; set; } = "Balanced";
        public string ClientMedicalConditions { get; set; } = "None";
        public decimal? ClientWeightKg { get; set; }
        public decimal? ClientTargetWeightKg { get; set; }
        public decimal? ClientBmi { get; set; }
        public string ClientBmiCategory { get; set; } = "Normal";

        [Required(ErrorMessage = "Plan title is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 100 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Plan overview / strategy is required")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Dietary preference is required")]
        [Display(Name = "Dietary Strategy")]
        public string DietaryPreference { get; set; } = string.Empty;

        [Required(ErrorMessage = "Daily calorie target is required")]
        [Range(800, 6000, ErrorMessage = "Daily calories must be between 800 and 6000 kcal")]
        [Display(Name = "Daily Target Calories")]
        public int? DailyCalorieTarget { get; set; }

        [Range(20, 400, ErrorMessage = "Target protein: 20-400g")]
        [Display(Name = "Protein Target (g)")]
        public int? TargetProteinGrams { get; set; }

        [Range(20, 600, ErrorMessage = "Target carbs: 20-600g")]
        [Display(Name = "Carbs Target (g)")]
        public int? TargetCarbsGrams { get; set; }

        [Range(10, 250, ErrorMessage = "Target fats: 10-250g")]
        [Display(Name = "Fats Target (g)")]
        public int? TargetFatsGrams { get; set; }

        [Range(1.0, 10.0, ErrorMessage = "Water intake: 1-10 L")]
        [Display(Name = "Daily Water Target (L)")]
        public double? WaterIntakeLiters { get; set; }

        [Required(ErrorMessage = "Duration in weeks is required")]
        [Range(1, 52, ErrorMessage = "Duration must be between 1 and 52 weeks")]
        [Display(Name = "Duration (Weeks)")]
        public int? DurationWeeks { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime? StartDate { get; set; }

        [Required(ErrorMessage = "Target end date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Target End Date")]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Special Dietary Instructions & Coaching Notes")]
        [StringLength(1000)]
        public string SpecialInstructions { get; set; } = string.Empty;

        [Display(Name = "Active Plan")]
        public bool IsActive { get; set; } = true;

        // Dynamic Meals Schedule List
        public List<DietMealFormItem> Meals { get; set; } = new List<DietMealFormItem>();

        // Dropdown References
        public List<ClientSummary> AvailableClients { get; set; } = new List<ClientSummary>();

        public List<string> DietaryPreferencesList { get; set; } = new List<string>
        {
            "Balanced", "High Protein", "Keto", "Low Carb", "Vegetarian", "Vegan", "Mediterranean"
        };

        public List<string> MealTypesList { get; set; } = new List<string>
        {
            "Breakfast", "Lunch", "Dinner", "Snack"
        };

        public List<string> DaysList { get; set; } = new List<string>
        {
            "Daily Routine", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
        };
    }
}
