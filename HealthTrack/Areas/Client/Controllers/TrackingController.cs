using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Client.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Client.Controllers
{
    [ClientAuthorize]
    public class TrackingController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // GET: /Client/Tracking
        [HttpGet]
        public ActionResult Index()
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            var client = db.Clients.FirstOrDefault(c => c.ClientId == clientId);
            if (client == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            var model = new ClientTrackingViewModel
            {
                ClientName = client.FullName
            };

            // 1. Water Intake for Today
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var todayLogs = db.ClientMealLogs
                .Where(m => m.ClientId == clientId && m.LogDate >= today && m.LogDate < tomorrow)
                .ToList();

            model.TodayWaterLogs = todayLogs.Where(m => m.WaterLiters.HasValue && m.WaterLiters.Value > 0).ToList();
            model.WaterConsumedTodayLiters = todayLogs.Sum(m => m.WaterLiters ?? 0m);

            // Fetch Daily Water Target (FR-C-32: Personalized client target from ClientPreferences, fallback to DietPlans)
            var preferences = db.ClientPreferences.FirstOrDefault(p => p.ClientId == clientId);
            int? personalTargetMl = preferences != null && preferences.DailyWaterTargetMl.HasValue && preferences.DailyWaterTargetMl.Value > 0
                ? preferences.DailyWaterTargetMl
                : (int?)null;

            var activeDiet = db.DietPlans.FirstOrDefault(d => d.ClientId == clientId && d.IsActive);
            decimal fallbackDietLiters = activeDiet != null && activeDiet.WaterIntakeLiters.HasValue ? activeDiet.WaterIntakeLiters.Value : 0m;

            if (personalTargetMl.HasValue)
            {
                model.HasPersonalWaterTarget = true;
                model.PersonalWaterTargetMl = personalTargetMl.Value;
                model.TargetWaterLiters = Math.Round((decimal)personalTargetMl.Value / 1000.0m, 2);
            }
            else if (fallbackDietLiters > 0)
            {
                model.HasPersonalWaterTarget = false;
                model.PersonalWaterTargetMl = (int)Math.Round(fallbackDietLiters * 1000);
                model.TargetWaterLiters = fallbackDietLiters;
            }
            else
            {
                model.HasPersonalWaterTarget = false;
                model.PersonalWaterTargetMl = null;
                model.TargetWaterLiters = 0m;
            }

            model.WaterAdherencePct = model.TargetWaterLiters > 0
                ? (int)Math.Min(100, Math.Round((model.WaterConsumedTodayLiters / model.TargetWaterLiters) * 100))
                : 0;

            // 2. 7-Day Water History
            var sevenDaysAgo = today.AddDays(-6);
            var recentMealLogs = db.ClientMealLogs
                .Where(m => m.ClientId == clientId && m.LogDate >= sevenDaysAgo && m.LogDate < tomorrow)
                .ToList();

            for (int i = 6; i >= 0; i--)
            {
                var d = today.AddDays(-i);
                var nextD = d.AddDays(1);
                var dayLogs = recentMealLogs.Where(m => m.LogDate >= d && m.LogDate < nextD).ToList();
                var waterSum = dayLogs.Sum(m => m.WaterLiters ?? 0m);

                int adh = model.TargetWaterLiters > 0
                    ? (int)Math.Min(100, Math.Round((waterSum / model.TargetWaterLiters) * 100))
                    : 0;

                model.WaterHistory.Add(new DailyWaterSummary
                {
                    Date = d,
                    WaterLiters = waterSum,
                    TargetMl = (int)Math.Round(model.TargetWaterLiters * 1000),
                    AdherencePct = adh
                });
            }

            // 3. Sleep Tracking Data
            var sleepList = db.ClientSleepLogs
                .Where(s => s.ClientId == clientId)
                .OrderByDescending(s => s.SleepDate)
                .Take(30)
                .ToList();
            sleepList.ForEach(HealthDataProtectionHelper.DecryptSleepLog);

            model.SleepHistory = sleepList;
            model.TotalSleepLoggedCount = sleepList.Count;
            model.TodaySleep = sleepList.FirstOrDefault(s => s.SleepDate == today || s.SleepDate == today.AddDays(-1));

            // FR-S-20: Audit hydration & sleep tracking view
            HealthAuditHelper.LogAccess(Session, clientId, "SleepLog", null, "View", true, "Viewed hydration and sleep health tracking hub");

            var last7Sleep = sleepList.Take(7).ToList();
            if (last7Sleep.Any())
            {
                model.AverageSleepHoursLast7Days = Math.Round(last7Sleep.Average(s => s.DurationHours), 1);
            }

            return View(model);
        }

        // POST: /Client/Tracking/LogWater
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogWater(decimal amountLiters = 0.25m)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            // Check if client has a daily water target (either personalized or from diet plan)
            var preferences = db.ClientPreferences.FirstOrDefault(p => p.ClientId == clientId);
            var activeDiet = db.DietPlans.FirstOrDefault(d => d.ClientId == clientId && d.IsActive);

            bool hasTarget = (preferences != null && preferences.DailyWaterTargetMl.HasValue && preferences.DailyWaterTargetMl.Value > 0)
                          || (activeDiet != null && activeDiet.WaterIntakeLiters.HasValue && activeDiet.WaterIntakeLiters.Value > 0);

            if (!hasTarget)
            {
                TempData["ErrorMessage"] = "Water logging is locked: Please set your daily water target first before logging hydration.";
                return RedirectToAction("Index");
            }

            if (amountLiters <= 0) amountLiters = 0.25m;

            var today = DateTime.Today;

            // 1. Add log to ClientMealLogs
            var mealLog = new ClientMealLogs
            {
                ClientId = clientId,
                DietPlanId = null,
                DietMealId = null,
                LogDate = DateTime.Now,
                MealType = "Hydration",
                MealName = "Water Intake",
                PortionSize = $"{Math.Round(amountLiters * 1000)} ml",
                Calories = 0,
                ProteinGrams = 0,
                CarbsGrams = 0,
                FatsGrams = 0,
                WaterLiters = amountLiters,
                IsCompleted = true,
                ClientNotes = "Quick Hydration Log",
                CreatedAt = DateTime.Now
            };
            db.ClientMealLogs.Add(mealLog);

            // 2. Sync to today's ClientProgressRecords
            var progress = db.ClientProgressRecords.FirstOrDefault(r => r.ClientId == clientId && r.RecordedDate == today);
            int addedMl = (int)Math.Round(amountLiters * 1000);
            if (progress != null)
            {
                progress.WaterIntakeMl += addedMl;
            }
            else
            {
                var latest = db.ClientProgressRecords
                    .Where(r => r.ClientId == clientId)
                    .OrderByDescending(r => r.RecordedDate)
                    .FirstOrDefault();

                var client = db.Clients.FirstOrDefault(c => c.ClientId == clientId);
                decimal resolvedHeight = latest != null && latest.HeightCm > 0 
                    ? latest.HeightCm 
                    : (client?.HeightCm ?? 0m);
                decimal resolvedWeight = latest != null ? latest.WeightKg : 0m;
                decimal resolvedBmi = 0m;

                if (resolvedWeight > 0 && resolvedHeight > 0)
                {
                    decimal hMeters = resolvedHeight / 100.0m;
                    resolvedBmi = Math.Round(resolvedWeight / (hMeters * hMeters), 1);
                }

                db.ClientProgressRecords.Add(new ClientProgressRecords
                {
                    ClientId = clientId,
                    RecordedDate = today,
                    WeightKg = resolvedWeight,
                    HeightCm = resolvedHeight,
                    Bmi = resolvedBmi,
                    CaloriesConsumed = 0,
                    CaloriesBurned = 0,
                    WaterIntakeMl = addedMl,
                    Notes = HealthDataProtectionHelper.Encrypt("Quick Water Check-in"),
                    CreatedAt = DateTime.Now
                });
            }

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Logged +{addedMl} ml of water successfully!";
            return RedirectToAction("Index");
        }

        // POST: /Client/Tracking/LogSleep
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogSleep(LogSleepInputModel input)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            if (input.WakeTime <= input.BedTime)
            {
                TempData["ErrorMessage"] = "Wake-up time must be after bedtime.";
                return RedirectToAction("Index");
            }

            // Calculate duration in hours
            var diff = input.WakeTime - input.BedTime;
            decimal duration = Math.Round((decimal)diff.TotalHours, 2);

            var sleepLog = new ClientSleepLogs
            {
                ClientId = clientId,
                SleepDate = input.SleepDate,
                BedTime = input.BedTime,
                WakeTime = input.WakeTime,
                DurationHours = duration,
                QualityRating = input.QualityRating,
                // FR-S-14: Encrypt sensitive sleep notes
                Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : HealthDataProtectionHelper.Encrypt(input.Notes.Trim()),
                CreatedAt = DateTime.Now
            };

            db.ClientSleepLogs.Add(sleepLog);
            db.SaveChanges();

            // FR-S-20: Audit sleep health log creation
            HealthAuditHelper.LogAccess(Session, clientId, "SleepLog", sleepLog.SleepLogId.ToString(), "Create", true, $"Logged {duration} hours of sleep for {input.SleepDate:yyyy-MM-dd}");

            TempData["SuccessMessage"] = $"Logged {duration} hours of sleep for {input.SleepDate:MMM dd} successfully!";
            return RedirectToAction("Index");
        }

        // POST: /Client/Tracking/DeleteSleep
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteSleep(int sleepLogId)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            var sleep = db.ClientSleepLogs.FirstOrDefault(s => s.SleepLogId == sleepLogId && s.ClientId == clientId);
            if (sleep != null)
            {
                db.ClientSleepLogs.Remove(sleep);
                db.SaveChanges();

                // FR-S-20: Audit sleep record deletion
                HealthAuditHelper.LogAccess(Session, clientId, "SleepLog", sleepLogId.ToString(), "Delete", true, $"Deleted sleep record {sleepLogId}");

                TempData["SuccessMessage"] = "Sleep record removed.";
            }

            return RedirectToAction("Index");
        }

        // POST: /Client/Tracking/SetWaterTarget
        // FR-C-32: Set and update personalized daily water-intake target
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetWaterTarget(SetWaterTargetInputModel input)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => !string.IsNullOrEmpty(e.ErrorMessage) ? e.ErrorMessage : e.Exception?.Message)
                    .Where(m => !string.IsNullOrEmpty(m))
                    .ToList();

                TempData["ErrorMessage"] = errors.Any()
                    ? string.Join(" ", errors)
                    : "Please enter a valid daily water target between 500 ml and 10,000 ml.";
                return RedirectToAction("Index");
            }

            if (input.DailyWaterTargetMl < 500 || input.DailyWaterTargetMl > 10000)
            {
                TempData["ErrorMessage"] = "Daily water target must be between 500 ml and 10,000 ml.";
                return RedirectToAction("Index");
            }

            var preferences = db.ClientPreferences.FirstOrDefault(p => p.ClientId == clientId);
            bool isUpdate = preferences != null && preferences.DailyWaterTargetMl.HasValue && preferences.DailyWaterTargetMl.Value > 0;

            if (preferences == null)
            {
                preferences = new ClientPreferences
                {
                    ClientId = clientId,
                    PrimaryGoal = "General Health",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    DailyWaterTargetMl = input.DailyWaterTargetMl
                };
                db.ClientPreferences.Add(preferences);
            }
            else
            {
                preferences.DailyWaterTargetMl = input.DailyWaterTargetMl;
                preferences.UpdatedAt = DateTime.UtcNow;
            }

            db.SaveChanges();

            decimal targetLiters = Math.Round((decimal)input.DailyWaterTargetMl / 1000.0m, 2);
            TempData["SuccessMessage"] = isUpdate
                ? $"Daily water target updated to {input.DailyWaterTargetMl:N0} ml ({targetLiters:F1} L) successfully!"
                : $"Daily water target set to {input.DailyWaterTargetMl:N0} ml ({targetLiters:F1} L) successfully!";

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
