using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Globalization;
using System.Linq;
using HealthTrack.Models;

namespace HealthTrack.Helpers
{
    /// <summary>
    /// Result summary returned by the reminder execution worker.
    /// </summary>
    public class ReminderExecutionResult
    {
        public DateTime ExecutionTimeUtc { get; set; } = DateTime.UtcNow;
        public DateTime ReferenceLocalTime { get; set; }
        public int WorkoutRemindersGenerated { get; set; }
        public int MealRemindersGenerated { get; set; }
        public int AppointmentRemindersGenerated { get; set; }
        public int TotalRemindersGenerated => WorkoutRemindersGenerated + MealRemindersGenerated + AppointmentRemindersGenerated;
        public List<string> Logs { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Reusable reminder service for automated processing of:
    /// - FR-S-26: Workout reminders 30 minutes before scheduled workouts.
    /// - FR-S-27: Meal reminders 30 minutes before scheduled meals.
    /// - FR-S-28: Appointment reminders 24 hours and 1 hour before confirmed consultations.
    /// </summary>
    public static class ReminderService
    {
        /// <summary>
        /// Main entry point to process all due reminders across workouts, meals, and appointments.
        /// Can be safely invoked by Windows Task Scheduler or background workers.
        /// </summary>
        /// <param name="referenceNow">Optional simulated local time (defaults to DateTime.Now)</param>
        public static ReminderExecutionResult ProcessAllDueReminders(DateTime? referenceNow = null)
        {
            var nowLocal = referenceNow ?? DateTime.Now;
            var result = new ReminderExecutionResult
            {
                ReferenceLocalTime = nowLocal
            };

            using (var db = new HealthTrackEntities())
            {
                // Disable auto-detect changes during batch reads for optimal performance
                db.Configuration.AutoDetectChangesEnabled = true;

                // 1. Process Workout Reminders (FR-S-26)
                try
                {
                    result.WorkoutRemindersGenerated = ProcessWorkoutReminders(db, nowLocal, result.Logs);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Workout Reminders Error: {ex.Message}");
                }

                // 2. Process Meal Reminders (FR-S-27)
                try
                {
                    result.MealRemindersGenerated = ProcessMealReminders(db, nowLocal, result.Logs);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Meal Reminders Error: {ex.Message}");
                }

                // 3. Process Appointment Reminders (FR-S-28)
                try
                {
                    result.AppointmentRemindersGenerated = ProcessAppointmentReminders(db, nowLocal, result.Logs);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Appointment Reminders Error: {ex.Message}");
                }
            }

            return result;
        }

        #region FR-S-26: Automated Workout Reminders

        /// <summary>
        /// FR-S-26: Generates workout reminders 30 minutes before scheduled client workout start.
        /// - Uses configured WorkoutPlans.ScheduledStartTime.
        /// - Skips plans without a scheduled start time.
        /// - Skips inactive plans, expired plans, or plans not scheduled for today's DayOfWeek.
        /// - Skips workouts already completed today in ClientWorkoutLogs.
        /// - Enforces narrow 30-minute delivery window [Start - 30m, Start].
        /// - Uses deterministic key wk_remind_{planId}_{yyyyMMdd} for reliable deduplication.
        /// </summary>
        public static int ProcessWorkoutReminders(HealthTrackEntities db, DateTime nowLocal, List<string> logs = null)
        {
            int remindersCreated = 0;
            var today = nowLocal.Date;
            var tomorrow = today.AddDays(1);
            string todayDayOfWeek = today.DayOfWeek.ToString(); // e.g. "Monday", "Tuesday"

            // Active workout plans within date range where client is active and a start time is configured
            var activePlans = db.WorkoutPlans
                .Include(p => p.Clients)
                .Include(p => p.WorkoutPlanExercises)
                .Where(p => p.IsActive 
                         && p.ScheduledStartTime.HasValue
                         && p.StartDate <= today 
                         && p.EndDate >= today
                         && p.Clients.IsActive)
                .ToList();

            foreach (var plan in activePlans)
            {
                // 1. Verify plan has exercises prescribed for today's day of week
                bool hasExerciseToday = plan.WorkoutPlanExercises.Any(e => 
                    string.Equals(e.DayOfWeek, todayDayOfWeek, StringComparison.OrdinalIgnoreCase));

                if (!hasExerciseToday)
                {
                    continue; // Rest day for this plan; no reminder needed
                }

                var scheduledStartTime = plan.ScheduledStartTime.Value;
                var targetWorkoutDateTime = today.Add(scheduledStartTime);

                // 2. Narrow delivery window: 30 minutes before workout until the workout start time
                // Reminder window: [Target - 30 mins, Target]
                var windowStart = targetWorkoutDateTime.AddMinutes(-30);
                var windowEnd = targetWorkoutDateTime;

                if (nowLocal < windowStart || nowLocal > windowEnd)
                {
                    // Outside delivery window (too early, or outdated/past start time)
                    continue;
                }

                // 3. Skip if already completed today in ClientWorkoutLogs
                bool alreadyCompleted = db.ClientWorkoutLogs.Any(l => 
                    l.ClientId == plan.ClientId 
                    && l.WorkoutPlanId == plan.WorkoutPlanId 
                    && l.LogDate >= today 
                    && l.LogDate < tomorrow 
                    && l.IsCompleted);

                if (alreadyCompleted)
                {
                    continue;
                }

                // 4. Deterministic key for deduplication
                string notificationKey = $"wk_remind_{plan.WorkoutPlanId}_{today:yyyyMMdd}";

                // 5. Check if notification already generated
                bool alreadyNotified = db.UserNotifications.Any(n => 
                    n.RecipientRole == "Client" 
                    && n.RecipientUserId == plan.ClientId 
                    && n.NotificationKey == notificationKey);

                if (alreadyNotified)
                {
                    continue;
                }

                // 6. Create reminder notification
                string timeDisplay = DateTime.Today.Add(scheduledStartTime).ToString("hh:mm tt");
                var notification = new UserNotifications
                {
                    RecipientUserId = plan.ClientId,
                    RecipientRole = "Client",
                    NotificationKey = notificationKey,
                    Title = "Upcoming Workout Reminder 🏋️",
                    Message = $"Your scheduled workout routine '{plan.Title}' starts at {timeDisplay} (in 30 minutes). Get ready to train!",
                    Category = "Workouts",
                    NotificationType = "Reminder",
                    ActionUrl = "/Client/Workout",
                    Icon = "bi-lightning-charge-fill",
                    Color = "warning",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                db.UserNotifications.Add(notification);
                try
                {
                    db.SaveChanges();
                    remindersCreated++;
                    logs?.Add($"[FR-S-26] Generated workout reminder for Client {plan.ClientId} (Plan #{plan.WorkoutPlanId}) at {timeDisplay}.");
                }
                catch (DbUpdateException)
                {
                    // Concurrent execution deduplication collision; safely ignored
                    db.Entry(notification).State = EntityState.Detached;
                }
            }

            return remindersCreated;
        }

        #endregion

        #region FR-S-27: Automated Meal Reminders

        /// <summary>
        /// FR-S-27: Generates meal reminders 30 minutes before scheduled meals.
        /// - Safely parses existing DietMeals.ScheduledTime string values.
        /// - Skips meals without valid or parseable scheduled times.
        /// - Skips meals on non-matching days of the week.
        /// - Skips meals already logged today in ClientMealLogs.
        /// - Enforces narrow 30-minute delivery window [MealTime - 30m, MealTime].
        /// - Uses deterministic key meal_remind_{mealId}_{yyyyMMdd} for reliable deduplication.
        /// </summary>
        public static int ProcessMealReminders(HealthTrackEntities db, DateTime nowLocal, List<string> logs = null)
        {
            int remindersCreated = 0;
            var today = nowLocal.Date;
            var tomorrow = today.AddDays(1);
            string todayDayOfWeek = today.DayOfWeek.ToString(); // e.g. "Monday"

            // Active diet plans within date range where client is active
            var activePlans = db.DietPlans
                .Include(p => p.Clients)
                .Include(p => p.DietMeals)
                .Where(p => p.IsActive 
                         && p.StartDate <= today 
                         && p.EndDate >= today
                         && p.Clients.IsActive)
                .ToList();

            foreach (var plan in activePlans)
            {
                // Find meals scheduled for today's DayOfWeek
                var todayMeals = plan.DietMeals
                    .Where(m => string.Equals(m.DayOfWeek, todayDayOfWeek, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var meal in todayMeals)
                {
                    // 1. Safely parse scheduled time
                    TimeSpan? mealTime = ParseMealTime(meal.ScheduledTime);
                    if (!mealTime.HasValue)
                    {
                        // Unparseable, "Flexible", empty, or null; safely skip
                        continue;
                    }

                    var targetMealDateTime = today.Add(mealTime.Value);

                    // 2. Narrow delivery window: 30 minutes before meal until scheduled meal time
                    var windowStart = targetMealDateTime.AddMinutes(-30);
                    var windowEnd = targetMealDateTime;

                    if (nowLocal < windowStart || nowLocal > windowEnd)
                    {
                        // Outside narrow window (too early or outdated)
                        continue;
                    }

                    // 3. Skip if already logged today in ClientMealLogs
                    bool alreadyLogged = db.ClientMealLogs.Any(ml => 
                        ml.ClientId == plan.ClientId 
                        && ml.DietMealId == meal.DietMealId 
                        && ml.LogDate >= today 
                        && ml.LogDate < tomorrow);

                    if (alreadyLogged)
                    {
                        continue;
                    }

                    // 4. Deterministic key for deduplication
                    string notificationKey = $"meal_remind_{meal.DietMealId}_{today:yyyyMMdd}";

                    // 5. Check if notification already generated
                    bool alreadyNotified = db.UserNotifications.Any(n => 
                        n.RecipientRole == "Client" 
                        && n.RecipientUserId == plan.ClientId 
                        && n.NotificationKey == notificationKey);

                    if (alreadyNotified)
                    {
                        continue;
                    }

                    // 6. Create reminder notification
                    string timeDisplay = DateTime.Today.Add(mealTime.Value).ToString("hh:mm tt");
                    string mealLabel = !string.IsNullOrWhiteSpace(meal.MealName) ? $"{meal.MealType} ('{meal.MealName}')" : meal.MealType;
                    var notification = new UserNotifications
                    {
                        RecipientUserId = plan.ClientId,
                        RecipientRole = "Client",
                        NotificationKey = notificationKey,
                        Title = "Upcoming Meal Reminder 🥗",
                        Message = $"Time for your scheduled {mealLabel} at {timeDisplay} (in 30 minutes). Calorie target: {meal.Calories} kcal.",
                        Category = "Diet",
                        NotificationType = "Reminder",
                        ActionUrl = "/Client/Diet",
                        Icon = "bi-egg-fried",
                        Color = "success",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    db.UserNotifications.Add(notification);
                    try
                    {
                        db.SaveChanges();
                        remindersCreated++;
                        logs?.Add($"[FR-S-27] Generated meal reminder for Client {plan.ClientId} (Meal #{meal.DietMealId} - {mealLabel}) at {timeDisplay}.");
                    }
                    catch (DbUpdateException)
                    {
                        // Concurrent execution deduplication collision; safely ignored
                        db.Entry(notification).State = EntityState.Detached;
                    }
                }
            }

            return remindersCreated;
        }

        /// <summary>
        /// Safely parses meal time string values such as "08:00 AM", "8:00 AM", "12:30 PM", "18:00", "8:00".
        /// Returns null if null, empty, "Flexible", or unparseable.
        /// </summary>
        public static TimeSpan? ParseMealTime(string rawTime)
        {
            if (string.IsNullOrWhiteSpace(rawTime))
            {
                return null;
            }

            var trimmed = rawTime.Trim();

            if (trimmed.Equals("flexible", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("anytime", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("n/a", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Standard 12-hour and 24-hour formats
            string[] formats = new[]
            {
                "h:mm tt", "hh:mm tt", "h:mmtt", "hh:mmtt",
                "H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss",
                "h:mm:ss tt", "hh:mm:ss tt"
            };

            if (DateTime.TryParseExact(trimmed, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDt))
            {
                return parsedDt.TimeOfDay;
            }

            // General fallback TryParse
            if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDt))
            {
                return parsedDt.TimeOfDay;
            }

            if (TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out var parsedSpan))
            {
                return parsedSpan;
            }

            return null;
        }

        #endregion

        #region FR-S-28: Appointment Reminders

        /// <summary>
        /// FR-S-28: Generates appointment reminders 24 hours and 1 hour before confirmed appointments.
        /// - Only reminds for confirmed appointments (Status == "Approved").
        /// - Does not remind for Cancelled, Rejected, or Completed appointments.
        /// - Supports FR-C-45 and FR-T-38 rescheduling by including AppointmentDate and StartTime in notification keys.
        /// - Enforces narrow delivery windows:
        ///     24h Reminder: [Target - 24h, Target - 2h]
        ///     1h Reminder:  [Target - 60m, Target]
        /// </summary>
        public static int ProcessAppointmentReminders(HealthTrackEntities db, DateTime nowLocal, List<string> logs = null)
        {
            int remindersCreated = 0;

            // Only check future or today's confirmed appointments
            var searchFromDate = nowLocal.Date;
            var searchToDate = nowLocal.Date.AddDays(2); // Look up to 48 hours ahead

            var confirmedAppointments = db.Appointments
                .Include(a => a.Clients)
                .Include(a => a.Trainers)
                .Where(a => a.Status == "Approved" 
                         && a.AppointmentDate >= searchFromDate 
                         && a.AppointmentDate <= searchToDate
                         && a.Clients.IsActive)
                .ToList();

            foreach (var apt in confirmedAppointments)
            {
                var aptDateTime = apt.AppointmentDate.Date.Add(apt.StartTime);
                string trainerName = apt.Trainers != null ? apt.Trainers.FullName : "your coach";
                string timeDisplay = DateTime.Today.Add(apt.StartTime).ToString("hh:mm tt");
                string dateDisplay = apt.AppointmentDate.ToString("MMM dd, yyyy");
                string location = !string.IsNullOrWhiteSpace(apt.MeetingLocation) ? apt.MeetingLocation : "Online Consultation";

                // Unique schedule signature incorporates current confirmed date and time
                // This ensures if an appointment is rescheduled to a new date/time, fresh reminders fire for the new slot
                string scheduleSig = $"{apt.AppointmentDate:yyyyMMdd}_{apt.StartTime.Hours:D2}{apt.StartTime.Minutes:D2}";

                // -------------------------------------------------------------
                // 1. 24-Hour Reminder Window
                // Target: aptDateTime - 24 hours.
                // Delivery window: [aptDateTime - 24h, aptDateTime - 2h]
                // -------------------------------------------------------------
                var window24hStart = aptDateTime.AddHours(-24);
                var window24hEnd = aptDateTime.AddHours(-2);

                if (nowLocal >= window24hStart && nowLocal <= window24hEnd)
                {
                    string key24h = $"apt_remind_{apt.AppointmentId}_{scheduleSig}_24h";

                    bool alreadyNotified24h = db.UserNotifications.Any(n => 
                        n.RecipientRole == "Client" 
                        && n.RecipientUserId == apt.ClientId 
                        && n.NotificationKey == key24h);

                    if (!alreadyNotified24h)
                    {
                        var notif24h = new UserNotifications
                        {
                            RecipientUserId = apt.ClientId,
                            RecipientRole = "Client",
                            NotificationKey = key24h,
                            Title = "Upcoming Consultation Reminder (24h) 📅",
                            Message = $"Reminder: You have a confirmed consultation '{apt.Title}' scheduled for tomorrow ({dateDisplay}) at {timeDisplay} with {trainerName}.",
                            Category = "Appointments",
                            NotificationType = "Reminder",
                            ActionUrl = "/Client/Appointments",
                            Icon = "bi-calendar2-check-fill",
                            Color = "info",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        db.UserNotifications.Add(notif24h);
                        try
                        {
                            db.SaveChanges();
                            remindersCreated++;
                            logs?.Add($"[FR-S-28] Generated 24h appointment reminder for Client {apt.ClientId} (Appointment #{apt.AppointmentId}) scheduled {dateDisplay} at {timeDisplay}.");
                        }
                        catch (DbUpdateException)
                        {
                            db.Entry(notif24h).State = EntityState.Detached;
                        }
                    }
                }

                // -------------------------------------------------------------
                // 2. 1-Hour Reminder Window
                // Target: aptDateTime - 1 hour (60 minutes).
                // Delivery window: [aptDateTime - 60m, aptDateTime]
                // -------------------------------------------------------------
                var window1hStart = aptDateTime.AddMinutes(-60);
                var window1hEnd = aptDateTime;

                if (nowLocal >= window1hStart && nowLocal <= window1hEnd)
                {
                    string key1h = $"apt_remind_{apt.AppointmentId}_{scheduleSig}_1h";

                    bool alreadyNotified1h = db.UserNotifications.Any(n => 
                        n.RecipientRole == "Client" 
                        && n.RecipientUserId == apt.ClientId 
                        && n.NotificationKey == key1h);

                    if (!alreadyNotified1h)
                    {
                        var notif1h = new UserNotifications
                        {
                            RecipientUserId = apt.ClientId,
                            RecipientRole = "Client",
                            NotificationKey = key1h,
                            Title = "Upcoming Consultation Reminder (1h) ⏰",
                            Message = $"Reminder: Your consultation '{apt.Title}' with {trainerName} starts in 1 hour at {timeDisplay}. Location: {location}.",
                            Category = "Appointments",
                            NotificationType = "Reminder",
                            ActionUrl = "/Client/Appointments",
                            Icon = "bi-alarm-fill",
                            Color = "warning",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        db.UserNotifications.Add(notif1h);
                        try
                        {
                            db.SaveChanges();
                            remindersCreated++;
                            logs?.Add($"[FR-S-28] Generated 1h appointment reminder for Client {apt.ClientId} (Appointment #{apt.AppointmentId}) starting at {timeDisplay}.");
                        }
                        catch (DbUpdateException)
                        {
                            db.Entry(notif1h).State = EntityState.Detached;
                        }
                    }
                }
            }

            return remindersCreated;
        }

        #endregion
    }
}
