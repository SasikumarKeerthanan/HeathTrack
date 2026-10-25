using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Models;
using HealthTrack.Helpers;
using HealthTrack.Areas.Trainer.ViewModels;

namespace HealthTrack.Areas.Trainer.Controllers
{
    // ========================================================================
    // ClientsController: Assigned Clients Module
    // Allows the logged-in trainer to view, search, and filter assigned clients (Page 1)
    // and view a complete 4-tab client dossier with health, goals, workout & meal history (Page 2)
    // ========================================================================
    [TrainerAuthorize]
    public class ClientsController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // --------------------------------------------------------------------
        // Helper: Get Current Logged-in Trainer ID from Session
        // --------------------------------------------------------------------
        private string GetCurrentTrainerId()
        {
            return SessionHelper.GetCurrentTrainerId(Session);
        }

        // --------------------------------------------------------------------
        // Helper: Calculate BMI category
        // --------------------------------------------------------------------
        private static string CalculateBmiCategory(decimal? bmi)
        {
            if (!bmi.HasValue || bmi.Value <= 0) return "Not Recorded";
            if (bmi < 18.5m) return "Underweight";
            if (bmi < 25.0m) return "Normal";
            if (bmi < 30.0m) return "Overweight";
            return "Obese";
        }

        // --------------------------------------------------------------------
        // Helper: Fetch meal logs (uses ClientMealLogs with progress fallback)
        // --------------------------------------------------------------------
        private List<ClientMealHistoryItem> GetMealLogsForClient(string clientId, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                string sql = @"SELECT MealLogId, LogDate, 
                                      ISNULL(MealType, 'Meal') AS MealType, 
                                      ISNULL(MealName, 'Meal') AS MealName, 
                                      ISNULL(PortionSize, '') AS PortionSize, 
                                      Calories, 
                                      ISNULL(ProteinGrams, 0) AS ProteinGrams, 
                                      ISNULL(CarbsGrams, 0) AS CarbsGrams, 
                                      ISNULL(FatsGrams, 0) AS FatsGrams, 
                                      ISNULL(WaterLiters, 0) AS WaterLiters, 
                                      ClientNotes 
                               FROM ClientMealLogs 
                               WHERE ClientId = @p0";
                var parameters = new List<object> { clientId };

                if (fromDate.HasValue)
                {
                    sql += " AND LogDate >= @p" + parameters.Count;
                    parameters.Add(fromDate.Value);
                }
                if (toDate.HasValue)
                {
                    sql += " AND LogDate <= @p" + parameters.Count;
                    parameters.Add(toDate.Value);
                }

                sql += " ORDER BY LogDate DESC";

                var results = db.Database.SqlQuery<ClientMealHistoryItem>(sql, parameters.ToArray()).ToList();
                if (results.Any())
                {
                    return results;
                }
            }
            catch
            {
                // ClientMealLogs table not populated - safely fall through to fallback
            }

            // Fallback: construct meal intake records from ClientProgressRecords
            var progressQuery = db.ClientProgressRecords.Where(r => r.ClientId == clientId && r.CaloriesConsumed > 0);
            if (fromDate.HasValue) progressQuery = progressQuery.Where(r => r.RecordedDate >= fromDate.Value);
            if (toDate.HasValue) progressQuery = progressQuery.Where(r => r.RecordedDate <= toDate.Value);

            return progressQuery
                .OrderByDescending(r => r.RecordedDate)
                .ToList()
                .Select(r => new ClientMealHistoryItem
                {
                    MealLogId = r.RecordId,
                    LogDate = r.RecordedDate,
                    MealType = "Daily Intake",
                    MealName = "Logged Daily Nutrition",
                    PortionSize = "Full Day",
                    Calories = r.CaloriesConsumed,
                    ProteinGrams = 0,
                    CarbsGrams = 0,
                    FatsGrams = 0,
                    WaterLiters = Math.Round((decimal)r.WaterIntakeMl / 1000m, 1),
                    ClientNotes = r.Notes
                }).ToList();
        }

        // ====================================================================
        // PAGE 1: GET /Trainer/Clients or /Trainer/Clients/Index
        // Search & Multi-Filter Assigned Clients Dashboard
        // ====================================================================
        [HttpGet]
        public ActionResult Index(string search, string goal = "all", string status = "all", string bmi = "all", string sortBy = "name_asc")
        {
            string trainerId = GetCurrentTrainerId();

            // 1. Query ALL active clients assigned to this trainer
            var rawClients = db.Clients
                .Include(c => c.ClientHealthProfile)
                .Include(c => c.ClientPreferences)
                .Include(c => c.ClientProgressRecords)
                .Include(c => c.WorkoutPlans)
                .Include(c => c.DietPlans)
                .Where(c => c.AssignedTrainerId == trainerId && c.IsActive)
                .ToList();

            // 2. Compute Trainer KPI statistics across all assigned clients
            int totalAssigned = rawClients.Count;
            int withWorkoutCount = rawClients.Count(c => c.WorkoutPlans.Any(p => p.IsActive));
            int withDietCount = rawClients.Count(c => c.DietPlans.Any(p => p.IsActive));
            int needsAttentionCount = rawClients.Count(c => !c.WorkoutPlans.Any(p => p.IsActive) || !c.DietPlans.Any(p => p.IsActive));

            // Extract distinct goals for filter dropdown
            var availableGoals = rawClients
                .Select(c => c.ClientPreferences.FirstOrDefault()?.PrimaryGoal)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            // 3. Transform to ClientCardItems
            var cards = rawClients.Select(c =>
            {
                var latestRecord = c.ClientProgressRecords.OrderByDescending(r => r.RecordedDate).FirstOrDefault();
                var activeWorkout = c.WorkoutPlans.FirstOrDefault(p => p.IsActive);
                var activeDiet = c.DietPlans.FirstOrDefault(p => p.IsActive);
                var preference = c.ClientPreferences.FirstOrDefault();

                decimal? currentWeight = latestRecord != null ? (decimal?)latestRecord.WeightKg : null;
                decimal? currentBmi = latestRecord != null ? latestRecord.Bmi : null;

                return new ClientCardItem
                {
                    ClientId = c.ClientId,
                    FullName = c.FullName,
                    Email = c.Email,
                    Gender = c.Gender,
                    DateOfBirth = c.DateOfBirth,
                    JoinedDate = c.CreatedAt,
                    PrimaryGoal = preference != null && !string.IsNullOrWhiteSpace(preference.PrimaryGoal) ? preference.PrimaryGoal : "General Fitness",
                    FitnessLevel = preference != null && !string.IsNullOrWhiteSpace(preference.FitnessLevel) ? preference.FitnessLevel : "Intermediate",
                    ActiveWorkoutPlanId = activeWorkout?.WorkoutPlanId,
                    ActiveWorkoutPlanTitle = activeWorkout?.Title,
                    ActiveDietPlanId = activeDiet?.DietPlanId,
                    ActiveDietPlanTitle = activeDiet?.Title,
                    CurrentWeightKg = currentWeight,
                    CurrentBmi = currentBmi,
                    BmiCategory = CalculateBmiCategory(currentBmi),
                    AssignmentStatus = "Assigned client",
                    LatestRecordedDate = latestRecord?.RecordedDate
                };
            }).AsEnumerable();

            // 4. Apply Search Filter (Name or Email)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                cards = cards.Where(c => (c.FullName != null && c.FullName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                                      || (c.Email != null && c.Email.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                                      || (c.ClientId != null && c.ClientId.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            // 5. Apply Goal Filter
            if (!string.IsNullOrWhiteSpace(goal) && goal != "all")
            {
                cards = cards.Where(c => c.PrimaryGoal != null && c.PrimaryGoal.IndexOf(goal, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // 6. Apply Plan Status Filter
            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                switch (status.ToLowerInvariant())
                {
                    case "has_workout":
                        cards = cards.Where(c => c.HasActiveWorkout);
                        break;
                    case "missing_workout":
                        cards = cards.Where(c => !c.HasActiveWorkout);
                        break;
                    case "has_diet":
                        cards = cards.Where(c => c.HasActiveDiet);
                        break;
                    case "missing_diet":
                        cards = cards.Where(c => !c.HasActiveDiet);
                        break;
                    case "configured":
                        cards = cards.Where(c => c.IsFullyConfigured);
                        break;
                    case "needs_attention":
                        cards = cards.Where(c => !c.IsFullyConfigured);
                        break;
                }
            }

            // 7. Apply BMI Category Filter
            if (!string.IsNullOrWhiteSpace(bmi) && bmi != "all")
            {
                cards = cards.Where(c => c.BmiCategory != null && c.BmiCategory.Equals(bmi, StringComparison.OrdinalIgnoreCase));
            }

            // 8. Apply Sorting
            switch (sortBy?.ToLowerInvariant())
            {
                case "name_desc":
                    cards = cards.OrderByDescending(c => c.FullName);
                    break;
                case "recent":
                    cards = cards.OrderByDescending(c => c.JoinedDate);
                    break;
                case "weight_desc":
                    cards = cards.OrderByDescending(c => c.CurrentWeightKg ?? 0);
                    break;
                case "weight_asc":
                    cards = cards.OrderBy(c => c.CurrentWeightKg ?? 9999);
                    break;
                case "name_asc":
                default:
                    cards = cards.OrderBy(c => c.FullName);
                    break;
            }

            var viewModel = new AssignedClientsIndexViewModel
            {
                SearchTerm = search,
                SelectedGoal = goal,
                SelectedStatus = status,
                SelectedBmi = bmi,
                SortBy = sortBy,
                TotalAssigned = totalAssigned,
                WithActiveWorkoutCount = withWorkoutCount,
                WithActiveDietCount = withDietCount,
                NeedsAttentionCount = needsAttentionCount,
                AvailableGoals = availableGoals,
                Clients = cards.ToList()
            };

            return View(viewModel);
        }

        // ====================================================================
        // PAGE 2: GET /Trainer/Clients/Details/{id}
        // One unified page with 4 tabs:
        // Tab 1: Profile & Health
        // Tab 2: Goals & Progress
        // Tab 3: Workout History
        // Tab 4: Meal History (with date filtering)
        // ====================================================================
        [HttpGet]
        public ActionResult Details(string id, string tab = "profile", string workoutFilter = "30days", string mealFilter = "30days", DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Please select a client to view their profile.";
                return RedirectToAction("Index");
            }

            string trainerId = GetCurrentTrainerId();

            // 1. Fetch client with all related entities
            var client = db.Clients
                .Include(c => c.ClientHealthProfile)
                .Include(c => c.ClientPreferences)
                .Include(c => c.ClientGoals)
                .Include(c => c.ClientProgressRecords)
                .Include(c => c.WorkoutPlans)
                .Include(c => c.DietPlans)
                .FirstOrDefault(c => c.ClientId == id);

            if (client == null)
            {
                TempData["ErrorMessage"] = "Client not found.";
                return RedirectToAction("Index");
            }

            // 2. SECURITY CHECK: Trainer can only view their own actively assigned client
            if (!AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, client.ClientId))
            {
                // FR-S-20: Audit blocked unauthorized access attempt
                HealthAuditHelper.Log(trainerId, "Trainer", client.ClientId, "HealthProfile", null, "View", false, "Blocked unauthorized attempt to access non-assigned client profile");

                TempData["ErrorMessage"] = "Access restricted: Clients not currently assigned to your trainer profile cannot be accessed.";
                return RedirectToAction("Index");
            }

            // 3. Prepare Tab 1: Profile & Health
            var healthProfile = client.ClientHealthProfile.FirstOrDefault();
            // FR-S-14: Decrypt sensitive narrative health profile fields for authorized trainer view
            HealthDataProtectionHelper.DecryptProfile(healthProfile);

            // FR-S-20: Audit authorized client health profile view
            HealthAuditHelper.Log(trainerId, "Trainer", client.ClientId, "HealthProfile", healthProfile?.ProfileId.ToString(), "View", true, "Viewed assigned client health profile and biometrics in dossier");

            var preference = client.ClientPreferences.FirstOrDefault();
            var latestRecord = client.ClientProgressRecords.OrderByDescending(r => r.RecordedDate).FirstOrDefault();
            var firstRecord = client.ClientProgressRecords.OrderBy(r => r.RecordedDate).FirstOrDefault();
            var activeWorkout = client.WorkoutPlans.FirstOrDefault(p => p.IsActive);
            var activeDiet = client.DietPlans.FirstOrDefault(p => p.IsActive);

            decimal? currentWeight = latestRecord != null ? (decimal?)latestRecord.WeightKg : null;
            decimal? baselineWeight = firstRecord != null ? (decimal?)firstRecord.WeightKg : null;
            decimal? currentBmi = latestRecord != null ? latestRecord.Bmi : null;

            int? age = null;
            if (client.DateOfBirth.HasValue)
            {
                var today = DateTime.Today;
                age = today.Year - client.DateOfBirth.Value.Year;
                if (client.DateOfBirth.Value.Date > today.AddYears(-age.Value)) age--;
            }

            var cardItem = new ClientCardItem
            {
                ClientId = client.ClientId,
                FullName = client.FullName,
                Email = client.Email,
                Gender = client.Gender,
                DateOfBirth = client.DateOfBirth,
                JoinedDate = client.CreatedAt,
                PrimaryGoal = preference != null && !string.IsNullOrWhiteSpace(preference.PrimaryGoal) ? preference.PrimaryGoal : "General Fitness",
                FitnessLevel = preference != null && !string.IsNullOrWhiteSpace(preference.FitnessLevel) ? preference.FitnessLevel : "Intermediate",
                ActiveWorkoutPlanId = activeWorkout?.WorkoutPlanId,
                ActiveWorkoutPlanTitle = activeWorkout?.Title,
                ActiveDietPlanId = activeDiet?.DietPlanId,
                ActiveDietPlanTitle = activeDiet?.Title,
                CurrentWeightKg = currentWeight,
                CurrentBmi = currentBmi,
                BmiCategory = CalculateBmiCategory(currentBmi),
                AssignmentStatus = "Assigned client",
                LatestRecordedDate = latestRecord?.RecordedDate
            };

            // 4. Prepare Tab 2: Goals & Progress
            var goals = client.ClientGoals.Select(g =>
            {
                double baseline = (double)g.BaselineValue;
                double current = (double)g.CurrentValue;
                double target = (double)g.TargetValue;
                double range = Math.Abs(target - baseline);
                int progress = 0;
                if (range > 0)
                {
                    double achieved = Math.Abs(current - baseline);
                    progress = Math.Min(100, Math.Max(0, (int)Math.Round(achieved / range * 100)));
                }
                else
                {
                    progress = 100;
                }

                return new ClientGoalItem
                {
                    GoalId = g.GoalId,
                    GoalName = g.GoalName,
                    Category = g.Category,
                    BaselineValue = baseline,
                    CurrentValue = current,
                    TargetValue = target,
                    Unit = g.Unit,
                    ProgressPercentage = progress,
                    StatusMessage = g.StatusMessage,
                    IsAchieved = g.IsAchieved,
                    TargetDate = g.TargetDate
                };
            }).ToList();

            var progressRecords = client.ClientProgressRecords
                .OrderByDescending(r => r.RecordedDate)
                .Select(r => new ClientProgressItem
                {
                    RecordedDate = r.RecordedDate,
                    WeightKg = r.WeightKg,
                    Bmi = r.Bmi,
                    CaloriesConsumed = r.CaloriesConsumed,
                    CaloriesBurned = r.CaloriesBurned,
                    WaterIntakeMl = r.WaterIntakeMl,
                    Notes = r.Notes
                }).ToList();
            progressRecords.ForEach(p => p.Notes = HealthDataProtectionHelper.Decrypt(p.Notes));

            // 5. Prepare Tab 3: Workout History (with date filter)
            var workoutLogsQuery = db.ClientWorkoutLogs
                .Include(wl => wl.WorkoutPlans)
                .Where(wl => wl.ClientId == client.ClientId);

            if (workoutFilter == "7days")
            {
                var cutoff = DateTime.Today.AddDays(-7);
                workoutLogsQuery = workoutLogsQuery.Where(wl => wl.LogDate >= cutoff);
            }
            else if (workoutFilter == "30days")
            {
                var cutoff = DateTime.Today.AddDays(-30);
                workoutLogsQuery = workoutLogsQuery.Where(wl => wl.LogDate >= cutoff);
            }
            else if (workoutFilter == "90days")
            {
                var cutoff = DateTime.Today.AddDays(-90);
                workoutLogsQuery = workoutLogsQuery.Where(wl => wl.LogDate >= cutoff);
            }

            var workoutLogs = workoutLogsQuery
                .OrderByDescending(wl => wl.LogDate)
                .ToList()
                .Select(wl => new ClientWorkoutHistoryItem
                {
                    LogId = wl.LogId,
                    LogDate = wl.LogDate,
                    SessionName = wl.SessionName,
                    DurationMinutes = wl.DurationMinutes,
                    CaloriesBurned = wl.CaloriesBurned,
                    IsCompleted = wl.IsCompleted,
                    PerceivedExertion = wl.PerceivedExertion,
                    ClientNotes = wl.ClientNotes,
                    CoachFeedback = wl.CoachFeedback,
                    WorkoutPlanTitle = wl.WorkoutPlans != null ? wl.WorkoutPlans.Title : "Custom Session",
                    WorkoutPlanId = wl.WorkoutPlanId
                }).ToList();

            int totalCompletedWorkouts = db.ClientWorkoutLogs.Count(wl => wl.ClientId == client.ClientId && wl.IsCompleted);

            // 6. Prepare Tab 4: Meal History (with date filter)
            DateTime? mealCutoff = null;
            if (mealFilter == "7days") mealCutoff = DateTime.Today.AddDays(-7);
            else if (mealFilter == "30days") mealCutoff = DateTime.Today.AddDays(-30);
            else if (mealFilter == "90days") mealCutoff = DateTime.Today.AddDays(-90);
            else if (mealFilter == "custom" && fromDate.HasValue) mealCutoff = fromDate.Value;

            var mealLogs = GetMealLogsForClient(client.ClientId, mealCutoff, toDate);

            int totalLoggedCalories = mealLogs.Sum(m => m.Calories);
            int loggedDays = mealLogs.Select(m => m.LogDate.Date).Distinct().Count();
            int avgDailyCalories = loggedDays > 0 ? (totalLoggedCalories / loggedDays) : 0;

            // 7. Assemble Full Details ViewModel
            var viewModel = new ClientDetailsViewModel
            {
                ActiveTab = string.IsNullOrWhiteSpace(tab) ? "profile" : tab.ToLowerInvariant(),
                Client = cardItem,

                // Tab 1: Profile & Health
                Age = age,
                HeightCm = healthProfile != null ? (decimal?)healthProfile.BaselineHeightCm : null,
                WeightKg = currentWeight,
                BaselineWeightKg = baselineWeight,
                Bmi = currentBmi,
                BmiCategory = CalculateBmiCategory(currentBmi),
                Allergies = healthProfile != null && !string.IsNullOrWhiteSpace(healthProfile.Allergies) ? healthProfile.Allergies : "None reported",
                DietaryRestrictions = healthProfile != null && !string.IsNullOrWhiteSpace(healthProfile.DietaryRestrictions) ? healthProfile.DietaryRestrictions : "None reported",
                MedicalConditions = healthProfile != null && !string.IsNullOrWhiteSpace(healthProfile.MedicalConditions) ? healthProfile.MedicalConditions : "None reported",
                InjuriesHistory = healthProfile != null && !string.IsNullOrWhiteSpace(healthProfile.InjuriesHistory) ? healthProfile.InjuriesHistory : "None reported",
                FitnessLevel = preference != null && !string.IsNullOrWhiteSpace(preference.FitnessLevel) ? preference.FitnessLevel : "Intermediate",
                TargetDaysPerWeek = preference != null ? preference.TargetDaysPerWeek : 4,
                PreferredWorkoutType = preference != null && !string.IsNullOrWhiteSpace(preference.PreferredWorkoutType) ? preference.PreferredWorkoutType : "Strength & Cardio",
                PreferredDietType = preference != null && !string.IsNullOrWhiteSpace(preference.PreferredDietType) ? preference.PreferredDietType : "Balanced Diet",

                // Tab 2: Goals & Progress
                Goals = goals,
                ProgressRecords = progressRecords,
                SleepLogs = db.ClientSleepLogs.Where(s => s.ClientId == client.ClientId).OrderByDescending(s => s.SleepDate).Take(30).ToList().Select(s => { HealthDataProtectionHelper.DecryptSleepLog(s); return s; }).ToList(),
                TotalCompletedWorkouts = totalCompletedWorkouts,

                // Tab 3: Workout History
                WorkoutLogs = workoutLogs,
                ActiveWorkoutPlanId = activeWorkout?.WorkoutPlanId,
                ActiveWorkoutPlanTitle = activeWorkout?.Title,
                WorkoutDateFilter = workoutFilter,

                // Tab 4: Meal History
                MealLogs = mealLogs,
                ActiveDietPlanId = activeDiet?.DietPlanId,
                ActiveDietPlanTitle = activeDiet?.Title,
                DailyCalorieTarget = activeDiet?.DailyCalorieTarget,
                MealDateFilter = mealFilter,
                FromDate = fromDate,
                ToDate = toDate,
                TotalLoggedCalories = totalLoggedCalories,
                AverageDailyCalories = avgDailyCalories
            };

            return View(viewModel);
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
