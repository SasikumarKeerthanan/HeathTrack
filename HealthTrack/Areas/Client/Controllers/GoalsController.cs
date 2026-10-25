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
    public class GoalsController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // GET: /Client/Goals
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

            // 1. Fetch Goals
            var allGoals = db.ClientGoals
                .Where(g => g.ClientId == clientId)
                .OrderByDescending(g => g.CreatedAt)
                .ToList();

            var activeGoals = allGoals
                .Where(g => (g.Status == "Active" || string.IsNullOrEmpty(g.Status)) && !g.IsAchieved && g.Status != "Cancelled")
                .ToList();

            var completedGoals = allGoals
                .Where(g => (g.Status == "Completed" || g.IsAchieved) && g.Status != "Cancelled")
                .ToList();

            var cancelledGoals = allGoals
                .Where(g => g.Status == "Cancelled")
                .ToList();

            var model = new ClientGoalsIndexViewModel
            {
                ClientName = client.FullName,
                ActiveGoals = activeGoals,
                CompletedGoals = completedGoals,
                CancelledGoals = cancelledGoals,
                CancelledGoalsCount = cancelledGoals.Count
            };

            // 2. Build Goal Progress Chart Data (Active goals comparison)
            foreach (var goal in activeGoals)
            {
                model.GoalChartData.Labels.Add(goal.GoalName);
                model.GoalChartData.CurrentValues.Add(goal.CurrentValue);
                model.GoalChartData.TargetValues.Add(goal.TargetValue);

                int pct = 0;
                if (goal.IsAchieved)
                {
                    pct = 100;
                }
                else if (goal.BaselineValue != goal.TargetValue)
                {
                    var totalDist = Math.Abs(goal.TargetValue - goal.BaselineValue);
                    var progressDist = Math.Abs(goal.CurrentValue - goal.BaselineValue);
                    pct = totalDist > 0 ? (int)Math.Max(0, Math.Min(100, Math.Round((progressDist / totalDist) * 100))) : 0;
                }
                model.GoalChartData.ProgressPercentages.Add(pct);
            }

            // 3. Build Workout Completion Chart Data (Past 14 Days)
            var fourteenDaysAgo = DateTime.Today.AddDays(-13);
            var workoutLogs = db.ClientWorkoutLogs
                .Where(w => w.ClientId == clientId && w.LogDate >= fourteenDaysAgo)
                .ToList();

            for (int i = 0; i < 14; i++)
            {
                var day = fourteenDaysAgo.AddDays(i);
                var logsOnDay = workoutLogs.Where(w => w.LogDate.Date == day.Date).ToList();

                model.WorkoutChartData.Dates.Add(day.ToString("MMM dd"));
                model.WorkoutChartData.CompletedCount.Add(logsOnDay.Count(w => w.IsCompleted));
                model.WorkoutChartData.DurationMinutes.Add(logsOnDay.Sum(w => w.DurationMinutes));
                model.WorkoutChartData.CaloriesBurned.Add(logsOnDay.Sum(w => w.CaloriesBurned));
            }

            // FR-S-20: Audit health goals viewing
            HealthAuditHelper.LogAccess(Session, clientId, "HealthGoal", null, "View", true, "Viewed client health goals hub");

            return View(model);
        }

        // POST: /Client/Goals/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateGoalInputModel input)
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
                    : "Please fill in all required fields properly.";
                return RedirectToAction("Index");
            }

            var newGoal = new ClientGoals
            {
                ClientId = clientId,
                GoalName = input.GoalName.Trim(),
                Category = input.Category.Trim(),
                BaselineValue = input.BaselineValue,
                CurrentValue = input.CurrentValue,
                TargetValue = input.TargetValue,
                Unit = input.Unit.Trim(),
                StatusMessage = string.IsNullOrWhiteSpace(input.StatusMessage) ? "In Progress" : input.StatusMessage.Trim(),
                IsAchieved = false,
                Status = "Active",
                CancelledAt = null,
                TargetDate = input.TargetDate,
                CreatedAt = DateTime.Now
            };

            db.ClientGoals.Add(newGoal);
            db.SaveChanges();

            // FR-S-20: Audit health goal creation
            HealthAuditHelper.LogAccess(Session, clientId, "HealthGoal", newGoal.GoalId.ToString(), "Create", true, $"Created health goal '{newGoal.Category}'");

            TempData["SuccessMessage"] = $"New goal '{newGoal.GoalName}' created successfully!";
            return RedirectToAction("Index");
        }

        // POST: /Client/Goals/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(EditGoalInputModel input)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            var goal = db.ClientGoals.FirstOrDefault(g => g.GoalId == input.GoalId && g.ClientId == clientId);
            if (goal == null)
            {
                TempData["ErrorMessage"] = "Goal not found.";
                return RedirectToAction("Index");
            }

            if (goal.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = "Cannot edit a cancelled health goal.";
                return RedirectToAction("Index");
            }

            goal.GoalName = input.GoalName.Trim();
            goal.Category = input.Category.Trim();
            goal.CurrentValue = input.CurrentValue;
            goal.TargetValue = input.TargetValue;
            goal.Unit = input.Unit.Trim();
            goal.TargetDate = input.TargetDate;
            if (!string.IsNullOrWhiteSpace(input.StatusMessage))
            {
                goal.StatusMessage = input.StatusMessage.Trim();
            }

            // Check if achieved automatically
            if (goal.CurrentValue == goal.TargetValue)
            {
                goal.IsAchieved = true;
                goal.Status = "Completed";
                goal.StatusMessage = "Achieved";
            }

            db.SaveChanges();

            // FR-S-20: Audit health goal update
            HealthAuditHelper.LogAccess(Session, clientId, "HealthGoal", goal.GoalId.ToString(), "Update", true, $"Updated health goal '{goal.Category}'");

            TempData["SuccessMessage"] = $"Goal '{goal.GoalName}' updated successfully!";
            return RedirectToAction("Index");
        }

        // POST: /Client/Goals/MarkComplete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkComplete(int goalId)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            var goal = db.ClientGoals.FirstOrDefault(g => g.GoalId == goalId && g.ClientId == clientId);
            if (goal == null)
            {
                TempData["ErrorMessage"] = "Goal not found.";
                return RedirectToAction("Index");
            }

            if (goal.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = "Cannot complete a goal that has already been cancelled.";
                return RedirectToAction("Index");
            }

            goal.IsAchieved = true;
            goal.Status = "Completed";
            goal.StatusMessage = "Achieved & Completed";
            goal.CurrentValue = goal.TargetValue; // milestone reached

            db.SaveChanges();

            // FR-S-20: Audit goal completion
            HealthAuditHelper.LogAccess(Session, clientId, "HealthGoal", goal.GoalId.ToString(), "Update", true, $"Completed health goal '{goal.Category}'");

            TempData["SuccessMessage"] = $"Congratulations! Goal '{goal.GoalName}' marked as completed!";
            return RedirectToAction("Index");
        }

        // POST: /Client/Goals/Cancel
        // FR-C-38: Cancel an active health goal while preserving historical records
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int goalId)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            var goal = db.ClientGoals.FirstOrDefault(g => g.GoalId == goalId && g.ClientId == clientId);
            if (goal == null)
            {
                TempData["ErrorMessage"] = "Goal not found.";
                return RedirectToAction("Index");
            }

            if (goal.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = $"Goal '{goal.GoalName}' is already cancelled.";
                return RedirectToAction("Index");
            }

            if (goal.Status == "Completed" || goal.IsAchieved)
            {
                TempData["ErrorMessage"] = $"Goal '{goal.GoalName}' is already marked as completed and cannot be cancelled.";
                return RedirectToAction("Index");
            }

            // Update status and record cancellation timestamp while preserving all original details
            goal.Status = "Cancelled";
            goal.CancelledAt = DateTime.Now;
            if (string.IsNullOrWhiteSpace(goal.StatusMessage) || goal.StatusMessage == "In Progress")
            {
                goal.StatusMessage = "Cancelled by client";
            }

            db.SaveChanges();

            // FR-S-20: Audit goal cancellation
            HealthAuditHelper.LogAccess(Session, clientId, "HealthGoal", goal.GoalId.ToString(), "Update", true, $"Cancelled health goal '{goal.Category}'");

            TempData["SuccessMessage"] = $"Health goal '{goal.GoalName}' has been cancelled. Its historical record has been preserved in your Goal History.";
            return RedirectToAction("Index");
        }

        // POST: /Client/Goals/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int goalId)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            var goal = db.ClientGoals.FirstOrDefault(g => g.GoalId == goalId && g.ClientId == clientId);
            if (goal == null)
            {
                TempData["ErrorMessage"] = "Goal not found.";
                return RedirectToAction("Index");
            }

            db.ClientGoals.Remove(goal);
            db.SaveChanges();

            // FR-S-20: Audit goal deletion
            HealthAuditHelper.LogAccess(Session, clientId, "HealthGoal", goalId.ToString(), "Delete", true, $"Deleted health goal {goalId}");

            TempData["SuccessMessage"] = "Goal removed successfully.";
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
