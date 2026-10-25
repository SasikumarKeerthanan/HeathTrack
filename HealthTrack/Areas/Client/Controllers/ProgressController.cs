using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Client.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Client.Controllers
{
    [ClientAuthorize]
    public class ProgressController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        private static readonly string[] OrderedDaysOfWeek = new[]
        {
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
        };

        // Resolves report date boundaries according to selected preset or custom range (FR-C-62)
        private static Tuple<DateTime, DateTime, string> ResolveDateRange(string preset, DateTime? fromDate, DateTime? toDate)
        {
            if (fromDate.HasValue || toDate.HasValue)
            {
                preset = "custom";
            }

            var activePreset = string.IsNullOrEmpty(preset) ? "30days" : preset.ToLower();

            DateTime start;
            DateTime end;

            switch (activePreset)
            {
                case "7days":
                    start = DateTime.Today.AddDays(-7);
                    end = DateTime.Today.AddDays(1).AddTicks(-1);
                    break;
                case "30days":
                    start = DateTime.Today.AddDays(-30);
                    end = DateTime.Today.AddDays(1).AddTicks(-1);
                    break;
                case "90days":
                    start = DateTime.Today.AddDays(-90);
                    end = DateTime.Today.AddDays(1).AddTicks(-1);
                    break;
                case "1year":
                    start = DateTime.Today.AddDays(-365);
                    end = DateTime.Today.AddDays(1).AddTicks(-1);
                    break;
                case "all":
                    start = new DateTime(2000, 1, 1);
                    end = DateTime.Today.AddDays(1).AddTicks(-1);
                    break;
                case "custom":
                default:
                    if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
                    {
                        var temp = fromDate;
                        fromDate = toDate;
                        toDate = temp;
                    }
                    start = fromDate.HasValue ? fromDate.Value.Date : DateTime.Today.AddDays(-30);
                    end = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : DateTime.Today.AddDays(1).AddTicks(-1);
                    activePreset = "custom";
                    break;
            }

            return Tuple.Create(start, end, activePreset);
        }

        // Assembles client progress metrics, workout adherence, and prescribed nutrition from database
        private ClientProgressReportViewModel BuildReportModel(string preset, DateTime? fromDate, DateTime? toDate)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return new ClientProgressReportViewModel();
            }

            var client = db.Clients
                .Include(c => c.Trainers)
                .Include(c => c.ClientHealthProfile)
                .Include(c => c.ClientPreferences)
                .FirstOrDefault(c => c.ClientId == clientId);

            if (client == null)
            {
                return new ClientProgressReportViewModel { ClientId = clientId };
            }

            var range = ResolveDateRange(preset, fromDate, toDate);
            DateTime start = range.Item1;
            DateTime end = range.Item2;
            string activePreset = range.Item3;

            var healthProfile = client.ClientHealthProfile?.FirstOrDefault();
            HealthDataProtectionHelper.DecryptProfile(healthProfile);
            var preferences = client.ClientPreferences?.FirstOrDefault();
            var trainer = client.Trainers;

            // Biometric measurements and progression history (strictly filtered to requested date range)
            var progressRecords = db.ClientProgressRecords
                .Where(r => r.ClientId == clientId && r.RecordedDate >= start && r.RecordedDate <= end)
                .OrderBy(r => r.RecordedDate)
                .ToList();
            progressRecords.ForEach(HealthDataProtectionHelper.DecryptProgressRecord);

            var allTimeRecords = db.ClientProgressRecords
                .Where(r => r.ClientId == clientId)
                .OrderBy(r => r.RecordedDate)
                .ToList();
            allTimeRecords.ForEach(HealthDataProtectionHelper.DecryptProgressRecord);

            var firstRec = allTimeRecords.FirstOrDefault();
            var latestRec = allTimeRecords.LastOrDefault();

            // Client Goals
            var goalEntities = db.ClientGoals
                .Where(g => g.ClientId == clientId)
                .OrderBy(g => g.IsAchieved)
                .ThenByDescending(g => g.CreatedAt)
                .ToList();

            var goals = goalEntities.Select(g =>
            {
                double rangeSpan = Math.Abs((double)g.TargetValue - (double)g.BaselineValue);
                int pct = 0;
                if (g.IsAchieved)
                {
                    pct = 100;
                }
                else if (rangeSpan > 0)
                {
                    double progress = Math.Abs((double)g.CurrentValue - (double)g.BaselineValue);
                    pct = Math.Min(100, Math.Max(0, (int)Math.Round(progress / rangeSpan * 100)));
                }

                return new ProgressGoalMetricDto
                {
                    GoalName = g.GoalName,
                    Category = g.Category,
                    BaselineValue = (double)g.BaselineValue,
                    CurrentValue = (double)g.CurrentValue,
                    TargetValue = (double)g.TargetValue,
                    ProgressPercentage = pct,
                    Unit = g.Unit ?? "",
                    StatusMessage = g.StatusMessage ?? (g.IsAchieved ? "Goal Completed" : "In Progress"),
                    IsAchieved = g.IsAchieved
                };
            }).ToList();

            var weightGoal = goals.FirstOrDefault(g => g.Category.Equals("Weight", StringComparison.OrdinalIgnoreCase));

            // Weight and height evaluation
            double currentWeight = 0;
            if (latestRec != null && latestRec.WeightKg > 0)
            {
                currentWeight = (double)latestRec.WeightKg;
            }
            else if (weightGoal != null && weightGoal.CurrentValue > 0)
            {
                currentWeight = weightGoal.CurrentValue;
            }

            double baselineWeight = firstRec != null && firstRec.WeightKg > 0
                ? (double)firstRec.WeightKg
                : (weightGoal != null && weightGoal.BaselineValue > 0 ? weightGoal.BaselineValue : currentWeight);

            double targetWeight = weightGoal != null ? weightGoal.TargetValue : currentWeight;

            double heightCm = 0;
            if (latestRec != null && latestRec.HeightCm > 0)
            {
                heightCm = (double)latestRec.HeightCm;
            }
            else if (client.HeightCm.HasValue && client.HeightCm.Value > 0)
            {
                heightCm = (double)client.HeightCm.Value;
            }
            else if (healthProfile != null && healthProfile.BaselineHeightCm > 0)
            {
                heightCm = (double)healthProfile.BaselineHeightCm;
            }

            // BMI calculation based on real recorded metrics
            double currentBmi = 0;
            if (latestRec != null && latestRec.Bmi.HasValue && latestRec.Bmi.Value > 0)
            {
                currentBmi = (double)latestRec.Bmi.Value;
            }
            else if (heightCm > 0 && currentWeight > 0)
            {
                double hMeters = heightCm / 100.0;
                currentBmi = Math.Round(currentWeight / (hMeters * hMeters), 1);
            }

            double baselineBmi = 0;
            if (firstRec != null && firstRec.Bmi.HasValue && firstRec.Bmi.Value > 0)
            {
                baselineBmi = (double)firstRec.Bmi.Value;
            }
            else if (heightCm > 0 && baselineWeight > 0)
            {
                double hMeters = heightCm / 100.0;
                baselineBmi = Math.Round(baselineWeight / (hMeters * hMeters), 1);
            }

            // Workout session logs and adherence
            var workoutLogs = db.ClientWorkoutLogs
                .Include(wl => wl.WorkoutPlans)
                .Where(wl => wl.ClientId == clientId && wl.LogDate >= start && wl.LogDate <= end)
                .OrderByDescending(wl => wl.LogDate)
                .ToList();

            if (!workoutLogs.Any())
            {
                workoutLogs = db.ClientWorkoutLogs
                    .Include(wl => wl.WorkoutPlans)
                    .Where(wl => wl.ClientId == clientId)
                    .OrderByDescending(wl => wl.LogDate)
                    .Take(15)
                    .ToList();
            }

            int completedWorkouts = workoutLogs.Count(w => w.IsCompleted);
            int scheduledWorkouts = Math.Max(completedWorkouts, workoutLogs.Count);
            int totalExerciseMinutes = workoutLogs.Where(w => w.IsCompleted).Sum(w => w.DurationMinutes);
            int totalCaloriesBurned = workoutLogs.Where(w => w.IsCompleted).Sum(w => w.CaloriesBurned);

            // Prescribed Workout Routine
            var activeWorkoutPlan = db.WorkoutPlans
                .Include(p => p.WorkoutPlanExercises.Select(wpe => wpe.Exercises))
                .Where(p => p.ClientId == clientId && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (activeWorkoutPlan == null)
            {
                activeWorkoutPlan = db.WorkoutPlans
                    .Include(p => p.WorkoutPlanExercises.Select(wpe => wpe.Exercises))
                    .Where(p => p.ClientId == clientId)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();
            }

            var workoutDays = new List<WorkoutPlanDayDto>();
            if (activeWorkoutPlan != null && activeWorkoutPlan.WorkoutPlanExercises != null)
            {
                var groupedExercises = activeWorkoutPlan.WorkoutPlanExercises
                    .GroupBy(e => e.DayOfWeek)
                    .ToDictionary(g => g.Key, g => g.OrderBy(e => e.OrderIndex).ToList(), StringComparer.OrdinalIgnoreCase);

                foreach (var dayName in OrderedDaysOfWeek)
                {
                    if (groupedExercises.ContainsKey(dayName))
                    {
                        var exList = groupedExercises[dayName];
                        var mappedEx = exList.Select(e => new WorkoutPlanExerciseDto
                        {
                            Name = e.Exercises != null ? e.Exercises.Name : "Exercise",
                            Category = e.Exercises != null ? e.Exercises.Category : "General",
                            TargetMuscle = e.Exercises != null ? e.Exercises.TargetMuscle : "Full Body",
                            Equipment = e.Exercises != null ? e.Exercises.Equipment : "Standard",
                            Sets = e.Sets,
                            Reps = e.Reps,
                            DurationMinutes = e.DurationMinutes,
                            RestSeconds = e.RestSeconds > 0 ? e.RestSeconds : 60,
                            Instructions = !string.IsNullOrWhiteSpace(e.Instructions) ? e.Instructions : (e.Exercises != null ? e.Exercises.Instructions : "")
                        }).ToList();

                        workoutDays.Add(new WorkoutPlanDayDto
                        {
                            DayOfWeek = dayName,
                            FocusTitle = string.Join(" / ", mappedEx.Select(m => m.TargetMuscle).Distinct().Take(2)),
                            TotalExercises = mappedEx.Count,
                            EstimatedMinutes = mappedEx.Sum(m => (m.DurationMinutes ?? 2) + (m.Sets * (m.RestSeconds / 60))),
                            Exercises = mappedEx
                        });
                    }
                }
            }

            // Prescribed Diet Plan and Meal Schedule
            var activeDietPlan = db.DietPlans
                .Include(p => p.DietMeals)
                .Include(p => p.Trainers)
                .Where(p => p.ClientId == clientId && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (activeDietPlan == null)
            {
                activeDietPlan = db.DietPlans
                    .Include(p => p.DietMeals)
                    .Include(p => p.Trainers)
                    .Where(p => p.ClientId == clientId)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();
            }

            var scheduledMeals = new List<DietPlanMealDto>();
            if (activeDietPlan != null && activeDietPlan.DietMeals != null)
            {
                scheduledMeals = activeDietPlan.DietMeals
                    .OrderBy(m => m.OrderIndex)
                    .ThenBy(m => m.DietMealId)
                    .Select(m => new DietPlanMealDto
                    {
                        DayOfWeek = !string.IsNullOrWhiteSpace(m.DayOfWeek) ? m.DayOfWeek : "Daily",
                        MealType = m.MealType ?? "Meal",
                        ScheduledTime = !string.IsNullOrWhiteSpace(m.ScheduledTime) ? m.ScheduledTime : "Flexible",
                        MealName = m.MealName ?? "Meal",
                        IngredientsAndPortion = !string.IsNullOrWhiteSpace(m.IngredientsAndPortion) ? m.IngredientsAndPortion : m.Ingredients,
                        Calories = m.Calories,
                        ProteinGrams = m.ProteinGrams ?? 0,
                        CarbsGrams = m.CarbsGrams ?? 0,
                        FatsGrams = m.FatsGrams ?? 0,
                        Instructions = m.Instructions ?? ""
                    }).ToList();
            }

            int dailyCalTarget = activeDietPlan != null ? activeDietPlan.DailyCalorieTarget : 0;
            double prescribedWater = activeDietPlan != null && activeDietPlan.WaterIntakeLiters.HasValue ? (double)activeDietPlan.WaterIntakeLiters.Value : 0.0;

            // Compute actual calorie and hydration averages from real logs
            int avgCaloriesConsumed = 0;
            if (progressRecords.Any(r => r.CaloriesConsumed > 0))
            {
                avgCaloriesConsumed = (int)Math.Round(progressRecords.Where(r => r.CaloriesConsumed > 0).Average(r => r.CaloriesConsumed));
            }

            double avgWaterConsumed = 0;
            var clientMealLogs = db.ClientMealLogs
                .Where(w => w.ClientId == clientId && w.LogDate >= start && w.LogDate <= end)
                .ToList();

            if (clientMealLogs.Any(w => w.WaterLiters.HasValue && w.WaterLiters.Value > 0))
            {
                avgWaterConsumed = Math.Round((double)clientMealLogs.Where(w => w.WaterLiters.HasValue && w.WaterLiters.Value > 0).Average(w => w.WaterLiters.Value), 1);
            }

            if (avgCaloriesConsumed == 0 && clientMealLogs.Any(m => m.Calories > 0))
            {
                avgCaloriesConsumed = (int)Math.Round(clientMealLogs.GroupBy(m => m.LogDate.Date).Average(g => g.Sum(m => m.Calories)));
            }

            // Real macro averages computed from logged meals
            int avgProtein = clientMealLogs.Any(m => m.ProteinGrams.HasValue && m.ProteinGrams.Value > 0)
                ? (int)Math.Round(clientMealLogs.GroupBy(m => m.LogDate.Date).Average(g => g.Sum(m => m.ProteinGrams ?? 0)))
                : 0;
            int avgCarbs = clientMealLogs.Any(m => m.CarbsGrams.HasValue && m.CarbsGrams.Value > 0)
                ? (int)Math.Round(clientMealLogs.GroupBy(m => m.LogDate.Date).Average(g => g.Sum(m => m.CarbsGrams ?? 0)))
                : 0;
            int avgFats = clientMealLogs.Any(m => m.FatsGrams.HasValue && m.FatsGrams.Value > 0)
                ? (int)Math.Round(clientMealLogs.GroupBy(m => m.LogDate.Date).Average(g => g.Sum(m => m.FatsGrams ?? 0)))
                : 0;

            // Clinical Evaluation & Next Phase Guidance (taken directly from assigned plan instructions)
            string evalNotes = activeWorkoutPlan != null && !string.IsNullOrWhiteSpace(activeWorkoutPlan.TrainerNotes)
                ? activeWorkoutPlan.TrainerNotes
                : (activeDietPlan != null && !string.IsNullOrWhiteSpace(activeDietPlan.SpecialInstructions)
                    ? activeDietPlan.SpecialInstructions
                    : string.Empty);

            string nextRecs = activeWorkoutPlan != null && !string.IsNullOrWhiteSpace(activeWorkoutPlan.ProgressionStrategy)
                ? activeWorkoutPlan.ProgressionStrategy
                : (activeWorkoutPlan != null && !string.IsNullOrWhiteSpace(activeWorkoutPlan.RecoveryGuidelines)
                    ? activeWorkoutPlan.RecoveryGuidelines
                    : string.Empty);

            var recentLogsDto = workoutLogs.Take(10).Select(w => new ProgressWorkoutLogDto
            {
                Date = w.LogDate,
                RoutineName = w.WorkoutPlans != null ? w.WorkoutPlans.Title : (!string.IsNullOrWhiteSpace(w.SessionName) ? w.SessionName : "Workout Session"),
                DurationMinutes = w.DurationMinutes,
                CaloriesBurned = w.CaloriesBurned,
                PerceivedExertion = w.PerceivedExertion,
                ClientNotes = w.ClientNotes
            }).ToList();

            var biometricsDto = progressRecords.OrderByDescending(r => r.RecordedDate).Select(r => new ProgressBiometricLogDto
            {
                RecordId = r.RecordId,
                RecordedDate = r.RecordedDate,
                WeightKg = (double)r.WeightKg,
                HeightCm = (double)r.HeightCm,
                Bmi = r.Bmi.HasValue ? (double?)r.Bmi.Value : null,
                BodyFatPct = r.BodyFatPercentage.HasValue ? (double?)r.BodyFatPercentage.Value : null,
                HeartRateBpm = r.HeartRateBpm,
                StepsCount = r.StepsCount,
                CaloriesConsumed = r.CaloriesConsumed,
                CaloriesBurned = r.CaloriesBurned,
                WaterIntakeMl = r.WaterIntakeMl,
                Notes = r.Notes
            }).ToList();

            var chartLabels = progressRecords.Select(r => r.RecordedDate.ToString("MMM dd")).ToList();
            var weightData = progressRecords.Select(r => (double)r.WeightKg).ToList();
            var bmiData = progressRecords.Select(r => (double)(r.Bmi ?? 0m)).ToList();
            var calBurnData = progressRecords.Select(r => r.CaloriesBurned).ToList();

            return new ClientProgressReportViewModel
            {
                ClientId = clientId,
                ClientName = client.FullName,
                ClientEmail = client.Email,
                Gender = client.Gender ?? string.Empty,
                PrimaryGoal = preferences != null && !string.IsNullOrWhiteSpace(preferences.PrimaryGoal) ? preferences.PrimaryGoal : string.Empty,
                DietaryRestrictions = healthProfile != null && !string.IsNullOrWhiteSpace(healthProfile.DietaryRestrictions) ? healthProfile.DietaryRestrictions : "None Reported",
                Allergies = healthProfile != null && !string.IsNullOrWhiteSpace(healthProfile.Allergies) ? healthProfile.Allergies : "None Reported",
                MedicalConditions = healthProfile != null && !string.IsNullOrWhiteSpace(healthProfile.MedicalConditions) ? healthProfile.MedicalConditions : "None Reported",

                TrainerName = trainer != null ? trainer.FullName : "Not Assigned",
                TrainerSpecialization = trainer != null ? (trainer.PrimarySpecialization ?? trainer.Specialization ?? "Fitness Coach") : string.Empty,
                TrainerEmail = trainer != null ? trainer.Email : string.Empty,

                FromDate = start,
                ToDate = end,
                DatePreset = activePreset,
                FilterFromDate = fromDate,
                FilterToDate = toDate,

                BaselineWeightKg = baselineWeight,
                CurrentWeightKg = currentWeight,
                TargetWeightKg = targetWeight,
                HeightCm = heightCm,
                BaselineBmi = baselineBmi,
                CurrentBmi = currentBmi,
                BaselineBodyFatPct = firstRec != null && firstRec.BodyFatPercentage.HasValue ? (double?)firstRec.BodyFatPercentage.Value : null,
                CurrentBodyFatPct = latestRec != null && latestRec.BodyFatPercentage.HasValue ? (double?)latestRec.BodyFatPercentage.Value : null,

                TotalWorkoutsScheduled = scheduledWorkouts,
                TotalWorkoutsCompleted = completedWorkouts,
                TotalExerciseMinutes = totalExerciseMinutes,
                TotalCaloriesBurned = totalCaloriesBurned,

                PrescribedDailyCalories = dailyCalTarget,
                AverageDailyCaloriesConsumed = avgCaloriesConsumed,
                PrescribedWaterLiters = prescribedWater,
                AverageDailyWaterLiters = avgWaterConsumed,

                TargetProteinGrams = activeDietPlan != null ? activeDietPlan.TargetProteinGrams : 0,
                TargetCarbsGrams = activeDietPlan != null ? activeDietPlan.TargetCarbsGrams : 0,
                TargetFatsGrams = activeDietPlan != null ? activeDietPlan.TargetFatsGrams : 0,

                AverageProteinGrams = avgProtein,
                AverageCarbsGrams = avgCarbs,
                AverageFatsGrams = avgFats,

                Goals = goals,

                HasActiveWorkoutPlan = activeWorkoutPlan != null,
                WorkoutPlanName = activeWorkoutPlan != null ? activeWorkoutPlan.Title : string.Empty,
                WorkoutObjective = activeWorkoutPlan != null ? (!string.IsNullOrWhiteSpace(activeWorkoutPlan.Goal) ? activeWorkoutPlan.Goal : activeWorkoutPlan.Description) : string.Empty,
                WorkoutDifficulty = activeWorkoutPlan != null ? activeWorkoutPlan.DifficultyLevel : string.Empty,
                WorkoutDurationWeeks = activeWorkoutPlan != null ? activeWorkoutPlan.DurationWeeks : 0,
                WorkoutTrainerNotes = activeWorkoutPlan != null ? activeWorkoutPlan.TrainerNotes : string.Empty,
                WorkoutProgressionStrategy = activeWorkoutPlan != null ? activeWorkoutPlan.ProgressionStrategy : string.Empty,
                WorkoutRecoveryGuidelines = activeWorkoutPlan != null ? activeWorkoutPlan.RecoveryGuidelines : string.Empty,
                WorkoutDays = workoutDays,

                HasActiveDietPlan = activeDietPlan != null,
                DietPlanName = activeDietPlan != null ? activeDietPlan.Title : string.Empty,
                DietObjective = activeDietPlan != null ? (!string.IsNullOrWhiteSpace(activeDietPlan.Description) ? activeDietPlan.Description : activeDietPlan.DietaryPreference) : string.Empty,
                DietSpecialInstructions = activeDietPlan != null ? activeDietPlan.SpecialInstructions : string.Empty,
                ScheduledMeals = scheduledMeals,

                RecentWorkoutLogs = recentLogsDto,
                BiometricHistory = biometricsDto,

                CoachEvaluationNotes = evalNotes,
                NextPhaseRecommendations = nextRecs,

                ChartLabels = chartLabels,
                WeightHistory = weightData,
                BmiHistory = bmiData,
                CaloriesBurnedHistory = calBurnData
            };
        }

        // GET: /Client/Progress
        // Interactive Client Progress & Plans Report Hub
        [HttpGet]
        public ActionResult Index(string preset = "30days", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var model = BuildReportModel(preset, fromDate, toDate);

            // FR-S-20: Audit progress hub viewing
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (!string.IsNullOrEmpty(clientId))
            {
                HealthAuditHelper.LogAccess(Session, clientId, "ProgressRecord", null, "View", true, $"Viewed client progress hub (preset: {preset})");
            }

            return View(model);
        }

        // GET: /Client/Progress/ExportPdf
        // Dedicated Printable View for direct Browser PDF Export
        [HttpGet]
        public ActionResult ExportPdf(string preset = "30days", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var model = BuildReportModel(preset, fromDate, toDate);

            // FR-S-20: Audit progress report export
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (!string.IsNullOrEmpty(clientId))
            {
                HealthAuditHelper.LogAccess(Session, clientId, "ProgressReport", null, "Export", true, $"Exported progress report to PDF (preset: {preset})");
            }

            return View("ExportPdf", model);
        }

        // GET: /Client/Progress/Print
        [HttpGet]
        public ActionResult Print(string preset = "30days", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var model = BuildReportModel(preset, fromDate, toDate);
            return View("ExportPdf", model);
        }

        // GET: /Client/Progress/Edit/5
        // FR-C-26: View previously recorded health measurement for editing
        [HttpGet]
        public ActionResult Edit(int id)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            var record = db.ClientProgressRecords.FirstOrDefault(r => r.RecordId == id && r.ClientId == clientId);
            if (record == null)
            {
                TempData["ErrorMessage"] = "Health measurement record not found or access denied.";
                return RedirectToAction("Index");
            }

            // FR-S-14: Decrypt record notes for display in edit form
            HealthDataProtectionHelper.DecryptProgressRecord(record);

            // FR-S-20: Audit health measurement access for editing
            HealthAuditHelper.LogAccess(Session, clientId, "ProgressRecord", id.ToString(), "View", true, "Viewed health measurement record for editing");

            var model = new EditHealthMeasurementViewModel
            {
                RecordId = record.RecordId,
                ClientId = record.ClientId,
                RecordedDate = record.RecordedDate,
                WeightKg = record.WeightKg,
                HeightCm = record.HeightCm,
                Bmi = record.Bmi,
                BodyFatPercentage = record.BodyFatPercentage,
                HeartRateBpm = record.HeartRateBpm,
                StepsCount = record.StepsCount,
                CaloriesConsumed = record.CaloriesConsumed,
                CaloriesBurned = record.CaloriesBurned,
                WaterIntakeMl = record.WaterIntakeMl,
                Notes = record.Notes
            };

            return View("Edit", model);
        }

        // POST: /Client/Progress/Edit
        // FR-C-26: Update recorded health measurement
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(EditHealthMeasurementViewModel model)
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
                    : "Please ensure all health measurement fields are entered correctly.";
                return RedirectToAction("Index");
            }

            // Verify the record exists and belongs strictly to the logged-in Client
            var record = db.ClientProgressRecords.FirstOrDefault(r => r.RecordId == model.RecordId && r.ClientId == clientId);
            if (record == null)
            {
                TempData["ErrorMessage"] = "Health measurement record not found or access denied.";
                return RedirectToAction("Index");
            }

            // Update applicable biometric and habit metrics
            record.WeightKg = Math.Round(model.WeightKg, 2);
            record.HeightCm = Math.Round(model.HeightCm, 2);
            record.HeartRateBpm = model.HeartRateBpm;
            record.StepsCount = model.StepsCount;
            record.CaloriesConsumed = model.CaloriesConsumed;
            record.CaloriesBurned = model.CaloriesBurned;
            record.WaterIntakeMl = model.WaterIntakeMl;
            record.BodyFatPercentage = model.BodyFatPercentage.HasValue ? Math.Round(model.BodyFatPercentage.Value, 1) : (decimal?)null;
            // FR-S-14: Encrypt sensitive clinical notes before saving
            record.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : HealthDataProtectionHelper.Encrypt(model.Notes.Trim());

            // Preserve automatic BMI calculation
            if (record.HeightCm > 0)
            {
                decimal heightInMeters = record.HeightCm / 100.0m;
                record.Bmi = Math.Round(record.WeightKg / (heightInMeters * heightInMeters), 1);
            }
            else
            {
                record.Bmi = null;
            }

            // Synchronize Client Profile Height and Weight Goals if updating the most recent record
            var latestRecord = db.ClientProgressRecords
                .Where(p => p.ClientId == clientId)
                .OrderByDescending(p => p.RecordedDate)
                .ThenByDescending(p => p.RecordId)
                .FirstOrDefault();

            if (latestRecord != null && latestRecord.RecordId == record.RecordId)
            {
                var client = db.Clients.FirstOrDefault(c => c.ClientId == clientId);
                if (client != null && record.HeightCm > 0)
                {
                    client.HeightCm = record.HeightCm;
                }

                var weightGoal = db.ClientGoals
                    .FirstOrDefault(g => g.ClientId == clientId && (g.Category == "Weight" || g.GoalName.Contains("Weight")));
                if (weightGoal != null)
                {
                    weightGoal.CurrentValue = record.WeightKg;
                    if (Math.Abs(weightGoal.TargetValue - weightGoal.CurrentValue) <= 0.5m)
                    {
                        weightGoal.IsAchieved = true;
                        weightGoal.StatusMessage = "Target Goal Achieved! 🎉";
                    }
                    else
                    {
                        weightGoal.StatusMessage = "In Progress";
                    }
                }
            }

            db.SaveChanges();

            // FR-S-20: Audit health measurement update
            HealthAuditHelper.LogAccess(Session, clientId, "ProgressRecord", record.RecordId.ToString(), "Update", true, $"Updated health measurement for {record.RecordedDate:yyyy-MM-dd}");

            TempData["SuccessMessage"] = $"Health measurement for {record.RecordedDate:MMM dd, yyyy} updated successfully!";
            return RedirectToAction("Index");
        }

        // POST: /Client/Progress/Delete
        // FR-C-27: Delete recorded health measurement after client confirmation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int? id, int? recordId)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            int targetId = id ?? recordId ?? 0;
            if (targetId <= 0)
            {
                TempData["ErrorMessage"] = "Invalid health measurement record specified.";
                return RedirectToAction("Index");
            }

            // Verify that the measurement exists and belongs strictly to the currently authenticated Client
            var record = db.ClientProgressRecords.FirstOrDefault(r => r.RecordId == targetId && r.ClientId == clientId);
            if (record == null)
            {
                TempData["ErrorMessage"] = "Health measurement record not found or access denied.";
                return RedirectToAction("Index");
            }

            string formattedDate = record.RecordedDate.ToString("MMM dd, yyyy");

            // Check if this was the latest recorded measurement
            var wasLatest = db.ClientProgressRecords
                .Where(p => p.ClientId == clientId)
                .OrderByDescending(p => p.RecordedDate)
                .ThenByDescending(p => p.RecordId)
                .Select(p => p.RecordId)
                .FirstOrDefault() == record.RecordId;

            // Remove the record from database
            db.ClientProgressRecords.Remove(record);
            db.SaveChanges();

            // FR-S-20: Audit health measurement deletion
            HealthAuditHelper.LogAccess(Session, clientId, "ProgressRecord", targetId.ToString(), "Delete", true, $"Deleted health measurement record {targetId} ({formattedDate})");

            // Synchronize weight goal and profile height if the latest record was deleted
            if (wasLatest)
            {
                var newLatest = db.ClientProgressRecords
                    .Where(p => p.ClientId == clientId)
                    .OrderByDescending(p => p.RecordedDate)
                    .ThenByDescending(p => p.RecordId)
                    .FirstOrDefault();

                var client = db.Clients.FirstOrDefault(c => c.ClientId == clientId);
                if (client != null && newLatest != null && newLatest.HeightCm > 0)
                {
                    client.HeightCm = newLatest.HeightCm;
                }

                var weightGoal = db.ClientGoals
                    .FirstOrDefault(g => g.ClientId == clientId && (g.Category == "Weight" || g.GoalName.Contains("Weight")));

                if (weightGoal != null)
                {
                    if (newLatest != null)
                    {
                        weightGoal.CurrentValue = newLatest.WeightKg;
                        if (Math.Abs(weightGoal.TargetValue - weightGoal.CurrentValue) <= 0.5m)
                        {
                            weightGoal.IsAchieved = true;
                            weightGoal.StatusMessage = "Target Goal Achieved! 🎉";
                        }
                        else
                        {
                            weightGoal.IsAchieved = false;
                            weightGoal.StatusMessage = "In Progress";
                        }
                    }
                    db.SaveChanges();
                }
            }

            TempData["SuccessMessage"] = $"Health measurement for {formattedDate} has been permanently deleted.";
            return RedirectToAction("Index");
        }

        // POST: /Client/Progress/DeleteMeasurement
        // Alias endpoint for FR-C-27
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("DeleteMeasurement")]
        public ActionResult DeleteMeasurement(int? id, int? recordId)
        {
            return Delete(id, recordId);
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
