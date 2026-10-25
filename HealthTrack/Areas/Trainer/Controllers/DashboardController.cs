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
    /// Trainer Dashboard Controller.
    [TrainerAuthorize]
    public class DashboardController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // GET: Trainer/Dashboard
        public ActionResult Index()
        {
            // 1. Get logged-in Trainer ID from Session
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            // 2. Fetch Trainer profile from database
            var trainer = db.Trainers.FirstOrDefault(t => t.TrainerId == trainerId);
            if (trainer == null)
            {
                SessionHelper.ClearSession(Session);
                TempData["ErrorMessage"] = "Trainer account not found. Please log in again.";
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            ViewBag.TrainerName = trainer.FullName;

            var model = new TrainerDashboardViewModel
            {
                Trainer = trainer
            };

            // 3. Assigned Clients from Database (Currently Active only)
            model.AssignedClients = db.Clients
                .Include(c => c.ClientHealthProfile)
                .Include(c => c.ClientPreferences)
                .Where(c => c.AssignedTrainerId == trainerId && c.IsActive)
                .OrderBy(c => c.FullName)
                .ToList();

            model.AssignedClientCount = model.AssignedClients.Count;

            // 4. Plan Counts & Exercises from Database
            model.ActiveWorkoutPlansCount = db.WorkoutPlans.Count(p => p.TrainerId == trainerId && p.IsActive);
            model.ActiveDietPlansCount = db.DietPlans.Count(p => p.TrainerId == trainerId && p.IsActive);
            model.TotalExercisesCount = db.Exercises.Count();

            // Computed Dynamic Database Metrics
            var activeWorkoutPlans = db.WorkoutPlans
                .Where(p => p.TrainerId == trainerId && p.IsActive)
                .ToList();

            if (activeWorkoutPlans.Any())
            {
                double avgDays = activeWorkoutPlans.Average(p => p.DaysPerWeek);
                model.CommonWorkoutSplit = $"{Math.Round(avgDays, 0)}-Day Routine Avg";
            }
            else
            {
                model.CommonWorkoutSplit = "No active routines";
            }

            var activeDietPlans = db.DietPlans
                .Where(p => p.TrainerId == trainerId && p.IsActive)
                .ToList();

            if (activeDietPlans.Any())
            {
                model.AverageDietPlanCalories = (int)Math.Round(activeDietPlans.Average(p => p.DailyCalorieTarget));
            }
            else
            {
                model.AverageDietPlanCalories = 0;
            }

            model.StrengthExercisesCount = db.Exercises.Count(e => e.Category == "Strength");
            model.CardioExercisesCount = db.Exercises.Count(e => e.Category == "Cardio");
            model.CoreExercisesCount = db.Exercises.Count(e => e.Category == "Core");

            // 5. Recent Client Activity Feed
            var clientIds = model.AssignedClients.Select(c => c.ClientId).ToList();

            // Recent workout logs from assigned clients
            model.RecentWorkoutLogs = db.ClientWorkoutLogs
                .Include(w => w.Clients)
                .Where(w => clientIds.Contains(w.ClientId) && w.IsCompleted)
                .OrderByDescending(w => w.LogDate)
                .Take(4)
                .ToList();

            // Recent meal logs from assigned clients
            model.RecentMealLogs = db.ClientMealLogs
                .Where(m => clientIds.Contains(m.ClientId) && m.IsCompleted && m.MealType != "Hydration")
                .OrderByDescending(m => m.LogDate)
                .Take(3)
                .ToList();

            // Recent feedback sent by this coach
            model.RecentFeedbackHistory = db.TrainerFeedback
                .Include(f => f.Clients)
                .Where(f => f.TrainerId == trainerId)
                .OrderByDescending(f => f.SentAt)
                .Take(3)
                .ToList();

            // 6. Appointments Metrics (FR-T-03)
            var today = DateTime.Today;
            model.TodayAppointmentsCount = db.Appointments.Count(a => a.TrainerId == trainerId && a.AppointmentDate == today && a.Status == "Approved");
            model.PendingAppointmentsCount = db.Appointments.Count(a => a.TrainerId == trainerId && a.Status == "Pending");
            model.UpcomingAppointments = db.Appointments
                .Include(a => a.Clients)
                .Where(a => a.TrainerId == trainerId && (a.Status == "Approved" || a.Status == "Pending") && a.AppointmentDate >= today)
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .Take(3)
                .ToList();

            return View(model);
        }
    }
}
