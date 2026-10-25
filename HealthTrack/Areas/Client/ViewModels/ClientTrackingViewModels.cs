using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HealthTrack.Models;

namespace HealthTrack.Areas.Client.ViewModels
{
    public class DailyWaterSummary
    {
        public DateTime Date { get; set; }
        public decimal WaterLiters { get; set; }
        public int WaterMl => (int)Math.Round(WaterLiters * 1000);
        public int TargetMl { get; set; }
        public int AdherencePct { get; set; }
    }

    public class ClientTrackingViewModel
    {
        public string ClientName { get; set; }

        // Water Tracking Data
        public decimal WaterConsumedTodayLiters { get; set; }
        public int WaterConsumedTodayMl => (int)Math.Round(WaterConsumedTodayLiters * 1000);
        public decimal TargetWaterLiters { get; set; }
        public int TargetWaterMl => (int)Math.Round(TargetWaterLiters * 1000);
        public int WaterAdherencePct { get; set; }
        public bool HasPersonalWaterTarget { get; set; }
        public int? PersonalWaterTargetMl { get; set; }
        public List<ClientMealLogs> TodayWaterLogs { get; set; } = new List<ClientMealLogs>();
        public List<DailyWaterSummary> WaterHistory { get; set; } = new List<DailyWaterSummary>();

        // Sleep Tracking Data
        public ClientSleepLogs TodaySleep { get; set; }
        public decimal AverageSleepHoursLast7Days { get; set; }
        public int TotalSleepLoggedCount { get; set; }
        public List<ClientSleepLogs> SleepHistory { get; set; } = new List<ClientSleepLogs>();
    }

    public class SetWaterTargetInputModel
    {
        [Required(ErrorMessage = "Please enter your daily water target in millilitres (ml).")]
        [Range(500, 10000, ErrorMessage = "Daily water target must be between 500 ml and 10,000 ml.")]
        [Display(Name = "Daily Water Target (ml)")]
        public int DailyWaterTargetMl { get; set; }
    }

    public class LogSleepInputModel
    {
        [Required(ErrorMessage = "Sleep date is required")]
        [Display(Name = "Sleep Date")]
        public DateTime SleepDate { get; set; }

        [Required(ErrorMessage = "Bedtime is required")]
        [Display(Name = "Bed Time")]
        public DateTime BedTime { get; set; }

        [Required(ErrorMessage = "Wake-up time is required")]
        [Display(Name = "Wake-up Time")]
        public DateTime WakeTime { get; set; }

        [Range(1, 5, ErrorMessage = "Quality rating must be between 1 and 5 stars")]
        [Display(Name = "Sleep Quality (1-5)")]
        public int? QualityRating { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        [Display(Name = "Notes")]
        public string Notes { get; set; }
    }
}
