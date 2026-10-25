using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    public class WorkoutPlanExerciseItem
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please select an exercise")]
        public int ExerciseId { get; set; }
        public string ExerciseName { get; set; }

        [Required(ErrorMessage = "Schedule Day is required")]
        public string DayOfWeek { get; set; } = "Monday";

        public int OrderIndex { get; set; } = 1;

        [Range(1, 20, ErrorMessage = "Sets: 1-20")]
        public int Sets { get; set; } = 3;

        [Range(1, 100, ErrorMessage = "Reps: 1-100")]
        public int? Reps { get; set; } = 12;

        [Range(0, 180, ErrorMessage = "Duration: 0-180 min")]
        public int? DurationMinutes { get; set; }

        [Range(0, 600, ErrorMessage = "Rest: 0-600 sec")]
        public int RestSeconds { get; set; } = 60;

        public string Instructions { get; set; } = string.Empty;

        public string ExerciseRole { get; set; } = string.Empty;
    }

    public class WorkoutPlanFormViewModel
    {
        public int Id { get; set; }

        // Client Selection (Personalized Plan for this Client)
        [Required(ErrorMessage = "Please select the assigned client for this personalized plan")]
        [Display(Name = "Assigned Client")]
        public string ClientId { get; set; } = string.Empty;

        public string ClientName { get; set; }
        public string ClientEmail { get; set; }

        [Required(ErrorMessage = "Personalized plan title is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 100 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please outline the personalized program goals")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Fitness goal is required")]
        [Display(Name = "Client Goal")]
        public string Goal { get; set; } = "Weight Loss";

        [Required(ErrorMessage = "Difficulty level is required")]
        [Display(Name = "Difficulty Level")]
        public string DifficultyLevel { get; set; } = "Beginner";

        [Required(ErrorMessage = "Duration in weeks is required")]
        [Range(1, 52, ErrorMessage = "Duration must be between 1 and 52 weeks")]
        [Display(Name = "Duration (Weeks)")]
        public int DurationWeeks { get; set; } = 4;

        [Required(ErrorMessage = "Target workout days per week is required")]
        [Range(1, 7, ErrorMessage = "Days per week must be between 1 and 7")]
        [Display(Name = "Workout Days / Week")]
        public int DaysPerWeek { get; set; } = 3;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Target End Date")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(28);

        [Display(Name = "Scheduled Workout Start Time")]
        [DataType(DataType.Time)]
        public TimeSpan? ScheduledStartTime { get; set; }

        [Display(Name = "Personal Coaching Notes for this Client")]
        [StringLength(500)]
        public string TrainerNotes { get; set; }

        [Display(Name = "Active Plan")]
        public bool IsActive { get; set; } = true;

        // Hypertrophy & Training Strategy Guidelines
        [Display(Name = "Target Rep Range")]
        [StringLength(50)]
        public string TargetRepRange { get; set; } = "6 – 30";

        [Display(Name = "Weekly Set Volume")]
        [StringLength(50)]
        public string WeeklySetVolume { get; set; } = "10 – 20";

        [Display(Name = "RIR Target (Proximity to Failure)")]
        [StringLength(50)]
        public string RirTarget { get; set; } = "1 – 3";

        [Display(Name = "Recommended Rest Period")]
        [StringLength(50)]
        public string RecommendedRest { get; set; } = "1 – 2.5 min";

        [Display(Name = "Plan Progression Strategy")]
        [StringLength(1000)]
        public string ProgressionStrategy { get; set; }

        [Display(Name = "Recovery Considerations")]
        [StringLength(1000)]
        public string RecoveryGuidelines { get; set; }

        // Exercise Schedule Items
        public List<WorkoutPlanExerciseItem> Exercises { get; set; } = new List<WorkoutPlanExerciseItem>();

        // Dropdown Lists
        public List<ClientSummary> AvailableClients { get; set; } = new List<ClientSummary>();
        public List<Exercises> AvailableExercises { get; set; } = new List<Exercises>();

        public List<string> GoalsList { get; set; } = new List<string>
        {
            "Weight Loss", "Muscle Building", "Endurance", "Strength", "Flexibility", "General Fitness"
        };

        public List<string> DifficultiesList { get; set; } = new List<string>
        {
            "Beginner", "Intermediate", "Advanced"
        };

        public List<string> DaysList { get; set; } = new List<string>
        {
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
        };
    }
}
