using System;
using System.Collections.Generic;
using HealthTrack.Models;

namespace HealthTrack.Areas.Client.ViewModels
{
    public class ClientDietSummaryDto
    {
        public string ClientId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Gender { get; set; }
        public string AssignedTrainerId { get; set; }
        public string TrainerName { get; set; }
        public string PrimaryGoal { get; set; }
        public decimal? CurrentWeightKg { get; set; }
        public decimal? TargetWeightKg { get; set; }
        public string DietaryRestrictions { get; set; }
        public string Allergies { get; set; }
    }

    public class DietMealDto
    {
        public int DietMealId { get; set; }
        public int DietPlanId { get; set; }
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
        public string ServingSize { get; set; }
        public string Description { get; set; }
        public string Allergens { get; set; }
        public string DietaryInfo { get; set; }
        public string TrainerNotes { get; set; }
        public string ImagePath { get; set; }

        // Live Today Tracking Status
        public bool IsConsumedToday { get; set; }
        public int? ConsumedLogId { get; set; }
        public string ConsumedTime { get; set; }
        public string ClientNotes { get; set; }
    }

    public class TodayNutritionSummaryDto
    {
        public int TargetCalories { get; set; }
        public int ConsumedCalories { get; set; }
        public int RemainingCalories { get; set; }
        public int CalorieProgressPercent { get; set; }

        public int TargetProtein { get; set; }
        public int ConsumedProtein { get; set; }
        public int ProteinPercent { get; set; }

        public int TargetCarbs { get; set; }
        public int ConsumedCarbs { get; set; }
        public int CarbsPercent { get; set; }

        public int TargetFats { get; set; }
        public int ConsumedFats { get; set; }
        public int FatsPercent { get; set; }

        public decimal TargetWaterLiters { get; set; }
        public decimal ConsumedWaterLiters { get; set; }
        public int WaterGlasses { get; set; }
        public int TargetWaterGlasses { get; set; }

        public int TotalMealsScheduled { get; set; }
        public int MealsConsumedCount { get; set; }
        public string ComplianceStatus { get; set; }
    }

    public class ClientMealLogItemDto
    {
        public int MealLogId { get; set; }
        public string ClientId { get; set; }
        public int? DietPlanId { get; set; }
        public int? DietMealId { get; set; }
        public DateTime LogDate { get; set; }
        public string MealType { get; set; }
        public string MealName { get; set; }
        public string PortionSize { get; set; }
        public int Calories { get; set; }
        public int ProteinGrams { get; set; }
        public int CarbsGrams { get; set; }
        public int FatsGrams { get; set; }
        public decimal WaterLiters { get; set; }
        public bool IsCompleted { get; set; }
        public string ClientNotes { get; set; }
        public string CoachFeedback { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DietHubViewModel
    {
        public ClientDietSummaryDto Client { get; set; }
        public DietPlans ActivePlan { get; set; }
        public TodayNutritionSummaryDto TodaySummary { get; set; }
        public List<DietMealDto> TodayMeals { get; set; }
        public List<ClientMealLogItemDto> RecentLogs { get; set; }
        public string TodayDateFormatted { get; set; }
        public string TodayDayOfWeek { get; set; }
    }

    public class DietPlanDetailsViewModel
    {
        public ClientDietSummaryDto Client { get; set; }
        public DietPlans ActivePlan { get; set; }
        public Dictionary<string, List<DietMealDto>> GroupedMeals { get; set; }
        public List<DietMealDto> AllMeals { get; set; }
        public List<DietPlans> PastPlans { get; set; }
        public int TotalDailyCalories { get; set; }
        public int TotalDailyProtein { get; set; }
        public int TotalDailyCarbs { get; set; }
        public int TotalDailyFats { get; set; }
    }

    public class DietCalendarDayDto
    {
        public DateTime Date { get; set; }
        public int DayNumber { get; set; }
        public string DayName { get; set; }
        public bool IsCurrentMonth { get; set; }
        public bool IsToday { get; set; }
        public bool IsPast { get; set; }
        public int TotalCaloriesConsumed { get; set; }
        public int TargetCalories { get; set; }
        public int MealsLoggedCount { get; set; }
        public bool HasLogs { get; set; }
        public string StatusBadge { get; set; }
        public string StatusClass { get; set; }
        public List<ClientMealLogItemDto> LoggedMeals { get; set; }
    }

    public class DietCalendarViewModel
    {
        public ClientDietSummaryDto Client { get; set; }
        public DietPlans ActivePlan { get; set; }
        public int CurrentMonth { get; set; }
        public int CurrentYear { get; set; }
        public string MonthName { get; set; }
        public List<DietCalendarDayDto> DaysInMonth { get; set; }
        public int AdherenceRatePercent { get; set; }
        public int DaysOnTarget { get; set; }
        public int AvgDailyCalories { get; set; }
        public int TotalLoggedMealsThisMonth { get; set; }

        // History tab support in the same page (FR-C-62)
        public List<ClientMealLogItemDto> HistoryLogs { get; set; }
        public string SearchTerm { get; set; }
        public string FilterMealType { get; set; }
        public string FilterStatus { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public bool HasActiveHistoryFilter =>
            !string.IsNullOrWhiteSpace(SearchTerm) ||
            (!string.IsNullOrWhiteSpace(FilterMealType) && !FilterMealType.Equals("all", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(FilterStatus) && !FilterStatus.Equals("all", StringComparison.OrdinalIgnoreCase)) ||
            FromDate.HasValue ||
            ToDate.HasValue;
    }

    public class LogMealInputModel
    {
        public string ClientId { get; set; }
        public int? DietPlanId { get; set; }
        public int? DietMealId { get; set; }
        public string MealType { get; set; }
        public string MealName { get; set; }
        public string PortionSize { get; set; }
        public int Calories { get; set; }
        public int ProteinGrams { get; set; }
        public int CarbsGrams { get; set; }
        public int FatsGrams { get; set; }
        public decimal WaterLiters { get; set; }
        public string ClientNotes { get; set; }
        public DateTime? LogDate { get; set; }
    }
}
