using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Client.ViewModels;
using HealthTrack.Models;
using HealthTrack.Helpers;

namespace HealthTrack.Areas.Client.Controllers
{
    [ClientAuthorize]
    public class DietController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        private static readonly string[] OrderedDaysOfWeek = new[]
        {
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
        };

        // Helper: retrieve active logged-in client strictly from Session
        private Clients GetCurrentClient(string requestedClientId = null)
        {
            var sessionClientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrWhiteSpace(sessionClientId))
            {
                return null;
            }

            // FR-S-04: If requestedClientId is supplied, strictly verify ownership against authenticated session
            if (!string.IsNullOrWhiteSpace(requestedClientId) &&
                !string.Equals(requestedClientId.Trim(), sessionClientId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var sessionClient = db.Clients
                .Include(c => c.Trainers)
                .Include(c => c.ClientHealthProfile)
                .Include(c => c.ClientPreferences)
                .FirstOrDefault(c => c.ClientId == sessionClientId);

            if (sessionClient != null)
            {
                Session["CurrentClientName"] = sessionClient.FullName;
                Session["CurrentClientEmail"] = sessionClient.Email;
            }

            return sessionClient;
        }

        private ClientDietSummaryDto MapClientSummary(Clients client)
        {
            if (client == null) return new ClientDietSummaryDto();

            var healthProfile = client.ClientHealthProfile != null ? client.ClientHealthProfile.FirstOrDefault() : null;
            var preference = client.ClientPreferences != null ? client.ClientPreferences.FirstOrDefault() : null;

            return new ClientDietSummaryDto
            {
                ClientId = client.ClientId,
                FullName = client.FullName,
                Email = client.Email,
                Gender = client.Gender,
                AssignedTrainerId = client.AssignedTrainerId,
                TrainerName = client.Trainers != null ? client.Trainers.FullName : "Assigned Coach",
                PrimaryGoal = preference != null && !string.IsNullOrWhiteSpace(preference.PrimaryGoal) ? preference.PrimaryGoal : "General Fitness",
                DietaryRestrictions = healthProfile != null && !string.IsNullOrWhiteSpace(healthProfile.DietaryRestrictions) ? healthProfile.DietaryRestrictions : "None Reported",
                Allergies = healthProfile != null && !string.IsNullOrWhiteSpace(healthProfile.Allergies) ? healthProfile.Allergies : "None Reported"
            };
        }

        // Raw SQL helper to fetch logs from ClientMealLogs table
        private List<ClientMealLogItemDto> GetMealLogsForClient(string clientId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                string sql = "SELECT MealLogId, ClientId, DietPlanId, DietMealId, LogDate, MealType, MealName, PortionSize, Calories, ISNULL(ProteinGrams, 0) AS ProteinGrams, ISNULL(CarbsGrams, 0) AS CarbsGrams, ISNULL(FatsGrams, 0) AS FatsGrams, ISNULL(WaterLiters, 0) AS WaterLiters, IsCompleted, ClientNotes, CoachFeedback, CreatedAt FROM ClientMealLogs WHERE ClientId = @p0";
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

                return db.Database.SqlQuery<ClientMealLogItemDto>(sql, parameters.ToArray()).ToList();
            }
            catch
            {
                return new List<ClientMealLogItemDto>();
            }
        }

        // ================================================================
        // 1. GET: /Client/Diet or /Client/Diet/Index
        // Main Nutrition & Meal Hub: Today's targets, consumed macros, live meals
        // ================================================================
        [HttpGet]
        public ActionResult Index(string clientId = null)
        {
            if (!string.IsNullOrWhiteSpace(clientId) && !AuthorizationHelper.IsClientOwner(Session, clientId))
            {
                return new HttpStatusCodeResult(403, "Access Denied: You are not authorized to view another client's nutrition hub.");
            }

            var client = GetCurrentClient(clientId);
            if (client == null) return RedirectToAction("Login", "Account", new { area = "Client" });

            var clientSummary = MapClientSummary(client);

            // Fetch Active Diet Plan
            var activePlan = db.DietPlans
                .Include(p => p.Trainers)
                .Include(p => p.DietMeals)
                .Where(p => p.ClientId == client.ClientId && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (activePlan == null)
            {
                activePlan = db.DietPlans
                    .Include(p => p.Trainers)
                    .Include(p => p.DietMeals)
                    .Where(p => p.ClientId == client.ClientId)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var todayLogs = GetMealLogsForClient(client.ClientId, today, tomorrow);

            int targetCal = activePlan != null ? activePlan.DailyCalorieTarget : 0;
            int targetProt = activePlan != null ? activePlan.TargetProteinGrams : 0;
            int targetCarb = activePlan != null ? activePlan.TargetCarbsGrams : 0;
            int targetFat = activePlan != null ? activePlan.TargetFatsGrams : 0;
            decimal targetWater = activePlan != null && activePlan.WaterIntakeLiters.HasValue ? activePlan.WaterIntakeLiters.Value : 0m;

            int consumedCal = todayLogs.Sum(l => l.Calories);
            int consumedProt = todayLogs.Sum(l => l.ProteinGrams);
            int consumedCarb = todayLogs.Sum(l => l.CarbsGrams);
            int consumedFat = todayLogs.Sum(l => l.FatsGrams);
            decimal consumedWater = todayLogs.Sum(l => l.WaterLiters);

            int remainingCal = Math.Max(0, targetCal - consumedCal);
            int calPercent = targetCal > 0 ? (int)Math.Min(100, Math.Round((double)consumedCal / targetCal * 100)) : 0;
            int protPercent = targetProt > 0 ? (int)Math.Min(100, Math.Round((double)consumedProt / targetProt * 100)) : 0;
            int carbPercent = targetCarb > 0 ? (int)Math.Min(100, Math.Round((double)consumedCarb / targetCarb * 100)) : 0;
            int fatPercent = targetFat > 0 ? (int)Math.Min(100, Math.Round((double)consumedFat / targetFat * 100)) : 0;

            int waterGlasses = (int)Math.Round((double)consumedWater * 4); // ~250ml per glass
            int targetWaterGlasses = (int)Math.Round((double)targetWater * 4);

            string compliance = "On Track";
            if (targetCal == 0) compliance = "No Plan Prescribed";
            else if (consumedCal == 0) compliance = "Awaiting First Meal";
            else if (consumedCal >= targetCal * 0.9 && consumedCal <= targetCal * 1.1) compliance = "Target Met";
            else if (consumedCal > targetCal * 1.1) compliance = "Calorie Surplus";

            // Map today's prescribed meals
            var todayMealsList = new List<DietMealDto>();
            if (activePlan != null && activePlan.DietMeals != null)
            {
                var todayDayName = today.DayOfWeek.ToString();

                var planMeals = activePlan.DietMeals
                    .Where(m => string.IsNullOrWhiteSpace(m.DayOfWeek)
                             || m.DayOfWeek.Equals("Daily Routine", StringComparison.OrdinalIgnoreCase)
                             || m.DayOfWeek.Equals("Everyday", StringComparison.OrdinalIgnoreCase)
                             || m.DayOfWeek.Equals("All", StringComparison.OrdinalIgnoreCase)
                             || m.DayOfWeek.Equals(todayDayName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(m => m.OrderIndex)
                    .ThenBy(m => m.DietMealId)
                    .ToList();

                if (!planMeals.Any())
                {
                    planMeals = activePlan.DietMeals
                        .OrderBy(m => m.OrderIndex)
                        .Take(4)
                        .ToList();
                }

                foreach (var m in planMeals)
                {
                    // Check if this meal was already logged today
                    var logged = todayLogs.FirstOrDefault(l =>
                        (l.DietMealId.HasValue && l.DietMealId.Value == m.DietMealId)
                        || (l.MealType.Equals(m.MealType, StringComparison.OrdinalIgnoreCase) && l.MealName.Equals(m.MealName, StringComparison.OrdinalIgnoreCase))
                    );

                    todayMealsList.Add(new DietMealDto
                    {
                        DietMealId = m.DietMealId,
                        DietPlanId = m.DietPlanId,
                        DayOfWeek = m.DayOfWeek,
                        MealType = m.MealType,
                        ScheduledTime = !string.IsNullOrWhiteSpace(m.ScheduledTime) ? m.ScheduledTime : "Flexible",
                        MealName = m.MealName,
                        IngredientsAndPortion = !string.IsNullOrWhiteSpace(m.IngredientsAndPortion) ? m.IngredientsAndPortion : m.Ingredients,
                        Calories = m.Calories,
                        ProteinGrams = m.ProteinGrams ?? 0,
                        CarbsGrams = m.CarbsGrams ?? 0,
                        FatsGrams = m.FatsGrams ?? 0,
                        Instructions = m.Instructions,
                        ServingSize = m.ServingSize,
                        Description = m.Description,
                        Allergens = m.Allergens,
                        DietaryInfo = m.DietaryInfo,
                        TrainerNotes = m.TrainerNotes,
                        ImagePath = m.ImagePath,
                        IsConsumedToday = logged != null,
                        ConsumedLogId = logged != null ? (int?)logged.MealLogId : null,
                        ConsumedTime = logged != null ? logged.LogDate.ToString("hh:mm tt") : null,
                        ClientNotes = logged != null ? logged.ClientNotes : null
                    });
                }
            }

            var recentLogs = GetMealLogsForClient(client.ClientId).Take(5).ToList();

            var viewModel = new DietHubViewModel
            {
                Client = clientSummary,
                ActivePlan = activePlan,
                TodaySummary = new TodayNutritionSummaryDto
                {
                    TargetCalories = targetCal,
                    ConsumedCalories = consumedCal,
                    RemainingCalories = remainingCal,
                    CalorieProgressPercent = calPercent,
                    TargetProtein = targetProt,
                    ConsumedProtein = consumedProt,
                    ProteinPercent = protPercent,
                    TargetCarbs = targetCarb,
                    ConsumedCarbs = consumedCarb,
                    CarbsPercent = carbPercent,
                    TargetFats = targetFat,
                    ConsumedFats = consumedFat,
                    FatsPercent = fatPercent,
                    TargetWaterLiters = targetWater,
                    ConsumedWaterLiters = consumedWater,
                    WaterGlasses = waterGlasses,
                    TargetWaterGlasses = targetWaterGlasses,
                    TotalMealsScheduled = todayMealsList.Count,
                    MealsConsumedCount = todayMealsList.Count(m => m.IsConsumedToday),
                    ComplianceStatus = compliance
                },
                TodayMeals = todayMealsList,
                RecentLogs = recentLogs,
                TodayDateFormatted = today.ToString("dddd, MMMM d, yyyy"),
                TodayDayOfWeek = today.DayOfWeek.ToString()
            };

            return View(viewModel);
        }

        // ================================================================
        // 2. GET: /Client/Diet/Plan
        // Prescribed Diet Plan Overview: Full 7-Day meal blueprint & guidelines
        // ================================================================
        [HttpGet]
        public ActionResult Plan(int? id, string clientId = null)
        {
            if (!string.IsNullOrWhiteSpace(clientId) && !AuthorizationHelper.IsClientOwner(Session, clientId))
            {
                return new HttpStatusCodeResult(403, "Access Denied: You are not authorized to view another client's diet plan.");
            }

            var client = GetCurrentClient(clientId);
            if (client == null) return RedirectToAction("Index");

            var clientSummary = MapClientSummary(client);

            DietPlans plan = null;
            if (id.HasValue && id.Value > 0)
            {
                plan = db.DietPlans
                    .Include(p => p.Trainers)
                    .Include(p => p.DietMeals)
                    .FirstOrDefault(p => p.DietPlanId == id.Value && p.ClientId == client.ClientId);

                if (plan == null)
                {
                    return new HttpStatusCodeResult(403, "Access Denied: The requested diet plan does not belong to you or does not exist.");
                }
            }

            if (plan == null)
            {
                plan = db.DietPlans
                    .Include(p => p.Trainers)
                    .Include(p => p.DietMeals)
                    .Where(p => p.ClientId == client.ClientId && p.IsActive)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();
            }

            if (plan == null)
            {
                plan = db.DietPlans
                    .Include(p => p.Trainers)
                    .Include(p => p.DietMeals)
                    .Where(p => p.ClientId == client.ClientId)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();
            }

            var allMappedMeals = new List<DietMealDto>();
            var groupedMeals = new Dictionary<string, List<DietMealDto>>(StringComparer.OrdinalIgnoreCase);

            if (plan != null && plan.DietMeals != null)
            {
                foreach (var m in plan.DietMeals.OrderBy(m => m.OrderIndex).ThenBy(m => m.DietMealId))
                {
                    var dto = new DietMealDto
                    {
                        DietMealId = m.DietMealId,
                        DietPlanId = m.DietPlanId,
                        DayOfWeek = !string.IsNullOrWhiteSpace(m.DayOfWeek) ? m.DayOfWeek : "Daily Routine",
                        MealType = m.MealType,
                        ScheduledTime = !string.IsNullOrWhiteSpace(m.ScheduledTime) ? m.ScheduledTime : "Flexible",
                        MealName = m.MealName,
                        IngredientsAndPortion = !string.IsNullOrWhiteSpace(m.IngredientsAndPortion) ? m.IngredientsAndPortion : m.Ingredients,
                        Calories = m.Calories,
                        ProteinGrams = m.ProteinGrams ?? 0,
                        CarbsGrams = m.CarbsGrams ?? 0,
                        FatsGrams = m.FatsGrams ?? 0,
                        Instructions = m.Instructions,
                        ServingSize = m.ServingSize,
                        Description = m.Description,
                        Allergens = m.Allergens,
                        DietaryInfo = m.DietaryInfo,
                        TrainerNotes = m.TrainerNotes,
                        ImagePath = m.ImagePath
                    };

                    allMappedMeals.Add(dto);

                    string groupKey = dto.DayOfWeek;
                    if (!groupedMeals.ContainsKey(groupKey))
                    {
                        groupedMeals[groupKey] = new List<DietMealDto>();
                    }
                    groupedMeals[groupKey].Add(dto);
                }
            }

            var pastPlans = db.DietPlans
                .Where(p => p.ClientId == client.ClientId)
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            var viewModel = new DietPlanDetailsViewModel
            {
                Client = clientSummary,
                ActivePlan = plan,
                GroupedMeals = groupedMeals,
                AllMeals = allMappedMeals,
                PastPlans = pastPlans,
                TotalDailyCalories = plan != null ? plan.DailyCalorieTarget : allMappedMeals.Sum(m => m.Calories),
                TotalDailyProtein = plan != null ? plan.TargetProteinGrams : allMappedMeals.Sum(m => m.ProteinGrams),
                TotalDailyCarbs = plan != null ? plan.TargetCarbsGrams : allMappedMeals.Sum(m => m.CarbsGrams),
                TotalDailyFats = plan != null ? plan.TargetFatsGrams : allMappedMeals.Sum(m => m.FatsGrams)
            };

            return View(viewModel);
        }

        // ================================================================
        // 3. GET: /Client/Diet/Calendar (FR-C-62)
        // Interactive Diet Calendar + Meal History Tab
        // ================================================================
        [HttpGet]
        public ActionResult Calendar(int? month, int? year, string search = null, string mealType = null, DateTime? fromDate = null, DateTime? toDate = null, string status = null, string clientId = null)
        {
            if (!string.IsNullOrWhiteSpace(clientId) && !AuthorizationHelper.IsClientOwner(Session, clientId))
            {
                return new HttpStatusCodeResult(403, "Access Denied: You are not authorized to view another client's diet calendar.");
            }

            var client = GetCurrentClient(clientId);
            if (client == null) return RedirectToAction("Index");

            var clientSummary = MapClientSummary(client);

            var today = DateTime.Today;
            int targetMonth = month ?? today.Month;
            int targetYear = year ?? today.Year;

            if (targetMonth < 1) { targetMonth = 12; targetYear--; }
            if (targetMonth > 12) { targetMonth = 1; targetYear++; }

            var firstDayOfMonth = new DateTime(targetYear, targetMonth, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            var activePlan = db.DietPlans
                .Include(p => p.Trainers)
                .Where(p => p.ClientId == client.ClientId && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (activePlan == null)
            {
                activePlan = db.DietPlans
                    .Include(p => p.Trainers)
                    .Where(p => p.ClientId == client.ClientId)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();
            }

            int targetCal = activePlan != null ? activePlan.DailyCalorieTarget : 1800;

            // Fetch logs for this month
            var logsInMonth = GetMealLogsForClient(client.ClientId, firstDayOfMonth, lastDayOfMonth.AddDays(1).AddSeconds(-1));

            // Calculate start padding for week start (Monday)
            int startDayOffset = ((int)firstDayOfMonth.DayOfWeek + 6) % 7; // 0 = Monday, 6 = Sunday
            var calendarGridStart = firstDayOfMonth.AddDays(-startDayOffset);

            int totalCells = (startDayOffset + lastDayOfMonth.Day > 35) ? 42 : 35;

            var calendarDays = new List<DietCalendarDayDto>();
            int daysOnTargetCount = 0;
            int loggedDaysCount = 0;
            int totalMonthCalories = 0;

            for (int i = 0; i < totalCells; i++)
            {
                var date = calendarGridStart.AddDays(i);
                bool isCurrentMonth = date.Month == targetMonth;
                bool isToday = date.Date == today;
                bool isPast = date.Date < today;

                var dayLogs = logsInMonth.Where(l => l.LogDate.Date == date.Date).ToList();
                int dayCalories = dayLogs.Sum(l => l.Calories);
                int dayMealsCount = dayLogs.Count;
                bool hasLogs = dayMealsCount > 0;

                string badge = "No Log";
                string badgeClass = "secondary";

                if (hasLogs)
                {
                    if (isCurrentMonth)
                    {
                        loggedDaysCount++;
                        totalMonthCalories += dayCalories;
                    }

                    if (dayCalories >= targetCal * 0.9 && dayCalories <= targetCal * 1.1)
                    {
                        badge = "Target Met (" + dayCalories + " kcal)";
                        badgeClass = "success";
                        if (isCurrentMonth) daysOnTargetCount++;
                    }
                    else if (dayCalories > targetCal * 1.1)
                    {
                        badge = "Surplus (" + dayCalories + " kcal)";
                        badgeClass = "warning";
                    }
                    else if (dayCalories > 0 && dayCalories < targetCal * 0.9)
                    {
                        badge = "Deficit (" + dayCalories + " kcal)";
                        badgeClass = "info";
                    }
                }
                else if (isPast && isCurrentMonth)
                {
                    badge = "Not Logged";
                    badgeClass = "light text-muted border";
                }
                else if (isToday)
                {
                    badge = "Today";
                    badgeClass = "primary";
                }

                calendarDays.Add(new DietCalendarDayDto
                {
                    Date = date,
                    DayNumber = date.Day,
                    DayName = date.DayOfWeek.ToString(),
                    IsCurrentMonth = isCurrentMonth,
                    IsToday = isToday,
                    IsPast = isPast,
                    TotalCaloriesConsumed = dayCalories,
                    TargetCalories = targetCal,
                    MealsLoggedCount = dayMealsCount,
                    HasLogs = hasLogs,
                    StatusBadge = badge,
                    StatusClass = badgeClass,
                    LoggedMeals = dayLogs
                });
            }

            int adherenceRate = loggedDaysCount > 0 ? (int)Math.Min(100, Math.Round((double)daysOnTargetCount / loggedDaysCount * 100)) : 0;
            int avgCalories = loggedDaysCount > 0 ? (int)Math.Round((double)totalMonthCalories / loggedDaysCount) : 0;

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            {
                TempData["ErrorMessage"] = "From Date cannot be later than To Date.";
                var temp = fromDate;
                fromDate = toDate;
                toDate = temp;
            }

            // Fetch history logs with date-range, status, mealType, and search filtering (FR-C-62)
            var historyQuery = db.ClientMealLogs
                .Where(l => l.ClientId == client.ClientId)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                historyQuery = historyQuery.Where(l => DbFunctions.TruncateTime(l.LogDate) >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date;
                historyQuery = historyQuery.Where(l => DbFunctions.TruncateTime(l.LogDate) <= to);
            }

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                {
                    historyQuery = historyQuery.Where(l => l.IsCompleted);
                }
                else if (status.Equals("incomplete", StringComparison.OrdinalIgnoreCase) || status.Equals("partial", StringComparison.OrdinalIgnoreCase))
                {
                    historyQuery = historyQuery.Where(l => !l.IsCompleted);
                }
            }

            if (!string.IsNullOrWhiteSpace(mealType) && !mealType.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                historyQuery = historyQuery.Where(l => l.MealType.Equals(mealType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                historyQuery = historyQuery.Where(l =>
                    l.MealName.Contains(term)
                    || l.MealType.Contains(term)
                    || (l.ClientNotes != null && l.ClientNotes.Contains(term))
                    || (l.CoachFeedback != null && l.CoachFeedback.Contains(term))
                );
            }

            var allClientLogs = historyQuery
                .OrderByDescending(l => l.LogDate)
                .Select(l => new ClientMealLogItemDto
                {
                    MealLogId = l.MealLogId,
                    ClientId = l.ClientId,
                    DietPlanId = l.DietPlanId,
                    DietMealId = l.DietMealId,
                    LogDate = l.LogDate,
                    MealType = l.MealType,
                    MealName = l.MealName,
                    PortionSize = l.PortionSize,
                    Calories = l.Calories,
                    ProteinGrams = l.ProteinGrams ?? 0,
                    CarbsGrams = l.CarbsGrams ?? 0,
                    FatsGrams = l.FatsGrams ?? 0,
                    WaterLiters = l.WaterLiters ?? 0m,
                    IsCompleted = l.IsCompleted,
                    ClientNotes = l.ClientNotes,
                    CoachFeedback = l.CoachFeedback,
                    CreatedAt = l.CreatedAt
                })
                .ToList();

            var viewModel = new DietCalendarViewModel
            {
                Client = clientSummary,
                ActivePlan = activePlan,
                CurrentMonth = targetMonth,
                CurrentYear = targetYear,
                MonthName = firstDayOfMonth.ToString("MMMM yyyy"),
                DaysInMonth = calendarDays,
                AdherenceRatePercent = adherenceRate,
                DaysOnTarget = daysOnTargetCount,
                AvgDailyCalories = avgCalories,
                TotalLoggedMealsThisMonth = logsInMonth.Count,
                HistoryLogs = allClientLogs,
                SearchTerm = search,
                FilterMealType = mealType,
                FilterStatus = status,
                FromDate = fromDate,
                ToDate = toDate
            };

            return View(viewModel);
        }

        // ================================================================
        // 4. GET: /Client/Diet/History (FR-C-62)
        // Direct route to Meal History with Date Range & Status Filtering
        // ================================================================
        [HttpGet]
        public ActionResult History(string search = null, string mealType = null, DateTime? fromDate = null, DateTime? toDate = null, string status = null, string clientId = null)
        {
            ViewBag.ActiveTab = "history";
            return Calendar(null, null, search, mealType, fromDate, toDate, status, clientId);
        }

        // ================================================================
        // 5. POST: /Client/Diet/LogMeal
        // Record Consumed Meal & Notes (Modal Submission)
        // ================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogMeal(LogMealInputModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.ClientId) && !AuthorizationHelper.IsClientOwner(Session, model.ClientId))
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Access Denied: You are not authorized to record meals for another client." });
                return new HttpStatusCodeResult(403, "Access Denied: You are not authorized to record meals for another client.");
            }

            var client = GetCurrentClient(model.ClientId);
            if (client == null)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Client session not found." });
                return RedirectToAction("Index");
            }

            if (model.DietPlanId.HasValue && !db.DietPlans.Any(p => p.DietPlanId == model.DietPlanId.Value && p.ClientId == client.ClientId))
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Access Denied: The specified diet plan does not belong to you." });
                return new HttpStatusCodeResult(403, "Access Denied: The specified diet plan does not belong to you.");
            }

            if (model.DietMealId.HasValue && !db.DietMeals.Any(m => m.DietMealId == model.DietMealId.Value && m.DietPlans.ClientId == client.ClientId))
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Access Denied: The specified meal does not belong to your diet plan." });
                return new HttpStatusCodeResult(403, "Access Denied: The specified meal does not belong to your diet plan.");
            }

            try
            {
                var logDate = model.LogDate ?? DateTime.Now;
                string mealName = !string.IsNullOrWhiteSpace(model.MealName) ? model.MealName : (model.MealType + " Log");
                string mealType = !string.IsNullOrWhiteSpace(model.MealType) ? model.MealType : "Breakfast";
                string portion = !string.IsNullOrWhiteSpace(model.PortionSize) ? model.PortionSize : "Standard Portion";
                string notes = model.ClientNotes;

                db.Database.ExecuteSqlCommand(
                    @"INSERT INTO ClientMealLogs 
                      (ClientId, DietPlanId, DietMealId, LogDate, MealType, MealName, PortionSize, Calories, ProteinGrams, CarbsGrams, FatsGrams, WaterLiters, IsCompleted, ClientNotes, CreatedAt)
                      VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, 1, @p12, GETDATE())",
                    client.ClientId,
                    model.DietPlanId.HasValue ? (object)model.DietPlanId.Value : DBNull.Value,
                    model.DietMealId.HasValue ? (object)model.DietMealId.Value : DBNull.Value,
                    logDate,
                    mealType,
                    mealName,
                    portion,
                    model.Calories,
                    model.ProteinGrams,
                    model.CarbsGrams,
                    model.FatsGrams,
                    model.WaterLiters,
                    !string.IsNullOrWhiteSpace(notes) ? (object)notes : DBNull.Value
                );

                if (Request.IsAjaxRequest())
                {
                    return Json(new
                    {
                        success = true,
                        message = "Meal successfully logged! Keep fueling your body right!",
                        mealName = mealName,
                        calories = model.Calories
                    });
                }

                TempData["SuccessMessage"] = "Meal recorded successfully! Nutrition tracking updated.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Error saving meal log: " + ex.Message });

                TempData["ErrorMessage"] = "Could not record meal: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ================================================================
        // 6. POST: /Client/Diet/QuickToggleMeal (FR-S-11)
        // Quick 1-Click "Mark Consumed" / "Undo" for prescribed meals
        // ================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult QuickToggleMeal(int dietMealId, bool markConsumed, string notes = null)
        {
            var client = GetCurrentClient();
            if (client == null)
            {
                return Json(new { success = false, message = "Session expired." });
            }

            // FR-S-04: Verify target meal belongs to a diet plan assigned to the authenticated client
            var meal = db.DietMeals.Include(m => m.DietPlans).FirstOrDefault(m => m.DietMealId == dietMealId);
            if (meal == null || meal.DietPlans == null || meal.DietPlans.ClientId != client.ClientId)
            {
                return Json(new { success = false, message = "Access Denied: The requested meal does not belong to your diet plan." });
            }

            try
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var existingLog = db.Database.SqlQuery<ClientMealLogItemDto>(
                    "SELECT TOP 1 MealLogId, ClientId, DietPlanId, DietMealId, LogDate, MealType, MealName, PortionSize, Calories, ProteinGrams, CarbsGrams, FatsGrams, WaterLiters, IsCompleted, ClientNotes, CoachFeedback, CreatedAt FROM ClientMealLogs WHERE ClientId = @p0 AND DietMealId = @p1 AND LogDate >= @p2 AND LogDate < @p3",
                    client.ClientId, dietMealId, today, tomorrow
                ).FirstOrDefault();

                if (markConsumed)
                {
                    if (existingLog == null)
                    {

                        db.Database.ExecuteSqlCommand(
                            @"INSERT INTO ClientMealLogs 
                              (ClientId, DietPlanId, DietMealId, LogDate, MealType, MealName, PortionSize, Calories, ProteinGrams, CarbsGrams, FatsGrams, WaterLiters, IsCompleted, ClientNotes, CreatedAt)
                              VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, 0.5, 1, @p11, GETDATE())",
                            client.ClientId,
                            meal.DietPlanId,
                            meal.DietMealId,
                            DateTime.Now,
                            meal.MealType,
                            meal.MealName,
                            !string.IsNullOrWhiteSpace(meal.ServingSize) ? meal.ServingSize : meal.IngredientsAndPortion,
                            meal.Calories,
                            meal.ProteinGrams ?? 0,
                            meal.CarbsGrams ?? 0,
                            meal.FatsGrams ?? 0,
                            !string.IsNullOrWhiteSpace(notes) ? (object)notes : DBNull.Value
                        );
                    }
                    return Json(new { success = true, isConsumed = true, message = "Meal marked as consumed! Macros updated." });
                }
                else
                {
                    if (existingLog != null)
                    {
                        db.Database.ExecuteSqlCommand("DELETE FROM ClientMealLogs WHERE MealLogId = @p0", existingLog.MealLogId);
                    }
                    return Json(new { success = true, isConsumed = false, message = "Meal marked as unconsumed." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating meal: " + ex.Message });
            }
        }

        // ================================================================
        // 7. POST: /Client/Diet/LogWater (FR-S-11)
        // Quick 1-Click Hydration Logging (+250ml / +1 glass)
        // ================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogWater(decimal amountLiters = 0.25m)
        {
            var client = GetCurrentClient();
            if (client == null)
            {
                return Json(new { success = false, message = "Session expired." });
            }

            var activePlan = db.DietPlans.FirstOrDefault(p => p.ClientId == client.ClientId && p.IsActive);
            if (activePlan == null || !activePlan.WaterIntakeLiters.HasValue || activePlan.WaterIntakeLiters.Value <= 0)
            {
                return Json(new { success = false, message = "Water logging is locked: Your assigned trainer has not set a daily water target in your diet plan yet." });
            }

            try
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                db.Database.ExecuteSqlCommand(
                    @"INSERT INTO ClientMealLogs 
                      (ClientId, DietPlanId, DietMealId, LogDate, MealType, MealName, PortionSize, Calories, ProteinGrams, CarbsGrams, FatsGrams, WaterLiters, IsCompleted, ClientNotes, CreatedAt)
                      VALUES (@p0, NULL, NULL, @p1, 'Hydration', 'Water Intake', '1 Glass (250ml)', 0, 0, 0, 0, @p2, 1, 'Logged quick hydration', GETDATE())",
                    client.ClientId, DateTime.Now, amountLiters
                );

                var totalWater = db.Database.SqlQuery<decimal?>(
                    "SELECT SUM(WaterLiters) FROM ClientMealLogs WHERE ClientId = @p0 AND LogDate >= @p1 AND LogDate < @p2",
                    client.ClientId, today, tomorrow
                ).FirstOrDefault() ?? 0.0m;

                return Json(new
                {
                    success = true,
                    totalWaterLiters = Math.Round(totalWater, 2),
                    totalGlasses = (int)Math.Round((double)totalWater * 4),
                    message = "Hydration recorded! Keep drinking clean water."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
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
