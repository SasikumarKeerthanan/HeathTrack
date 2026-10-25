using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using HealthTrack.Models;

namespace HealthTrack.Helpers
{
    /// <summary>
    /// Encapsulates engagement metrics calculated strictly according to FR-S-40 and FR-T-50 specifications:
    /// Workout Completion Rate (%) = (Completed Eligible Scheduled Workouts / Total Eligible Scheduled Workouts) × 100
    /// </summary>
    public class WorkoutEngagementMetrics
    {
        public int ScheduledWorkoutsCount { get; set; }
        public int CompletedWorkoutsCount { get; set; }
        public int? CompletionRatePct { get; set; } // Null if denominator is zero (displays N/A)
        public string FormattedCompletionRate => CompletionRatePct.HasValue ? $"{CompletionRatePct.Value}%" : "N/A";
        public long TotalCaloriesBurned { get; set; }
        public int TotalExerciseMinutes { get; set; }
    }

    /// <summary>
    /// Shared calculation engine for Workout Engagement (FR-S-40, FR-T-50, and FR-A-28).
    /// Ensures 100% mathematical and reporting consistency across Trainer and Administrator modules.
    /// </summary>
    public static class EngagementCalculationHelper
    {
        /// <summary>
        /// Calculates workout engagement for all clients assigned to a specific trainer within a date range (FR-A-28).
        /// </summary>
        public static WorkoutEngagementMetrics CalculateTrainerEngagement(HealthTrackEntities db, string trainerId, DateTime start, DateTime end)
        {
            var metrics = new WorkoutEngagementMetrics();
            if (db == null || string.IsNullOrWhiteSpace(trainerId))
            {
                return metrics;
            }

            DateTime now = DateTime.Now;
            DateTime maxEligibleDate = end.Date < now.Date ? end.Date : now.Date;

            // 1. Fetch active workout plans for this trainer
            var activePlans = db.WorkoutPlans
                .Include(p => p.WorkoutPlanExercises)
                .Where(p => p.TrainerId == trainerId && p.IsActive)
                .ToList();

            var activePlanIds = activePlans.Select(p => p.WorkoutPlanId).ToHashSet();

            // 2. Pre-fetch completed workout logs within the filter window
            var workoutLogs = db.ClientWorkoutLogs
                .Where(w => w.WorkoutPlanId.HasValue && activePlanIds.Contains(w.WorkoutPlanId.Value) && w.LogDate >= start && w.LogDate <= end)
                .ToList();

            metrics.TotalCaloriesBurned = workoutLogs.Where(w => w.IsCompleted).Sum(w => (long)w.CaloriesBurned);
            metrics.TotalExerciseMinutes = workoutLogs.Where(w => w.IsCompleted).Sum(w => w.DurationMinutes);

            // Group completed logs by ClientId + Date to prevent duplicate counts from multiple logs on the same day (Rule 9)
            var completedDatesByClient = workoutLogs
                .Where(w => w.IsCompleted)
                .GroupBy(w => w.ClientId)
                .ToDictionary(
                    g => g.Key,
                    g => new HashSet<DateTime>(g.Select(w => w.LogDate.Date))
                );

            int totalScheduled = 0;
            int totalCompleted = 0;

            foreach (var plan in activePlans)
            {
                var prescribedDays = new HashSet<string>(
                    plan.WorkoutPlanExercises.Select(e => e.DayOfWeek),
                    StringComparer.OrdinalIgnoreCase
                );

                if (prescribedDays.Count == 0)
                {
                    continue;
                }

                // Effective occurrence window: [max(plan.StartDate, start), min(plan.EndDate, maxEligibleDate)]
                DateTime windowStart = plan.StartDate.Date > start.Date ? plan.StartDate.Date : start.Date;
                DateTime windowEnd = plan.EndDate.Date < maxEligibleDate ? plan.EndDate.Date : maxEligibleDate;

                if (windowStart > windowEnd)
                {
                    continue; // Future plan or outside date range (Rule 6)
                }

                completedDatesByClient.TryGetValue(plan.ClientId, out var clientCompletedDates);

                for (DateTime d = windowStart; d <= windowEnd; d = d.AddDays(1))
                {
                    string dayName = d.DayOfWeek.ToString();
                    if (prescribedDays.Contains(dayName))
                    {
                        totalScheduled++; // Count each eligible scheduled occurrence once (Rule 3)

                        if (clientCompletedDates != null && clientCompletedDates.Contains(d))
                        {
                            totalCompleted++; // Completed eligible scheduled workout (Rule 7)
                        }
                    }
                }
            }

            metrics.ScheduledWorkoutsCount = totalScheduled;
            metrics.CompletedWorkoutsCount = totalCompleted;

            if (totalScheduled > 0)
            {
                metrics.CompletionRatePct = (int)Math.Min(100, Math.Max(0, Math.Round((double)totalCompleted / totalScheduled * 100)));
            }
            else
            {
                metrics.CompletionRatePct = null; // Denominator zero => N/A (Rule 12)
            }

            return metrics;
        }

        /// <summary>
        /// Calculates workout engagement for an individual client within a date range (FR-T-50 / FR-A-27 Client Performance).
        /// </summary>
        public static WorkoutEngagementMetrics CalculateClientEngagement(HealthTrackEntities db, string clientId, DateTime start, DateTime end)
        {
            var metrics = new WorkoutEngagementMetrics();
            if (db == null || string.IsNullOrWhiteSpace(clientId))
            {
                return metrics;
            }

            DateTime now = DateTime.Now;
            DateTime maxEligibleDate = end.Date < now.Date ? end.Date : now.Date;

            // Fetch active workout plan for this client
            var activePlans = db.WorkoutPlans
                .Include(p => p.WorkoutPlanExercises)
                .Where(p => p.ClientId == clientId && p.IsActive)
                .ToList();

            var activePlanIds = activePlans.Select(p => p.WorkoutPlanId).ToHashSet();

            // Pre-fetch completed workout logs for this client
            var workoutLogs = db.ClientWorkoutLogs
                .Where(w => w.ClientId == clientId && w.LogDate >= start && w.LogDate <= end)
                .ToList();

            metrics.TotalCaloriesBurned = workoutLogs.Where(w => w.IsCompleted).Sum(w => (long)w.CaloriesBurned);
            metrics.TotalExerciseMinutes = workoutLogs.Where(w => w.IsCompleted).Sum(w => w.DurationMinutes);

            var completedDates = new HashSet<DateTime>(
                workoutLogs.Where(w => w.IsCompleted).Select(w => w.LogDate.Date)
            );

            int totalScheduled = 0;
            int totalCompleted = 0;

            foreach (var plan in activePlans)
            {
                var prescribedDays = new HashSet<string>(
                    plan.WorkoutPlanExercises.Select(e => e.DayOfWeek),
                    StringComparer.OrdinalIgnoreCase
                );

                if (prescribedDays.Count == 0)
                {
                    continue;
                }

                DateTime windowStart = plan.StartDate.Date > start.Date ? plan.StartDate.Date : start.Date;
                DateTime windowEnd = plan.EndDate.Date < maxEligibleDate ? plan.EndDate.Date : maxEligibleDate;

                if (windowStart > windowEnd)
                {
                    continue;
                }

                for (DateTime d = windowStart; d <= windowEnd; d = d.AddDays(1))
                {
                    string dayName = d.DayOfWeek.ToString();
                    if (prescribedDays.Contains(dayName))
                    {
                        totalScheduled++;

                        if (completedDates.Contains(d))
                        {
                            totalCompleted++;
                        }
                    }
                }
            }

            metrics.ScheduledWorkoutsCount = totalScheduled;
            metrics.CompletedWorkoutsCount = totalCompleted;

            if (totalScheduled > 0)
            {
                metrics.CompletionRatePct = (int)Math.Min(100, Math.Max(0, Math.Round((double)totalCompleted / totalScheduled * 100)));
            }
            else
            {
                metrics.CompletionRatePct = null; // N/A
            }

            return metrics;
        }
    }
}
