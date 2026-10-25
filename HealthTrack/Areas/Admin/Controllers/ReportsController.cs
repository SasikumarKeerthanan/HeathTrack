using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Admin.ViewModels;
using HealthTrack.Areas.Trainer.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Admin.Controllers
{
    /// Controller for Admin Analytics and System Reports.
    [AdminAuthorize]
    public class ReportsController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // GET: /Admin/Reports
        [HttpGet]
        public ActionResult Index(string range = "30days", DateTime? startDate = null, DateTime? endDate = null)
        {
            var model = new AdminReportsViewModel();

            // 1. Calculate Date Filter Range
            DateTime today = DateTime.Today;
            DateTime start;
            DateTime end = today.AddDays(1); // Include entire current day up to midnight

            if (range == "custom" && startDate.HasValue && endDate.HasValue)
            {
                start = startDate.Value.Date;
                end = endDate.Value.Date.AddDays(1);
                model.DateRange = "custom";
                model.StartDate = startDate.Value;
                model.EndDate = endDate.Value;
            }
            else
            {
                switch (range?.ToLower())
                {
                    case "7days":
                        start = today.AddDays(-7);
                        model.DateRange = "7days";
                        break;
                    case "90days":
                        start = today.AddDays(-90);
                        model.DateRange = "90days";
                        break;
                    case "year":
                        start = today.AddYears(-1);
                        model.DateRange = "year";
                        break;
                    case "all":
                        start = new DateTime(2020, 1, 1);
                        model.DateRange = "all";
                        break;
                    case "30days":
                    default:
                        start = today.AddDays(-30);
                        model.DateRange = "30days";
                        break;
                }

                model.StartDate = start;
                model.EndDate = today;
            }

            model.GeneratedAt = DateTime.Now;

            // 2. Client & Trainer User Statistics
            var allClients = db.Clients.ToList();
            var allTrainers = db.Trainers.ToList();

            model.TotalClients = allClients.Count;
            model.ActiveClients = allClients.Count(c => c.IsActive);
            model.InactiveClients = allClients.Count(c => !c.IsActive);
            model.NewClientsInPeriod = allClients.Count(c => c.CreatedAt >= start && c.CreatedAt < end);

            model.TotalTrainers = allTrainers.Count;
            model.ActiveTrainers = allTrainers.Count(t => t.IsActive);
            model.InactiveTrainers = allTrainers.Count(t => !t.IsActive);
            model.NewTrainersInPeriod = allTrainers.Count(t => t.CreatedAt >= start && t.CreatedAt < end);

            // 3. Appointment Statistics in Filter Period
            var appointmentsQuery = db.Appointments
                .Include(a => a.Clients)
                .Include(a => a.Trainers)
                .Where(a => a.AppointmentDate >= start && a.AppointmentDate < end)
                .ToList();

            model.TotalAppointments = appointmentsQuery.Count;
            model.PendingAppointments = appointmentsQuery.Count(a => a.Status == "Pending");
            model.ApprovedAppointments = appointmentsQuery.Count(a => a.Status == "Approved");
            model.CompletedAppointments = appointmentsQuery.Count(a => a.Status == "Completed");
            model.CancelledAppointments = appointmentsQuery.Count(a => a.Status == "Cancelled" || a.Status == "Rejected");

            // 4. Workout Engagement & System Usage in Filter Period
            var workoutLogs = db.ClientWorkoutLogs
                .Include(w => w.Clients)
                .Where(w => w.LogDate >= start && w.LogDate < end)
                .ToList();

            model.TotalWorkoutLogs = workoutLogs.Count;
            model.TotalCaloriesBurned = workoutLogs.Sum(w => (long)w.CaloriesBurned);

            var mealLogs = db.ClientMealLogs
                .Where(m => m.LogDate >= start && m.LogDate < end)
                .ToList();
            model.TotalMealLogs = mealLogs.Count;

            var progressRecords = db.ClientProgressRecords
                .Where(p => p.RecordedDate >= start && p.RecordedDate < end)
                .ToList();
            model.TotalStepsLogged = progressRecords.Where(p => p.StepsCount.HasValue).Sum(p => (long)p.StepsCount.Value);

            var auditLogs = db.AuthAuditLogs
                .Where(l => l.TimestampUtc >= start && l.TimestampUtc < end)
                .ToList();
            model.TotalSecurityEvents = auditLogs.Count;

            // 5. Chart 1: Registration Trend (Past 6 Months)
            DateTime sixMonthsAgo = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
            for (int i = 0; i < 6; i++)
            {
                DateTime mStart = sixMonthsAgo.AddMonths(i);
                DateTime mEnd = mStart.AddMonths(1);
                string label = mStart.ToString("MMM yyyy");

                model.TrendLabels.Add(label);
                model.ClientTrendData.Add(allClients.Count(c => c.CreatedAt >= mStart && c.CreatedAt < mEnd));
                model.TrainerTrendData.Add(allTrainers.Count(t => t.CreatedAt >= mStart && t.CreatedAt < mEnd));
            }

            // 6. Trainer Activity Breakdown
            var allAppointments = db.Appointments.ToList();
            foreach (var trainer in allTrainers)
            {
                int assignedClients = allClients.Count(c => c.AssignedTrainerId == trainer.TrainerId && c.IsActive);
                int trainerAppts = allAppointments.Count(a => a.TrainerId == trainer.TrainerId);
                int completedAppts = allAppointments.Count(a => a.TrainerId == trainer.TrainerId && a.Status == "Completed");
                int maxCap = trainer.MaxClientCapacity > 0 ? trainer.MaxClientCapacity : 10;
                int utilPercent = (int)Math.Min(100, Math.Round((double)assignedClients / maxCap * 100));

                model.TrainerActivities.Add(new TrainerActivityItem
                {
                    TrainerId = trainer.TrainerId,
                    FullName = trainer.FullName,
                    Specialization = trainer.PrimarySpecialization ?? trainer.Specialization ?? "General Fitness",
                    AssignedClientsCount = assignedClients,
                    MaxCapacity = maxCap,
                    TotalAppointmentsCount = trainerAppts,
                    CompletedSessionsCount = completedAppts,
                    UtilizationPercentage = utilPercent,
                    IsActive = trainer.IsActive
                });
            }

            // 7. Recent System Engagement List (Top 10)
            var recentWorkouts = workoutLogs
                .OrderByDescending(w => w.LogDate)
                .Take(5)
                .Select(w => new EngagementActivityItem
                {
                    ActivityDate = w.LogDate,
                    ClientName = w.Clients != null ? w.Clients.FullName : w.ClientId,
                    ActivityType = "Workout",
                    Title = w.SessionName ?? "Workout Session",
                    DetailSummary = string.Format("{0} mins | {1} kcal burned", w.DurationMinutes, w.CaloriesBurned),
                    Status = w.IsCompleted ? "Completed" : "In Progress"
                }).ToList();

            var recentAppts = appointmentsQuery
                .OrderByDescending(a => a.AppointmentDate)
                .Take(5)
                .Select(a => new EngagementActivityItem
                {
                    ActivityDate = a.AppointmentDate,
                    ClientName = a.Clients != null ? a.Clients.FullName : a.ClientId,
                    ActivityType = "Consultation",
                    Title = a.Title ?? "Coach Consultation",
                    DetailSummary = string.Format("Coach: {0}", a.Trainers != null ? a.Trainers.FullName : a.TrainerId),
                    Status = a.Status
                }).ToList();

            model.RecentActivities.AddRange(recentWorkouts);
            model.RecentActivities.AddRange(recentAppts);
            model.RecentActivities = model.RecentActivities.OrderByDescending(a => a.ActivityDate).Take(10).ToList();

            return View(model);
        }

        private static Tuple<DateTime, DateTime, string> ResolveDateRange(string preset, DateTime? fromDate, DateTime? toDate)
        {
            DateTime today = DateTime.Today;
            DateTime end = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : today.AddDays(1).AddTicks(-1);
            string activePreset = string.IsNullOrEmpty(preset) ? "30days" : preset.ToLower();

            DateTime start;
            switch (activePreset)
            {
                case "7days":
                    start = today.AddDays(-7);
                    break;
                case "30days":
                    start = today.AddDays(-30);
                    break;
                case "90days":
                    start = today.AddDays(-90);
                    break;
                case "year":
                    start = today.AddYears(-1);
                    break;
                case "all":
                    start = new DateTime(2020, 1, 1);
                    break;
                case "custom":
                default:
                    if (fromDate.HasValue)
                    {
                        start = fromDate.Value.Date;
                        activePreset = "custom";
                    }
                    else
                    {
                        start = today.AddDays(-30);
                        activePreset = "30days";
                    }
                    break;
            }

            return Tuple.Create(start, end, activePreset);
        }

        // =====================================================================
        // 2. CLIENT PERFORMANCE REPORT (GET: /Admin/Reports/ClientPerformance)
        // FR-A-26: Generate Client Performance Reports
        // =====================================================================
        [HttpGet]
        public ActionResult ClientPerformance(string clientId = null, string preset = "30days", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var model = new AdminClientPerformanceReportViewModel();

            var allClients = db.Clients
                .Include(c => c.Trainers)
                .Include(c => c.ClientPreferences)
                .OrderBy(c => c.FullName)
                .ToList();

            model.AvailableClients = allClients.Select(c => new SelectListItem
            {
                Value = c.ClientId,
                Text = $"{c.FullName} ({c.Email})",
                Selected = (c.ClientId == clientId)
            }).ToList();

            var targetClient = allClients.FirstOrDefault(c => c.ClientId == clientId) ?? allClients.FirstOrDefault();
            if (targetClient == null)
            {
                model.ValidationError = "No client accounts exist in the system.";
                return View(model);
            }

            model.SelectedClientId = targetClient.ClientId;
            model.ClientName = targetClient.FullName;
            model.ClientEmail = targetClient.Email;
            model.Gender = targetClient.Gender ?? "Not Specified";
            model.ClientRegisteredDate = targetClient.CreatedAt;

            var clientPref = targetClient.ClientPreferences.FirstOrDefault();
            model.PrimaryGoal = clientPref != null && !string.IsNullOrWhiteSpace(clientPref.PrimaryGoal) ? clientPref.PrimaryGoal : "General Fitness";
            model.AssignedTrainerId = targetClient.AssignedTrainerId;
            model.AssignedTrainerName = targetClient.Trainers != null ? targetClient.Trainers.FullName : (!string.IsNullOrEmpty(targetClient.AssignedTrainerId) ? targetClient.AssignedTrainerId : "Unassigned");

            // Validate date range
            if (preset == "custom" && fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            {
                model.ValidationError = "The 'From Date' cannot be later than the 'To Date'. Please select a valid date range.";
                model.FromDate = fromDate.Value;
                model.ToDate = toDate.Value;
                model.DatePreset = "custom";
                return View(model);
            }

            var dateRange = ResolveDateRange(preset, fromDate, toDate);
            DateTime start = dateRange.Item1;
            DateTime end = dateRange.Item2;
            model.DatePreset = dateRange.Item3;
            model.FromDate = start;
            model.ToDate = end.Date;

            // Health measurement history and progress
            var progressRecords = db.ClientProgressRecords
                .Where(r => r.ClientId == targetClient.ClientId && r.RecordedDate >= start && r.RecordedDate <= end)
                .OrderBy(r => r.RecordedDate)
                .ToList();

            var allClientProgress = db.ClientProgressRecords
                .Where(r => r.ClientId == targetClient.ClientId)
                .OrderBy(r => r.RecordedDate)
                .ToList();

            var firstRec = progressRecords.FirstOrDefault() ?? allClientProgress.FirstOrDefault();
            var latestRec = progressRecords.LastOrDefault() ?? allClientProgress.LastOrDefault();

            model.BaselineWeightKg = firstRec != null ? (double)firstRec.WeightKg : (targetClient.HeightCm.HasValue ? 70.0 : 0.0);
            model.CurrentWeightKg = latestRec != null ? (double)latestRec.WeightKg : model.BaselineWeightKg;
            model.CurrentBmi = latestRec != null && latestRec.Bmi.HasValue ? (double)latestRec.Bmi.Value : 0.0;
            model.CurrentBodyFatPct = latestRec != null && latestRec.BodyFatPercentage.HasValue ? (double?)latestRec.BodyFatPercentage.Value : null;
            model.BloodPressure = "Not Recorded";
            model.RestingHeartRateBpm = latestRec != null && latestRec.HeartRateBpm.HasValue ? latestRec.HeartRateBpm : null;

            if (model.CurrentBmi <= 0) model.BmiCategory = "Not Recorded";
            else if (model.CurrentBmi < 18.5) model.BmiCategory = "Underweight";
            else if (model.CurrentBmi < 25.0) model.BmiCategory = "Normal Weight";
            else if (model.CurrentBmi < 30.0) model.BmiCategory = "Overweight";
            else model.BmiCategory = "Obese";

            // Workout completion and engagement (FR-S-40 via shared EngagementCalculationHelper)
            var engagement = EngagementCalculationHelper.CalculateClientEngagement(db, targetClient.ClientId, start, end);
            model.TotalWorkoutsScheduled = engagement.ScheduledWorkoutsCount;
            model.TotalWorkoutsCompleted = engagement.CompletedWorkoutsCount;
            model.TotalCaloriesBurned = engagement.TotalCaloriesBurned;
            model.TotalExerciseMinutes = engagement.TotalExerciseMinutes;

            var workoutLogs = db.ClientWorkoutLogs
                .Include(w => w.WorkoutPlans)
                .Where(w => w.ClientId == targetClient.ClientId && w.LogDate >= start && w.LogDate <= end)
                .OrderByDescending(w => w.LogDate)
                .ToList();

            // Goal progress
            var goals = db.ClientGoals
                .Where(g => g.ClientId == targetClient.ClientId)
                .ToList();

            var weightGoal = goals.FirstOrDefault(g => g.Category == "Weight");
            model.TargetWeightKg = weightGoal != null ? (double)weightGoal.TargetValue : model.CurrentWeightKg;

            foreach (var g in goals)
            {
                double range = Math.Abs((double)g.TargetValue - (double)g.BaselineValue);
                int progressPct = 100;
                if (range > 0)
                {
                    double diff = Math.Abs((double)g.CurrentValue - (double)g.BaselineValue);
                    progressPct = Math.Min(100, Math.Max(0, (int)Math.Round(diff / range * 100)));
                }

                model.GoalProgressItems.Add(new ClientGoalProgressItem
                {
                    GoalName = g.GoalName,
                    Category = g.Category,
                    BaselineValue = (double)g.BaselineValue,
                    CurrentValue = (double)g.CurrentValue,
                    TargetValue = (double)g.TargetValue,
                    Unit = g.Unit ?? "",
                    ProgressPercentage = progressPct,
                    StatusMessage = g.StatusMessage ?? "In Progress"
                });
            }

            // Meal tracking and adherence
            var activeDiet = db.DietPlans.FirstOrDefault(dp => dp.ClientId == targetClient.ClientId && dp.IsActive);
            model.PrescribedDailyCalories = activeDiet != null ? activeDiet.DailyCalorieTarget : 0;
            model.PrescribedWaterLiters = activeDiet != null && activeDiet.WaterIntakeLiters.HasValue ? (double)activeDiet.WaterIntakeLiters.Value : 0.0;

            var mealLogs = db.ClientMealLogs
                .Where(m => m.ClientId == targetClient.ClientId && m.LogDate >= start && m.LogDate <= end)
                .OrderByDescending(m => m.LogDate)
                .ToList();

            model.TotalMealsLogged = mealLogs.Count;
            var calRecords = progressRecords.Where(r => r.CaloriesConsumed > 0).ToList();
            model.AverageConsumedCalories = calRecords.Any() ? (int)Math.Round(calRecords.Average(r => r.CaloriesConsumed)) : 0;
            var waterRecords = progressRecords.Where(r => r.WaterIntakeMl > 0).ToList();
            model.AverageWaterLiters = waterRecords.Any() ? Math.Round(waterRecords.Average(r => (double)r.WaterIntakeMl) / 1000.0, 1) : 0.0;

            model.ChartLabels = progressRecords.Select(r => r.RecordedDate.ToString("MMM dd")).ToList();
            model.WeightHistory = progressRecords.Select(r => (double)r.WeightKg).ToList();
            model.CaloriesBurnedHistory = progressRecords.Select(r => r.CaloriesBurned).ToList();
            model.CaloriesConsumedHistory = progressRecords.Select(r => r.CaloriesConsumed).ToList();

            model.ProgressRecords = progressRecords.OrderByDescending(r => r.RecordedDate).Take(15).ToList();
            HealthDataProtectionHelper.DecryptProgressRecords(model.ProgressRecords);
            model.WorkoutLogs = workoutLogs.Take(15).ToList();
            model.MealLogs = mealLogs.Take(15).ToList();

            HealthAuditHelper.Log(
                actorUserId: SessionHelper.GetCurrentAdminId(Session) ?? "Admin",
                actorRole: "Administrator",
                clientId: targetClient.ClientId,
                recordType: "ClientProgress",
                recordId: null,
                action: "View",
                wasSuccessful: true,
                details: $"Viewed client performance and clinical health metrics report ({model.DatePreset})");

            return View(model);
        }

        // =====================================================================
        // 3. POPULAR WORKOUT ROUTINES REPORT (GET: /Admin/Reports/PopularRoutines)
        // FR-A-27: Generate Popular Workout Routine Reports
        // Reuses FR-T-49 algorithm across all authorized routines
        // =====================================================================
        [HttpGet]
        public ActionResult PopularRoutines(string goal = null, string trainerId = null, string preset = "all", DateTime? fromDate = null, DateTime? toDate = null, string status = "active")
        {
            var dateRange = ResolveDateRange(preset, fromDate, toDate);
            DateTime start = dateRange.Item1;
            DateTime end = dateRange.Item2;
            string activePreset = dateRange.Item3;

            var query = db.WorkoutPlans.AsQueryable();

            // FR-S-41: Popular routine rankings must exclude cancelled assignments
            if (string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => !p.IsActive);
            }
            else
            {
                // Default and all valid popularity rankings strictly exclude cancelled/inactive assignments
                query = query.Where(p => p.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(goal) && !string.Equals(goal, "All Goals", StringComparison.OrdinalIgnoreCase))
            {
                string g = goal.Trim();
                query = query.Where(p => p.Goal == g);
            }

            if (!string.IsNullOrWhiteSpace(trainerId) && !string.Equals(trainerId, "all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.TrainerId == trainerId);
            }

            if (activePreset != "all")
            {
                query = query.Where(p => p.CreatedAt >= start && p.CreatedAt <= end);
            }

            var planSummaries = query
                .Select(p => new
                {
                    p.WorkoutPlanId,
                    p.Title,
                    p.Goal,
                    p.DifficultyLevel,
                    p.DurationWeeks,
                    p.DaysPerWeek,
                    p.ClientId,
                    p.CreatedAt,
                    p.StartDate,
                    p.IsActive
                })
                .ToList();

            var groupedRoutines = planSummaries
                .GroupBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    RoutineTitle = g.Key,
                    TotalAssignments = g.Count(),
                    Goal = g.OrderByDescending(p => p.CreatedAt).Select(p => p.Goal).FirstOrDefault(x => !string.IsNullOrEmpty(x)),
                    DifficultyLevel = g.OrderByDescending(p => p.CreatedAt).Select(p => p.DifficultyLevel).FirstOrDefault(x => !string.IsNullOrEmpty(x)),
                    DurationWeeks = g.OrderByDescending(p => p.CreatedAt).Select(p => p.DurationWeeks).FirstOrDefault(),
                    DaysPerWeek = g.OrderByDescending(p => p.CreatedAt).Select(p => p.DaysPerWeek).FirstOrDefault(),
                    LatestAssignedDate = g.Max(p => (DateTime?)p.CreatedAt) ?? g.Max(p => (DateTime?)p.StartDate),
                    UniqueClientsCount = g.Select(p => p.ClientId).Distinct().Count()
                })
                .OrderByDescending(x => x.TotalAssignments)
                .ThenBy(x => x.RoutineTitle, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var rankedList = new List<PopularRoutineItemViewModel>();
            int rank = 1;
            foreach (var item in groupedRoutines)
            {
                rankedList.Add(new PopularRoutineItemViewModel
                {
                    Rank = rank++,
                    RoutineTitle = item.RoutineTitle,
                    Goal = !string.IsNullOrWhiteSpace(item.Goal) ? item.Goal : "General Fitness",
                    DifficultyLevel = !string.IsNullOrWhiteSpace(item.DifficultyLevel) ? item.DifficultyLevel : "All Levels",
                    DurationWeeks = item.DurationWeeks,
                    DaysPerWeek = item.DaysPerWeek,
                    TotalAssignments = item.TotalAssignments,
                    UniqueClientsCount = item.UniqueClientsCount,
                    LatestAssignedDate = item.LatestAssignedDate
                });
            }

            var totalValidAssignments = rankedList.Sum(r => r.TotalAssignments);
            var topRoutine = rankedList.FirstOrDefault();

            var allTrainers = db.Trainers.OrderBy(t => t.FullName).ToList();
            var trainerOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "all", Text = "All Trainers (System-wide)", Selected = string.IsNullOrEmpty(trainerId) || trainerId == "all" }
            };
            trainerOptions.AddRange(allTrainers.Select(t => new SelectListItem
            {
                Value = t.TrainerId,
                Text = $"{t.FullName} ({t.TrainerId})",
                Selected = (t.TrainerId == trainerId)
            }));

            var model = new AdminPopularRoutinesReportViewModel
            {
                RankedRoutines = rankedList,
                TotalUniqueRoutines = rankedList.Count,
                TotalValidAssignments = totalValidAssignments,
                TotalClientsServed = planSummaries.Select(p => p.ClientId).Distinct().Count(),
                TopRoutineTitle = topRoutine != null ? topRoutine.RoutineTitle : "None",
                TopRoutineAssignments = topRoutine != null ? topRoutine.TotalAssignments : 0,

                SelectedGoal = goal,
                SelectedTrainerId = trainerId,
                StatusFilter = status,
                DatePreset = activePreset,
                FromDate = fromDate,
                ToDate = toDate,
                AvailableTrainers = trainerOptions,
                AvailableGoals = new List<string> { "All Goals", "Weight Loss", "Muscle Building", "Endurance", "Strength", "General Fitness" }
            };

            return View(model);
        }

        // =====================================================================
        // 4. TRAINER ACTIVITY & ENGAGEMENT REPORT (GET: /Admin/Reports/TrainerActivity)
        // FR-A-28: Generate Trainer Activity and Engagement Reports
        // Combines FR-T-50 Workout Engagement & FR-T-51 Appointment Activity calculations
        // =====================================================================
        [HttpGet]
        public ActionResult TrainerActivity(string trainerId = null, string preset = "30days", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var model = new AdminTrainerActivityReportViewModel();

            var allTrainers = db.Trainers.OrderBy(t => t.FullName).ToList();
            model.AvailableTrainers = allTrainers.Select(t => new SelectListItem
            {
                Value = t.TrainerId,
                Text = $"{t.FullName} ({t.PrimarySpecialization ?? t.Specialization ?? "General Fitness"})",
                Selected = (t.TrainerId == trainerId)
            }).ToList();

            var targetTrainer = allTrainers.FirstOrDefault(t => t.TrainerId == trainerId) ?? allTrainers.FirstOrDefault();
            if (targetTrainer == null)
            {
                model.ValidationError = "No trainer accounts exist in the system.";
                return View(model);
            }

            model.SelectedTrainerId = targetTrainer.TrainerId;
            model.TrainerName = targetTrainer.FullName;
            model.TrainerEmail = targetTrainer.Email;
            model.Specialization = targetTrainer.PrimarySpecialization ?? targetTrainer.Specialization ?? "General Fitness";
            model.MaxClientCapacity = targetTrainer.MaxClientCapacity > 0 ? targetTrainer.MaxClientCapacity : 15;
            model.IsActiveTrainer = targetTrainer.IsActive;

            if (preset == "custom" && fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            {
                model.ValidationError = "The 'From Date' cannot be later than the 'To Date'. Please select a valid date range.";
                model.FromDate = fromDate.Value;
                model.ToDate = toDate.Value;
                model.DatePreset = "custom";
                return View(model);
            }

            var dateRange = ResolveDateRange(preset, fromDate, toDate);
            DateTime start = dateRange.Item1;
            DateTime end = dateRange.Item2;
            model.DatePreset = dateRange.Item3;
            model.FromDate = start;
            model.ToDate = end.Date;

            // 1. Client Assignment Activity: Active vs Historical
            var activeClients = db.Clients
                .Where(c => c.AssignedTrainerId == targetTrainer.TrainerId)
                .ToList();
            model.ActiveClientsCount = activeClients.Count(c => c.IsActive);

            var historicalClientIdsFromAppts = db.Appointments
                .Where(a => a.TrainerId == targetTrainer.TrainerId)
                .Select(a => a.ClientId)
                .Distinct()
                .ToList();

            var historicalClientIdsFromPlans = db.WorkoutPlans
                .Where(p => p.TrainerId == targetTrainer.TrainerId)
                .Select(p => p.ClientId)
                .Distinct()
                .ToList();

            var allClientIdsAssociated = historicalClientIdsFromAppts
                .Union(historicalClientIdsFromPlans)
                .Union(activeClients.Select(c => c.ClientId))
                .Distinct()
                .ToList();

            var activeClientIds = activeClients.Where(c => c.IsActive).Select(c => c.ClientId).ToHashSet();
            model.HistoricalClientsCount = allClientIdsAssociated.Count(id => !activeClientIds.Contains(id));

            var allAssociatedClients = db.Clients
                .Where(c => allClientIdsAssociated.Contains(c.ClientId))
                .ToList();

            foreach (var c in allAssociatedClients)
            {
                bool isActive = activeClientIds.Contains(c.ClientId);
                int clientWorkouts = db.ClientWorkoutLogs.Count(w => w.ClientId == c.ClientId && w.IsCompleted);
                int clientAppts = db.Appointments.Count(a => a.ClientId == c.ClientId && a.TrainerId == targetTrainer.TrainerId && a.Status == "Completed");

                model.ClientRoster.Add(new TrainerClientAssignmentSummaryItem
                {
                    ClientId = c.ClientId,
                    ClientName = c.FullName,
                    ClientEmail = c.Email,
                    AssignmentStatus = isActive ? "Active" : "Historical",
                    AssignmentDate = c.CreatedAt,
                    CompletedWorkoutsCount = clientWorkouts,
                    CompletedAppointmentsCount = clientAppts
                });
            }

            // 2. Workout Engagement Metrics (FR-S-40 & FR-A-28 calculations via shared EngagementCalculationHelper)
            var engagement = EngagementCalculationHelper.CalculateTrainerEngagement(db, targetTrainer.TrainerId, start, end);
            model.ScheduledWorkoutsCount = engagement.ScheduledWorkoutsCount;
            model.CompletedWorkoutsCount = engagement.CompletedWorkoutsCount;
            model.TotalCaloriesBurned = engagement.TotalCaloriesBurned;
            model.TotalExerciseMinutes = engagement.TotalExerciseMinutes;
            model.WorkoutCompletionRatePct = engagement.CompletionRatePct;

            // 3. Appointment Activity by Status (FR-T-51 calculations)
            var apptsQuery = db.Appointments
                .Include(a => a.Clients)
                .Where(a => a.TrainerId == targetTrainer.TrainerId && a.AppointmentDate >= start && a.AppointmentDate <= end)
                .OrderByDescending(a => a.AppointmentDate)
                .ToList();

            model.TotalAppointments = apptsQuery.Count;
            model.PendingAppointments = apptsQuery.Count(a => a.Status == "Pending");
            model.ApprovedAppointments = apptsQuery.Count(a => a.Status == "Approved");
            model.CompletedAppointments = apptsQuery.Count(a => a.Status == "Completed");
            model.CancelledAppointments = apptsQuery.Count(a => a.Status == "Cancelled");
            model.RejectedAppointments = apptsQuery.Count(a => a.Status == "Rejected");

            model.AppointmentDetails = apptsQuery.Select(a => new AppointmentActivityDetailItem
            {
                AppointmentId = a.AppointmentId,
                AppointmentCode = $"APT-{a.AppointmentId:D4}",
                AppointmentDate = a.AppointmentDate,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                ClientName = a.Clients != null ? a.Clients.FullName : a.ClientId,
                ClientEmail = a.Clients != null ? a.Clients.Email : "",
                Title = a.Title ?? "Training Session",
                Status = a.Status,
                MeetingLocation = a.MeetingLocation ?? "Fitness Center",
                CancellationReason = a.CancellationReason ?? a.TrainerNotes ?? a.ClientNotes
            }).ToList();

            return View(model);
        }

        // =====================================================================
        // 5. PDF PRINT / EXPORT ACTIONS (FR-A-31)
        // Export Administrator Reports as PDF
        // =====================================================================
        [HttpGet]
        public ActionResult ExportClientPerformancePdf(string clientId = null, string preset = "30days", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var result = ClientPerformance(clientId, preset, fromDate, toDate) as ViewResult;
            if (result != null)
            {
                var model = result.Model as AdminClientPerformanceReportViewModel;
                HealthAuditHelper.Log(
                    actorUserId: SessionHelper.GetCurrentAdminId(Session) ?? "Admin",
                    actorRole: "Administrator",
                    clientId: model != null ? model.SelectedClientId : clientId,
                    recordType: "ClientProgress",
                    recordId: null,
                    action: "Export",
                    wasSuccessful: true,
                    details: $"Exported client performance report to PDF ({preset})");

                result.ViewName = "ClientPerformancePrint";
                return result;
            }
            return RedirectToAction("ClientPerformance");
        }

        [HttpGet]
        public ActionResult ExportPopularRoutinesPdf(string goal = null, string trainerId = null, string preset = "all", DateTime? fromDate = null, DateTime? toDate = null, string status = "active")
        {
            var result = PopularRoutines(goal, trainerId, preset, fromDate, toDate, status) as ViewResult;
            if (result != null)
            {
                result.ViewName = "PopularRoutinesPrint";
                return result;
            }
            return RedirectToAction("PopularRoutines");
        }

        [HttpGet]
        public ActionResult ExportTrainerActivityPdf(string trainerId = null, string preset = "30days", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var result = TrainerActivity(trainerId, preset, fromDate, toDate) as ViewResult;
            if (result != null)
            {
                result.ViewName = "TrainerActivityPrint";
                return result;
            }
            return RedirectToAction("TrainerActivity");
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
