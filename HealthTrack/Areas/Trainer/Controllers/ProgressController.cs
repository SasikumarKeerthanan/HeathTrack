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
    [TrainerAuthorize]
    public class ProgressController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        private static Tuple<DateTime, DateTime, string> ResolveDateRange(string preset, DateTime? fromDate, DateTime? toDate)
        {
            var end = toDate.HasValue ? toDate.Value : DateTime.Today;
            var activePreset = string.IsNullOrEmpty(preset) ? "30days" : preset;

            DateTime start;
            switch (activePreset)
            {
                case "7days":
                    start = end.AddDays(-7);
                    break;
                case "30days":
                    start = end.AddDays(-30);
                    break;
                case "90days":
                    start = end.AddDays(-90);
                    break;
                case "all":
                    start = new DateTime(2020, 1, 1);
                    break;
                default:
                    start = fromDate.HasValue ? fromDate.Value : end.AddDays(-30);
                    break;
            }

            return Tuple.Create(start, end, activePreset);
        }

        private string GetCurrentTrainerId()
        {
            return SessionHelper.GetCurrentTrainerId(Session);
        }

        private List<ClientSummary> GetAssignedClients(HealthTrackEntities context)
        {
            string trainerId = GetCurrentTrainerId();
            var clients = context.Clients
                .Where(c => c.AssignedTrainerId == trainerId && c.IsActive)
                .Include(c => c.ClientPreferences)
                .OrderBy(c => c.FullName)
                .ToList();

            return clients.Select(c =>
            {
                var pref = c.ClientPreferences.FirstOrDefault();
                return new ClientSummary
                {
                    Id = c.ClientId,
                    FullName = c.FullName,
                    Email = c.Email,
                    PrimaryGoal = pref != null && !string.IsNullOrWhiteSpace(pref.PrimaryGoal) ? pref.PrimaryGoal : "General Fitness"
                };
            }).ToList();
        }

        private ProgressAnalyticsViewModel BuildAnalyticsViewModel(string clientId, string preset, DateTime? fromDate, DateTime? toDate)
        {
            string trainerId = GetCurrentTrainerId();
            if (!string.IsNullOrWhiteSpace(clientId) && !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, clientId))
            {
                return null;
            }

            var clients = GetAssignedClients(db);
            var selectedClient = !string.IsNullOrWhiteSpace(clientId)
                ? clients.FirstOrDefault(c => c.Id == clientId)
                : clients.FirstOrDefault();
            string targetClientId = selectedClient != null ? selectedClient.Id : (clients.Any() ? clients.First().Id : string.Empty);

            var dateRange = ResolveDateRange(preset, fromDate, toDate);
            DateTime start = dateRange.Item1;
            DateTime end = dateRange.Item2;
            string activePreset = dateRange.Item3;

            var progressEntities = db.ClientProgressRecords
                .Where(r => r.ClientId == targetClientId && r.RecordedDate >= start && r.RecordedDate <= end)
                .OrderBy(r => r.RecordedDate)
                .ToList();

            if (!progressEntities.Any())
            {
                progressEntities = db.ClientProgressRecords
                    .Where(r => r.ClientId == targetClientId)
                    .OrderBy(r => r.RecordedDate)
                    .ToList();
            }

            var workoutLogs = db.ClientWorkoutLogs
                .Include(wl => wl.WorkoutPlans)
                .Where(wl => wl.ClientId == targetClientId)
                .OrderByDescending(wl => wl.LogDate)
                .ToList();

            var goalEntities = db.ClientGoals
                .Where(g => g.ClientId == targetClientId)
                .ToList();

            var goals = goalEntities.Select(g => new ClientGoalMetric
            {
                GoalName = g.GoalName,
                Category = g.Category,
                BaselineValue = (double)g.BaselineValue,
                CurrentValue = (double)g.CurrentValue,
                TargetValue = (double)g.TargetValue,
                Unit = g.Unit,
                StatusMessage = g.StatusMessage
            }).ToList();

            foreach (var g in goals)
            {
                double range = Math.Abs(g.TargetValue - g.BaselineValue);
                if (range > 0)
                {
                    double progress = Math.Abs(g.CurrentValue - g.BaselineValue);
                    g.ProgressPercentage = Math.Min(100, Math.Max(0, (int)Math.Round(progress / range * 100)));
                }
                else
                {
                    g.ProgressPercentage = 100;
                }
            }

            var firstRecord = progressEntities.FirstOrDefault();
            var latestRecord = progressEntities.LastOrDefault();

            double currentWeight = latestRecord != null ? (double)latestRecord.WeightKg : 0.0;
            double baselineWeight = firstRecord != null ? (double)firstRecord.WeightKg : currentWeight;
            var weightGoal = goals.FirstOrDefault(g => g.Category == "Weight");
            double targetWeight = weightGoal != null ? weightGoal.TargetValue : currentWeight;
            double currentBmi = latestRecord != null && latestRecord.Bmi.HasValue
                ? (double)latestRecord.Bmi.Value
                : 0.0;

            // FR-S-40: Calculate workout engagement metrics using shared EngagementCalculationHelper
            var engagement = EngagementCalculationHelper.CalculateClientEngagement(db, targetClientId, start, end);
            int scheduledWorkouts = engagement.ScheduledWorkoutsCount;
            int completedWorkouts = engagement.CompletedWorkoutsCount;
            int totalBurned = (int)Math.Min(int.MaxValue, engagement.TotalCaloriesBurned);
            int totalMinutes = engagement.TotalExerciseMinutes;

            var activeDiet = db.DietPlans.FirstOrDefault(dp => dp.ClientId == targetClientId && dp.IsActive);
            int dailyCalorieTarget = activeDiet != null ? activeDiet.DailyCalorieTarget : 0;
            int avgCaloriesConsumed = progressEntities.Any(r => r.CaloriesConsumed > 0)
                ? (int)Math.Round(progressEntities.Where(r => r.CaloriesConsumed > 0).Average(r => r.CaloriesConsumed))
                : 0;

            return new ProgressAnalyticsViewModel
            {
                SelectedClientId = targetClientId,
                SelectedClient = selectedClient,
                AvailableClients = clients,
                FromDate = start,
                ToDate = end,
                DatePreset = activePreset,

                CurrentWeight = currentWeight,
                BaselineWeight = baselineWeight,
                TargetWeight = targetWeight,
                CurrentBmi = currentBmi,

                TotalWorkoutsScheduled = scheduledWorkouts,
                TotalWorkoutsCompleted = completedWorkouts,
                TotalCaloriesBurned = totalBurned,
                AvgDailyCaloriesConsumed = avgCaloriesConsumed,
                DailyCalorieTarget = dailyCalorieTarget,
                TotalActiveMinutes = totalMinutes,

                Goals = goals,
                RecentLogs = workoutLogs.Take(5).ToList(),

                ChartLabels = progressEntities.Select(r => r.RecordedDate.ToString("MMM dd")).ToList(),
                WeightHistory = progressEntities.Select(r => (double)r.WeightKg).ToList(),
                BmiHistory = progressEntities.Select(r => (double)(r.Bmi ?? 0m)).ToList(),
                CaloriesConsumedHistory = progressEntities.Select(r => r.CaloriesConsumed).ToList(),
                CaloriesBurnedHistory = progressEntities.Select(r => r.CaloriesBurned).ToList(),
                ActivityMinutesHistory = progressEntities.Select(r => workoutLogs.Where(w => w.LogDate.Date == r.RecordedDate.Date).Select(w => (int?)w.DurationMinutes).Sum() ?? 0).ToList()
            };
        }

        private ClientReportViewModel BuildClientReportViewModel(string clientId, string preset, DateTime? fromDate, DateTime? toDate)
        {
            var analytics = BuildAnalyticsViewModel(clientId, preset, fromDate, toDate);
            if (analytics == null)
            {
                return null;
            }

            var client = analytics.SelectedClient;

            var progressEntities = db.ClientProgressRecords
                .Where(r => r.ClientId == analytics.SelectedClientId)
                .OrderByDescending(r => r.RecordedDate)
                .ToList();
            HealthDataProtectionHelper.DecryptProgressRecords(progressEntities);

            var workoutLogs = db.ClientWorkoutLogs
                .Include(wl => wl.WorkoutPlans)
                .Where(wl => wl.ClientId == analytics.SelectedClientId)
                .OrderByDescending(wl => wl.LogDate)
                .ToList();

            var firstRec = progressEntities.LastOrDefault();
            var latestRec = progressEntities.FirstOrDefault();

            var activeWorkout = db.WorkoutPlans.FirstOrDefault(p => p.ClientId == analytics.SelectedClientId && p.IsActive);
            var activeDiet = db.DietPlans.Include(dp => dp.Trainers).FirstOrDefault(p => p.ClientId == analytics.SelectedClientId && p.IsActive);
            var clientEntity = db.Clients.Include(c => c.Trainers).FirstOrDefault(c => c.ClientId == analytics.SelectedClientId);

            string coachName = clientEntity != null && clientEntity.Trainers != null 
                ? clientEntity.Trainers.FullName 
                : (activeDiet != null && activeDiet.Trainers != null 
                    ? activeDiet.Trainers.FullName 
                    : "Not Assigned");

            double prescribedWater = activeDiet != null && activeDiet.WaterIntakeLiters.HasValue ? (double)activeDiet.WaterIntakeLiters.Value : 0.0;
            double avgWater = prescribedWater > 0 ? prescribedWater : 0.0;

            string evalNotes = activeWorkout != null && !string.IsNullOrWhiteSpace(activeWorkout.TrainerNotes)
                ? activeWorkout.TrainerNotes
                : (activeDiet != null && !string.IsNullOrWhiteSpace(activeDiet.SpecialInstructions)
                    ? activeDiet.SpecialInstructions
                    : string.Empty);

            string nextRecs = activeWorkout != null && !string.IsNullOrWhiteSpace(activeWorkout.RecoveryGuidelines)
                ? activeWorkout.RecoveryGuidelines
                : (activeWorkout != null && !string.IsNullOrWhiteSpace(activeWorkout.ProgressionStrategy)
                    ? activeWorkout.ProgressionStrategy
                    : string.Empty);

            return new ClientReportViewModel
            {
                ClientId = analytics.SelectedClientId,
                ClientName = client != null ? client.FullName : "Selected Client",
                ClientEmail = client != null ? client.Email : "",
                PrimaryGoal = client != null ? client.PrimaryGoal : "General Fitness",
                CoachName = coachName,

                FromDate = analytics.FromDate,
                ToDate = analytics.ToDate,
                DatePreset = analytics.DatePreset,
                AvailableClients = analytics.AvailableClients,

                BaselineWeightKg = analytics.BaselineWeight,
                CurrentWeightKg = analytics.CurrentWeight,
                TargetWeightKg = analytics.TargetWeight,
                HeightCm = latestRec != null ? (double)latestRec.HeightCm : (client != null ? (double)client.HeightCm : 0.0),
                BaselineBmi = firstRec != null && firstRec.Bmi.HasValue ? (double)firstRec.Bmi.Value : analytics.CurrentBmi,
                CurrentBmi = analytics.CurrentBmi,
                BaselineBodyFatPct = firstRec != null && firstRec.BodyFatPercentage.HasValue ? (double?)firstRec.BodyFatPercentage.Value : null,
                CurrentBodyFatPct = latestRec != null && latestRec.BodyFatPercentage.HasValue ? (double?)latestRec.BodyFatPercentage.Value : null,

                TotalWorkoutsScheduled = analytics.TotalWorkoutsScheduled,
                TotalWorkoutsCompleted = analytics.TotalWorkoutsCompleted,
                TotalExerciseMinutes = analytics.TotalActiveMinutes,
                TotalCaloriesBurned = analytics.TotalCaloriesBurned,

                PrescribedDailyCalories = analytics.DailyCalorieTarget,
                AverageConsumedCalories = analytics.AvgDailyCaloriesConsumed,
                PrescribedWaterLiters = prescribedWater,
                AverageWaterLiters = avgWater,

                GoalMilestones = analytics.Goals,
                SessionLogs = workoutLogs,
                MetricRecords = progressEntities,

                CoachEvaluationNotes = evalNotes,
                NextPhaseRecommendations = nextRecs
            };
        }

        // 1. GET: /Progress
        // Interactive Analytics Dashboard with Charts for weight, BMI, calories, completion, activity
        [HttpGet]
        public ActionResult Index(string clientId, string preset = "30days", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var viewModel = BuildAnalyticsViewModel(clientId, preset, fromDate, toDate);
            if (viewModel == null)
            {
                HealthAuditHelper.Log(
                    actorUserId: GetCurrentTrainerId() ?? "Anonymous",
                    actorRole: "Trainer",
                    clientId: clientId,
                    recordType: "ClientProgress",
                    recordId: null,
                    action: "View",
                    wasSuccessful: false,
                    details: "Blocked unauthorized attempt to view client progress analytics");

                TempData["ErrorMessage"] = "Access restricted: The requested client is not currently assigned to you.";
                return RedirectToAction("Index", new { clientId = (string)null });
            }

            HealthAuditHelper.Log(
                actorUserId: GetCurrentTrainerId(),
                actorRole: "Trainer",
                clientId: viewModel.SelectedClientId,
                recordType: "ClientProgress",
                recordId: null,
                action: "View",
                wasSuccessful: true,
                details: $"Viewed client progress analytics dashboard ({preset})");

            return View(viewModel);
        }

        // 2. GET: /Progress/Reports
        // Comprehensive Client Progress, Workout & Health Report with Preview & Date Filters
        [HttpGet]
        public ActionResult Reports(string clientId, string preset = "30days", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var report = BuildClientReportViewModel(clientId, preset, fromDate, toDate);
            if (report == null)
            {
                HealthAuditHelper.Log(
                    actorUserId: GetCurrentTrainerId() ?? "Anonymous",
                    actorRole: "Trainer",
                    clientId: clientId,
                    recordType: "ClientProgress",
                    recordId: null,
                    action: "View",
                    wasSuccessful: false,
                    details: "Blocked unauthorized attempt to view client progress report");

                TempData["ErrorMessage"] = "Access restricted: The requested client is not currently assigned to you.";
                return RedirectToAction("Reports", new { clientId = (string)null });
            }

            HealthAuditHelper.Log(
                actorUserId: GetCurrentTrainerId(),
                actorRole: "Trainer",
                clientId: report.ClientId,
                recordType: "ClientProgress",
                recordId: null,
                action: "View",
                wasSuccessful: true,
                details: $"Viewed client comprehensive health and progress report ({preset})");

            return View(report);
        }

        // 3. GET: /Progress/ExportPdf
        // Print-optimized clean view tailored for direct browser PDF export
        [HttpGet]
        public ActionResult ExportPdf(string clientId, string preset = "30days", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var report = BuildClientReportViewModel(clientId, preset, fromDate, toDate);
            if (report == null)
            {
                HealthAuditHelper.Log(
                    actorUserId: GetCurrentTrainerId() ?? "Anonymous",
                    actorRole: "Trainer",
                    clientId: clientId,
                    recordType: "ClientProgress",
                    recordId: null,
                    action: "Export",
                    wasSuccessful: false,
                    details: "Blocked unauthorized attempt to export client progress report to PDF");

                return new HttpStatusCodeResult(403, "Access restricted: The requested client is not currently assigned to you.");
            }

            HealthAuditHelper.Log(
                actorUserId: GetCurrentTrainerId(),
                actorRole: "Trainer",
                clientId: report.ClientId,
                recordType: "ClientProgress",
                recordId: null,
                action: "Export",
                wasSuccessful: true,
                details: $"Exported client progress report to PDF ({preset})");

            return View("ReportPrint", report);
        }

        [HttpGet]
        public ActionResult ReportPrint(string clientId, string preset = "30days", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var report = BuildClientReportViewModel(clientId, preset, fromDate, toDate);
            if (report == null)
            {
                HealthAuditHelper.Log(
                    actorUserId: GetCurrentTrainerId() ?? "Anonymous",
                    actorRole: "Trainer",
                    clientId: clientId,
                    recordType: "ClientProgress",
                    recordId: null,
                    action: "View",
                    wasSuccessful: false,
                    details: "Blocked unauthorized attempt to view printable client progress report");

                return new HttpStatusCodeResult(403, "Access restricted: The requested client is not currently assigned to you.");
            }

            HealthAuditHelper.Log(
                actorUserId: GetCurrentTrainerId(),
                actorRole: "Trainer",
                clientId: report.ClientId,
                recordType: "ClientProgress",
                recordId: null,
                action: "View",
                wasSuccessful: true,
                details: $"Viewed printable client progress report ({preset})");

            return View(report);
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
