using System;
using System.Collections.Generic;
using System.Linq;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    public class ExerciseLibraryViewModel
    {
        public List<Exercises> Exercises { get; set; } = new List<Exercises>();

        // Search & Filters
        public string SearchTerm { get; set; }
        public string SelectedCategory { get; set; }
        public string SelectedDifficulty { get; set; }
        public string SelectedMuscle { get; set; }
        public string SelectedEquipment { get; set; }

        // All exercises in library (unfiltered, for global category badges)
        public List<Exercises> AllExercises { get; set; } = new List<Exercises>();

        // Metrics
        public int TotalExercisesCount => (AllExercises.Any() ? AllExercises : Exercises).Count;
        public int StrengthCount => (AllExercises.Any() ? AllExercises : Exercises).Count(e => e.Category == "Strength");
        public int CardioCount => (AllExercises.Any() ? AllExercises : Exercises).Count(e => e.Category == "Cardio");
        public int CoreCount => (AllExercises.Any() ? AllExercises : Exercises).Count(e => e.Category == "Core");
        public int HiitCount => (AllExercises.Any() ? AllExercises : Exercises).Count(e => e.Category == "HIIT");
        public int FlexibilityCount => (AllExercises.Any() ? AllExercises : Exercises).Count(e => e.Category == "Flexibility");

        // Predefined Filter Lists
        public List<string> Categories { get; set; } = new List<string>
        {
            "All Categories", "Strength", "Cardio", "Core", "HIIT", "Flexibility"
        };

        public List<string> Difficulties { get; set; } = new List<string>
        {
            "All Difficulties", "Beginner", "Intermediate", "Advanced"
        };

        public List<string> MuscleGroups { get; set; } = new List<string>
        {
            "All Muscles", "Chest", "Back", "Legs", "Shoulders", "Arms", "Core", "Full Body"
        };

        public List<string> EquipmentList { get; set; } = new List<string>
        {
            "All Equipment", "Bodyweight", "Barbell", "Dumbbell", "Machine", "Cable", "Kettlebell"
        };
    }
}
