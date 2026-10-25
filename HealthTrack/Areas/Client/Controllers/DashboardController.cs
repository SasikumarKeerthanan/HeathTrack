using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Models;
using HealthTrack.Helpers;
using HealthTrack.Areas.Client.ViewModels;

namespace HealthTrack.Areas.Client.Controllers
{
    [ClientAuthorize]
    public class DashboardController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // GET: Client/Dashboard
        public ActionResult Index()
        {
            // 1. Get logged-in Client ID from Session
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            // 2. Fetch Client profile with relations
            var client = db.Clients
                .Include(c => c.Trainers)
                .Include(c => c.ClientHealthProfile)
                .Include(c => c.ClientPreferences)
                .FirstOrDefault(c => c.ClientId == clientId);

            if (client == null)
            {
                Session.Clear();
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            // Sync session variables for header and sidebar
            Session["CurrentClientId"] = client.ClientId;
            Session["CurrentClientName"] = client.FullName;
            Session["CurrentClientEmail"] = client.Email;
            Session["CurrentClientAvatar"] = client.ProfilePicturePath;
            Session["Role"] = "Client";
            ViewBag.ClientName = client.FullName;

            var model = new ClientDashboardViewModel
            {
                Client = client,
                AssignedTrainer = client?.Trainers,
                HealthProfile = client?.ClientHealthProfile?.FirstOrDefault(),
                Preferences = client?.ClientPreferences?.FirstOrDefault()
            };

            // 3. Health Statistics & Weight from ClientProgressRecords
            var progressRecords = db.ClientProgressRecords
                .Where(r => r.ClientId == clientId)
                .OrderBy(r => r.RecordedDate)
                .ToList();

            if (progressRecords.Any())
            {
                var firstRecord = progressRecords.First();
                var latestRecord = progressRecords.Last();

                model.HasWeightRecords = true;
                model.CurrentWeightKg = latestRecord.WeightKg;
                model.BaselineWeightKg = firstRecord.WeightKg;
                model.WeightChangeKg = latestRecord.WeightKg - firstRecord.WeightKg;
                model.HeightCm = latestRecord.HeightCm > 0
                    ? latestRecord.HeightCm
                    : (model.HealthProfile?.BaselineHeightCm ?? 0m);

                // Use recorded BMI or compute dynamically
                if (latestRecord.Bmi.HasValue && latestRecord.Bmi.Value > 0)
                {
                    model.Bmi = latestRecord.Bmi.Value;
                }
                else if (model.HeightCm > 0)
                {
                    decimal heightMeters = model.HeightCm / 100.0m;
                    model.Bmi = Math.Round(model.CurrentWeightKg / (heightMeters * heightMeters), 1);
                }

                if (model.Bmi > 0)
                {
                    if (model.Bmi < 18.5m) model.BmiCategory = "Underweight";
                    else if (model.Bmi <= 24.9m) model.BmiCategory = "Normal / Healthy Weight";
                    else if (model.Bmi <= 29.9m) model.BmiCategory = "Overweight";
                    else model.BmiCategory = "Obesity Range";

                    model.BmiBarPct = Math.Max(5, Math.Min(100, (int)Math.Round(((model.Bmi - 15m) / 20m) * 100)));
                }
                else
                {
                    model.BmiCategory = "Not recorded";
                    model.BmiBarPct = 0;
                }
            }
            else
            {
                model.HasWeightRecords = false;
                model.HeightCm = model.HealthProfile?.BaselineHeightCm ?? 0m;
                model.BmiCategory = "No progress records";
            }

            // Target Weight from ClientGoals table
            var weightGoal = db.ClientGoals
                .FirstOrDefault(g => g.ClientId == clientId && (g.Category == "Weight" || g.GoalName.Contains("Weight")));

            if (weightGoal != null)
            {
                model.TargetWeightKg = weightGoal.TargetValue;
                if (model.HasWeightRecords)
                {
                    decimal totalToLose = model.BaselineWeightKg - model.TargetWeightKg;
                    if (totalToLose > 0)
                    {
                        decimal lost = model.BaselineWeightKg - model.CurrentWeightKg;
                        model.WeightGoalProgressPct = (int)Math.Max(0, Math.Min(100, Math.Round((lost / totalToLose) * 100)));
                    }
                    else if (totalToLose < 0)
                    {
                        decimal gained = model.CurrentWeightKg - model.BaselineWeightKg;
                        model.WeightGoalProgressPct = (int)Math.Max(0, Math.Min(100, Math.Round((gained / Math.Abs(totalToLose)) * 100)));
                    }
                    else
                    {
                        model.WeightGoalProgressPct = 100;
                    }
                }
            }

            // 4. Goals Progress (from ClientGoals)
            model.Goals = db.ClientGoals
                .Where(g => g.ClientId == clientId)
                .OrderBy(g => g.IsAchieved)
                .ThenBy(g => g.TargetDate)
                .ToList();

            // 5. Today's Workout Summary (from WorkoutPlans & ClientWorkoutLogs)
            DateTime today = DateTime.Today;
            string todayDayName = today.DayOfWeek.ToString();

            model.ActiveWorkoutPlan = db.WorkoutPlans
                .Include(p => p.WorkoutPlanExercises.Select(e => e.Exercises))
                .FirstOrDefault(p => p.ClientId == clientId && p.IsActive);

            if (model.ActiveWorkoutPlan != null)
            {
                var exercisesForToday = model.ActiveWorkoutPlan.WorkoutPlanExercises
                    .Where(e => e.DayOfWeek.Equals(todayDayName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.OrderIndex)
                    .ToList();

                if (!exercisesForToday.Any())
                {
                    var firstDay = model.ActiveWorkoutPlan.WorkoutPlanExercises.Select(e => e.DayOfWeek).FirstOrDefault() ?? "Scheduled Routine";
                    exercisesForToday = model.ActiveWorkoutPlan.WorkoutPlanExercises
                        .Where(e => e.DayOfWeek == firstDay)
                        .OrderBy(e => e.OrderIndex)
                        .ToList();
                    model.TodayWorkoutDay = firstDay;
                }
                else
                {
                    model.TodayWorkoutDay = todayDayName;
                }

                model.TodayWorkoutExercises = exercisesForToday;
                model.EstimatedWorkoutDurationMinutes = exercisesForToday.Count * 12; // ~12 minutes per exercise block
                model.EstimatedCaloriesBurned = exercisesForToday.Sum(e => e.Exercises != null ? e.Exercises.DefaultCaloriesBurnEstimate : 60);

                // Check if workout log exists for today in database
                model.TodayWorkoutLog = db.ClientWorkoutLogs
                    .FirstOrDefault(l => l.ClientId == clientId && DbFunctions.TruncateTime(l.LogDate) == today);

                model.IsTodayWorkoutCompleted = model.TodayWorkoutLog != null && model.TodayWorkoutLog.IsCompleted;
            }

            // 6. Today's Diet Summary (from DietPlans & ClientMealLogs)
            model.ActiveDietPlan = db.DietPlans
                .Include(d => d.DietMeals)
                .FirstOrDefault(d => d.ClientId == clientId && d.IsActive);

            if (model.ActiveDietPlan != null)
            {
                model.DailyCalorieTarget = model.ActiveDietPlan.DailyCalorieTarget;
                model.TargetProteinGrams = model.ActiveDietPlan.TargetProteinGrams;
                model.TargetCarbsGrams = model.ActiveDietPlan.TargetCarbsGrams;
                model.TargetFatsGrams = model.ActiveDietPlan.TargetFatsGrams;
                model.TargetWaterLiters = model.ActiveDietPlan.WaterIntakeLiters ?? 0m;
                model.PrescribedMeals = model.ActiveDietPlan.DietMeals.OrderBy(m => m.OrderIndex).ToList();

                // Get today's completed meal logs from database
                model.TodayMealLogs = db.ClientMealLogs
                    .Where(m => m.ClientId == clientId && DbFunctions.TruncateTime(m.LogDate) == today && m.IsCompleted)
                    .ToList();

                model.CaloriesConsumedToday = model.TodayMealLogs.Sum(m => m.Calories);
                model.ProteinConsumedGrams = model.TodayMealLogs.Sum(m => m.ProteinGrams) ?? 0;
                model.CarbsConsumedGrams = model.TodayMealLogs.Sum(m => m.CarbsGrams) ?? 0;
                model.FatsConsumedGrams = model.TodayMealLogs.Sum(m => m.FatsGrams) ?? 0;
                model.WaterConsumedLiters = model.TodayMealLogs.Sum(m => m.WaterLiters ?? 0m);

                model.CaloriesRemainingToday = Math.Max(0, model.DailyCalorieTarget - model.CaloriesConsumedToday);
                if (model.DailyCalorieTarget > 0)
                {
                    model.CalorieAdherencePct = (int)Math.Min(100, Math.Round(((decimal)model.CaloriesConsumedToday / model.DailyCalorieTarget) * 100));
                }

                model.ProteinAdherencePct = model.TargetProteinGrams > 0
                    ? (int)Math.Min(100, Math.Round(((decimal)model.ProteinConsumedGrams / model.TargetProteinGrams) * 100)) : 0;
                model.CarbsAdherencePct = model.TargetCarbsGrams > 0
                    ? (int)Math.Min(100, Math.Round(((decimal)model.CarbsConsumedGrams / model.TargetCarbsGrams) * 100)) : 0;
                model.FatsAdherencePct = model.TargetFatsGrams > 0
                    ? (int)Math.Min(100, Math.Round(((decimal)model.FatsConsumedGrams / model.TargetFatsGrams) * 100)) : 0;
                model.WaterAdherencePct = model.TargetWaterLiters > 0
                    ? (int)Math.Min(100, Math.Round((model.WaterConsumedLiters / model.TargetWaterLiters) * 100)) : 0;
            }

            // 7. Recent Activities & Coach Guidance
            model.RecentWorkoutLogs = db.ClientWorkoutLogs
                .Where(w => w.ClientId == clientId && w.IsCompleted)
                .OrderByDescending(w => w.LogDate)
                .Take(3)
                .ToList();

            model.RecentCoachFeedback = db.TrainerFeedback
                .Where(f => f.ClientId == clientId && !f.IsDraft)
                .OrderByDescending(f => f.SentAt)
                .Take(2)
                .ToList();

            // 8. Upcoming Appointment & Coach Availability (FR-C-04, FR-C-15, FR-C-17, FR-C-21)
            var nowTime = DateTime.Now.TimeOfDay;
            var clientAppointments = db.Appointments
                .Include(a => a.Trainers)
                .Where(a => a.ClientId == clientId &&
                           (a.Status == "Approved" || a.Status == "Pending") &&
                           a.AppointmentDate >= today)
                .ToList();

            var upcomingList = clientAppointments
                .Where(a => a.AppointmentDate > today || a.EndTime >= nowTime)
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .ToList();

            model.UpcomingAppointmentsCount = upcomingList.Count;
            model.NextUpcomingAppointment = upcomingList.FirstOrDefault();

            if (!string.IsNullOrEmpty(client.AssignedTrainerId))
            {
                var trainerSlots = db.TrainerAvailability
                    .Where(s => s.TrainerId == client.AssignedTrainerId &&
                               s.AvailableDate >= today)
                    .ToList();

                model.AvailableCoachSlotsCount = trainerSlots
                    .Count(s => !s.IsBooked && (s.AvailableDate > today || s.StartTime > nowTime));
            }

            return View(model);
        }

        // POST: /Client/Dashboard/LogWeight
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogWeight(decimal weightKg, string notes)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            if (weightKg < 30 || weightKg > 300)
            {
                TempData["ErrorMessage"] = "Please enter a valid weight between 30 and 300 kg.";
                return RedirectToAction("Index");
            }

            try
            {
                // 1. Get client's recorded height
                var client = db.Clients.FirstOrDefault(c => c.ClientId == clientId);
                var latestProgress = db.ClientProgressRecords
                    .Where(p => p.ClientId == clientId)
                    .OrderByDescending(p => p.RecordedDate)
                    .FirstOrDefault();

                decimal heightCm = latestProgress != null && latestProgress.HeightCm > 0
                    ? latestProgress.HeightCm
                    : (client?.HeightCm ?? (client?.ClientHealthProfile?.FirstOrDefault()?.BaselineHeightCm ?? 0m));

                // 2. Insert new Progress Record
                var newRecord = new ClientProgressRecords
                {
                    ClientId = clientId,
                    RecordedDate = DateTime.Today,
                    WeightKg = Math.Round(weightKg, 2),
                    HeightCm = heightCm,
                    CaloriesConsumed = 0,
                    CaloriesBurned = 0,
                    WaterIntakeMl = 0,
                    Notes = string.IsNullOrWhiteSpace(notes) ? "Weight check-in" : notes.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                db.ClientProgressRecords.Add(newRecord);

                // 3. Update Current Value in ClientGoals (Target Weight Goal)
                var weightGoal = db.ClientGoals
                    .FirstOrDefault(g => g.ClientId == clientId && (g.Category == "Weight" || g.GoalName.Contains("Weight")));

                if (weightGoal != null)
                {
                    weightGoal.CurrentValue = Math.Round(weightKg, 2);
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
                else
                {
                    // If no goal exists yet, create one using current weight
                    db.ClientGoals.Add(new ClientGoals
                    {
                        ClientId = clientId,
                        GoalName = "Target Weight",
                        Category = "Weight",
                        BaselineValue = Math.Round(weightKg, 2),
                        CurrentValue = Math.Round(weightKg, 2),
                        TargetValue = Math.Round(weightKg, 2),
                        Unit = "kg",
                        StatusMessage = "In Progress",
                        IsAchieved = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                db.SaveChanges();
                TempData["SuccessMessage"] = $"Your weight of {weightKg:F1} kg has been logged successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to log weight: " + ex.Message;
            }

            return RedirectToAction("Index");
        }
    }
}
