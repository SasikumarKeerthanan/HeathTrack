using System;
using System.Collections.Generic;
using System.Linq;
using HealthTrack.Models;

namespace HealthTrack.Helpers
{
    /// Helper for matching Clients with Personal Trainers.
    public static class TrainerMatchingHelper
    {
        public class MatchResult
        {
            public int Score { get; set; }
            public bool GoalMatch { get; set; }
            public bool WorkoutTypeMatch { get; set; }
            public bool FitnessLevelMatch { get; set; }
            public bool GenderMatch { get; set; }
            public bool CapacityMatch { get; set; }
        }

        /// Calculates the compatibility match score (0 - 100%) between client preferences and a trainer.
        public static MatchResult CalculateMatch(
            Trainers trainer, 
            string primaryGoal, 
            string preferredWorkoutType, 
            string fitnessLevel, 
            string preferredTrainerGender, 
            int activeClientsCount)
        {
            var result = new MatchResult();
            if (trainer == null) return result;

            int score = 0;
            string trainerSpec = (trainer.PrimarySpecialization ?? trainer.Specialization ?? "General Fitness").ToLower();
            string trainerWorkouts = (trainer.SupportedWorkoutTypes ?? "Strength, Cardio").ToLower();
            string trainerLevels = (trainer.SupportedFitnessLevels ?? "Beginner, Intermediate, Advanced").ToLower();
            int maxCap = trainer.MaxClientCapacity > 0 ? trainer.MaxClientCapacity : 15;

            // 1. Primary Goal & Specialization Match (Weight: 30%)
            if (!string.IsNullOrWhiteSpace(primaryGoal))
            {
                string goal = primaryGoal.ToLower();
                if (trainerSpec.Contains(goal) || goal.Contains(trainerSpec) ||
                    (goal.Contains("weight") && trainerSpec.Contains("weight")) ||
                    (goal.Contains("muscle") && trainerSpec.Contains("muscle")) ||
                    (goal.Contains("endurance") && trainerSpec.Contains("endurance")) ||
                    (goal.Contains("rehab") && trainerSpec.Contains("rehab")))
                {
                    result.GoalMatch = true;
                    score += 30;
                }
                else if (trainerSpec.Contains("general") || goal.Contains("general") || trainerSpec.Contains("fitness"))
                {
                    result.GoalMatch = true;
                    score += 20; // Partial match
                }
            }
            else
            {
                result.GoalMatch = true;
                score += 25; // Neutral
            }

            // 2. Preferred Workout Type Match (Weight: 25%)
            if (!string.IsNullOrWhiteSpace(preferredWorkoutType))
            {
                string workout = preferredWorkoutType.ToLower();
                if (trainerWorkouts.Contains(workout))
                {
                    result.WorkoutTypeMatch = true;
                    score += 25;
                }
                else if ((workout.Contains("strength") && trainerWorkouts.Contains("weight")) ||
                         (workout.Contains("hiit") && trainerWorkouts.Contains("cardio")))
                {
                    result.WorkoutTypeMatch = true;
                    score += 15; // Partial synonym
                }
            }
            else
            {
                result.WorkoutTypeMatch = true;
                score += 20;
            }

            // 3. Fitness Experience Level Match (Weight: 20%)
            if (!string.IsNullOrWhiteSpace(fitnessLevel))
            {
                string level = fitnessLevel.ToLower();
                if (trainerLevels.Contains(level) || trainerLevels.Contains("all"))
                {
                    result.FitnessLevelMatch = true;
                    score += 20;
                }
            }
            else
            {
                result.FitnessLevelMatch = true;
                score += 15;
            }

            // 4. Gender Preference Match (Weight: 15%) - Strict Male/Female check
            if (!string.IsNullOrWhiteSpace(preferredTrainerGender) &&
                !preferredTrainerGender.Equals("No Preference", StringComparison.OrdinalIgnoreCase) &&
                !preferredTrainerGender.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(preferredTrainerGender, trainer.Gender, StringComparison.OrdinalIgnoreCase))
                {
                    result.GenderMatch = true;
                    score += 15;
                }
            }
            else
            {
                result.GenderMatch = true;
                score += 15; // Neutral if no preference
            }

            // 5. Workload Capacity Availability (Weight: 10%)
            if (activeClientsCount < maxCap)
            {
                result.CapacityMatch = true;
                score += 10;
            }
            else
            {
                result.CapacityMatch = false; // 0 points if coach is full
            }

            result.Score = Math.Min(100, Math.Max(0, score));
            return result;
        }

        /// Automatically finds and returns the best matching active trainer for a registering client.
        public static Trainers FindBestMatchingTrainer(
            HealthTrackEntities db,
            string primaryGoal,
            string preferredWorkoutType,
            string fitnessLevel,
            string preferredTrainerGender,
            out int bestScore)
        {
            bestScore = 0;
            if (db == null) return null;

            var activeTrainers = db.Trainers.Where(t => t.IsActive).ToList();
            if (!activeTrainers.Any()) return null;

            Trainers bestTrainer = null;
            int highestScore = -1;
            int highestFreeSlots = -1;

            foreach (var trainer in activeTrainers)
            {
                int activeClients = db.Clients.Count(c => c.AssignedTrainerId == trainer.TrainerId && c.IsActive);
                int maxCap = trainer.MaxClientCapacity > 0 ? trainer.MaxClientCapacity : 15;
                int freeSlots = Math.Max(0, maxCap - activeClients);

                var match = CalculateMatch(trainer, primaryGoal, preferredWorkoutType, fitnessLevel, preferredTrainerGender, activeClients);

                // Priority:
                // 1. Higher score
                // 2. If equal score, prefer coach with open capacity
                // 3. If both have open capacity, prefer coach with more available slots (better workload balance)
                bool isBetter = false;
                if (match.Score > highestScore)
                {
                    if (bestTrainer != null && highestFreeSlots > 0 && freeSlots == 0 && (match.Score - highestScore < 10))
                    {
                        isBetter = false;
                    }
                    else
                    {
                        isBetter = true;
                    }
                }
                else if (match.Score == highestScore)
                {
                    if (freeSlots > highestFreeSlots)
                    {
                        isBetter = true;
                    }
                }

                if (isBetter)
                {
                    highestScore = match.Score;
                    highestFreeSlots = freeSlots;
                    bestTrainer = trainer;
                }
            }

            // Fallback to first trainer if none selected
            if (bestTrainer == null && activeTrainers.Any())
            {
                bestTrainer = activeTrainers.First();
                highestScore = 50;
            }

            bestScore = highestScore;
            return bestTrainer;
        }
    }
}
