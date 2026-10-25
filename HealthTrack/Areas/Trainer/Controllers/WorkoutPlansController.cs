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
    public class WorkoutPlansController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        private static bool IsAllOrEmpty(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            var trimmed = value.Trim();
            return trimmed.Equals("all", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("all ", StringComparison.OrdinalIgnoreCase);
        }

        private static List<ClientSummary> GetAssignedClients(HealthTrackEntities context, string trainerId)
        {
            var clients = context.Clients
                .Include(c => c.ClientHealthProfile)
                .Include(c => c.ClientPreferences)
                .Include(c => c.ClientProgressRecords)
                .Include(c => c.ClientGoals)
                .Where(c => c.AssignedTrainerId == trainerId && c.IsActive)
                .OrderBy(c => c.FullName)
                .ToList();

            return clients.Select(c =>
            {
                var healthProfile = c.ClientHealthProfile.FirstOrDefault();
                HealthDataProtectionHelper.DecryptHealthProfile(healthProfile);
                var preference = c.ClientPreferences.FirstOrDefault();
                var latestRecord = c.ClientProgressRecords.OrderByDescending(r => r.RecordedDate).FirstOrDefault();
                var firstRecord = c.ClientProgressRecords.OrderBy(r => r.RecordedDate).FirstOrDefault();
                var weightGoal = c.ClientGoals.FirstOrDefault(g => g.Category == "Weight" || g.GoalName.Contains("Weight"));

                decimal? currentWeight = latestRecord != null ? (decimal?)latestRecord.WeightKg : null;
                decimal? baselineWeight = firstRecord != null ? (decimal?)firstRecord.WeightKg : null;
                decimal? targetWeight = weightGoal != null ? (decimal?)weightGoal.TargetValue : null;

                decimal? bmi = latestRecord != null ? latestRecord.Bmi : null;

                string bmiCategory = "Normal";
                if (bmi.HasValue)
                {
                    if (bmi < 18.5m) bmiCategory = "Underweight";
                    else if (bmi < 25.0m) bmiCategory = "Normal";
                    else if (bmi < 30.0m) bmiCategory = "Overweight";
                    else bmiCategory = "Obese";
                }

                return new ClientSummary
                {
                    Id = c.ClientId,
                    FullName = c.FullName,
                    Email = c.Email,
                    PrimaryGoal = preference != null && !string.IsNullOrWhiteSpace(preference.PrimaryGoal) ? preference.PrimaryGoal : "General Fitness",
                    Gender = c.Gender,
                    HeightCm = healthProfile != null ? healthProfile.BaselineHeightCm : 0,
                    Allergies = healthProfile != null && !string.IsNullOrWhiteSpace(healthProfile.Allergies) ? healthProfile.Allergies : "None",
                    DietaryRestrictions = healthProfile != null && !string.IsNullOrWhiteSpace(healthProfile.DietaryRestrictions) ? healthProfile.DietaryRestrictions : "Balanced",
                    MedicalConditions = healthProfile != null && !string.IsNullOrWhiteSpace(healthProfile.MedicalConditions) ? healthProfile.MedicalConditions : "None",
                    CurrentWeightKg = currentWeight,
                    BaselineWeightKg = baselineWeight,
                    TargetWeightKg = targetWeight,
                    CurrentBmi = bmi,
                    BmiCategory = bmiCategory
                };
            }).ToList();
        }

        private static List<WorkoutPlans> GetFilteredPlans(HealthTrackEntities context, string trainerId, string search = null, string goal = null, string status = null, string clientId = null)
        {
            var query = context.WorkoutPlans
                .Include(p => p.Clients)
                .Include(p => p.Trainers)
                .Include(p => p.WorkoutPlanExercises.Select(wpe => wpe.Exercises))
                .Where(p => p.TrainerId == trainerId && p.Clients.AssignedTrainerId == trainerId && p.Clients.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p =>
                    p.Title.Contains(term)
                    || p.Description.Contains(term)
                    || p.Goal.Contains(term)
                    || p.DifficultyLevel.Contains(term)
                    || (p.TrainerNotes != null && p.TrainerNotes.Contains(term))
                    || p.Clients.FullName.Contains(term)
                    || p.Clients.Email.Contains(term)
                    || p.Trainers.FullName.Contains(term)
                    || p.WorkoutPlanExercises.Any(wpe =>
                        wpe.Exercises.Name.Contains(term)
                        || (wpe.Instructions != null && wpe.Instructions.Contains(term)))
                );
            }

            if (!IsAllOrEmpty(goal))
            {
                var g = goal.Trim();
                query = query.Where(p => p.Goal == g);
            }

            if (!IsAllOrEmpty(status))
            {
                if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(p => p.IsActive);
                }
                else if (status.Equals("deactivated", StringComparison.OrdinalIgnoreCase)
                      || status.Equals("inactive", StringComparison.OrdinalIgnoreCase)
                      || status.Equals("archived", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(p => !p.IsActive);
                }
            }

            if (!IsAllOrEmpty(clientId))
            {
                var cId = clientId.Trim();
                query = query.Where(p => p.ClientId == cId);
            }

            return query.OrderByDescending(p => p.CreatedAt).ToList();
        }

        // 1. GET: /WorkoutPlans
        // Lists assigned clients and their personalized workout plans
        [HttpGet]
        public ActionResult Index(string search, string goal, string status = "all", string clientId = null)
        {
            string trainerId = GetCurrentTrainerId();
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var allPlans = GetFilteredPlans(db, trainerId);
            var plans = GetFilteredPlans(db, trainerId, search, goal, status, clientId);
            var clients = GetAssignedClients(db, trainerId);

            var viewModel = new WorkoutPlanIndexViewModel
            {
                Plans = plans,
                AllPlans = allPlans,
                AssignedClients = clients,
                SearchTerm = search,
                SelectedGoal = goal,
                SelectedStatus = status,
                SelectedClientId = clientId
            };

            return View(viewModel);
        }

        // 2. GET: /WorkoutPlans/Details/5
        // View client's personalized workout plan and day-by-day routines
        [HttpGet]
        public ActionResult Details(int id)
        {
            string trainerId = GetCurrentTrainerId();
            var plan = db.WorkoutPlans
                .Include(p => p.Clients)
                .Include(p => p.Clients.ClientHealthProfile)
                .Include(p => p.Trainers)
                .Include(p => p.WorkoutPlanExercises.Select(wpe => wpe.Exercises))
                .FirstOrDefault(p => p.WorkoutPlanId == id);

            if (plan == null)
            {
                TempData["ErrorMessage"] = "The requested workout plan was not found.";
                return RedirectToAction("Index");
            }

            if (plan.TrainerId != trainerId || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, plan.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only view workout plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            var clientProfile = plan.Clients?.ClientHealthProfile?.FirstOrDefault();
            HealthDataProtectionHelper.DecryptHealthProfile(clientProfile);

            return View(plan);
        }

        // 2b. GET: /WorkoutPlans/ExerciseGuide?planId=3&exerciseId=1
        // Dedicated comprehensive exercise execution guide & client prescription
        [HttpGet]
        public ActionResult ExerciseGuide(int planId, int exerciseId)
        {
            string trainerId = GetCurrentTrainerId();
            var plan = db.WorkoutPlans
                .Include(p => p.Clients)
                .Include(p => p.Trainers)
                .Include(p => p.WorkoutPlanExercises.Select(wpe => wpe.Exercises))
                .FirstOrDefault(p => p.WorkoutPlanId == planId);

            if (plan == null)
            {
                TempData["ErrorMessage"] = "The requested workout plan was not found.";
                return RedirectToAction("Index");
            }

            if (plan.TrainerId != trainerId || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, plan.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only view workout plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            var exercise = db.Exercises.FirstOrDefault(e => e.ExerciseId == exerciseId);
            if (exercise == null)
            {
                TempData["ErrorMessage"] = "The requested exercise was not found in the library.";
                return RedirectToAction("Details", new { id = planId });
            }

            var prescription = plan.WorkoutPlanExercises.FirstOrDefault(wpe => wpe.ExerciseId == exerciseId);

            ViewBag.Plan = plan;
            ViewBag.Prescription = prescription;

            return View(exercise);
        }

        // GET: /Trainer/WorkoutPlans/Create
        // Create an individual personalized workout plan for an assigned client (optionally preselecting a library exercise)
        [HttpGet]
        public ActionResult Create(string clientId, int? exerciseId = null)
        {
            string trainerId = GetCurrentTrainerId();
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            if (!string.IsNullOrEmpty(clientId) && !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, clientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only create workout plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            var availableClients = GetAssignedClients(db, trainerId);
            var availableExercises = db.Exercises.OrderBy(e => e.Name).ToList();

            var selectedClient = !string.IsNullOrEmpty(clientId)
                ? availableClients.FirstOrDefault(c => c.Id == clientId)
                : availableClients.FirstOrDefault();

            int initialExerciseId = exerciseId ?? (availableExercises.FirstOrDefault() != null ? availableExercises.FirstOrDefault().ExerciseId : 1);
            var targetEx = availableExercises.FirstOrDefault(e => e.ExerciseId == initialExerciseId);

            string initialInstructions = "Focus on form and gradual warm-up.";
            if (targetEx != null && !string.IsNullOrWhiteSpace(targetEx.Instructions))
            {
                var lines = targetEx.Instructions.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0 && !string.IsNullOrWhiteSpace(lines[0]))
                {
                    initialInstructions = lines[0];
                }
            }

            var viewModel = new WorkoutPlanFormViewModel
            {
                ClientId = selectedClient != null ? selectedClient.Id : string.Empty,
                ClientName = selectedClient != null ? selectedClient.FullName : null,
                ClientEmail = selectedClient != null ? selectedClient.Email : null,
                Goal = selectedClient != null ? selectedClient.PrimaryGoal : "Weight Loss",
                Title = selectedClient != null ? $"{selectedClient.FullName}'s Personalized Routine" : "Client Workout Plan",
                AvailableClients = availableClients,
                AvailableExercises = availableExercises,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(28),
                Exercises = new List<WorkoutPlanExerciseItem>
                {
                    new WorkoutPlanExerciseItem
                    {
                        DayOfWeek = "Monday",
                        ExerciseId = initialExerciseId,
                        ExerciseName = targetEx != null ? targetEx.Name : "Exercise",
                        Sets = 3,
                        Reps = 12,
                        RestSeconds = 60,
                        Instructions = initialInstructions
                    }
                }
            };

            return View(viewModel);
        }

        // 4. POST: /WorkoutPlans/Create
        // Saves the client's personalized plan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(WorkoutPlanFormViewModel model)
        {
            string trainerId = GetCurrentTrainerId();
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            if (!AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, model.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only create workout plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableClients = GetAssignedClients(db, trainerId);
                model.AvailableExercises = db.Exercises.OrderBy(e => e.Name).ToList();
                return View(model);
            }

            if (model.IsActive)
            {
                var existingActive = db.WorkoutPlans.Where(p => p.ClientId == model.ClientId && p.IsActive).ToList();
                foreach (var p in existingActive)
                {
                    p.IsActive = false;
                }
            }

            string effectiveTrainerId = trainerId;

            var entity = new WorkoutPlans
            {
                ClientId = model.ClientId,
                TrainerId = effectiveTrainerId,
                Title = model.Title,
                Description = model.Description,
                Goal = model.Goal,
                DifficultyLevel = model.DifficultyLevel,
                DurationWeeks = model.DurationWeeks,
                DaysPerWeek = model.DaysPerWeek,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                ScheduledStartTime = model.ScheduledStartTime,
                TrainerNotes = model.TrainerNotes,
                IsActive = model.IsActive,
                TargetRepRange = model.TargetRepRange,
                WeeklySetVolume = model.WeeklySetVolume,
                RirTarget = model.RirTarget,
                RecommendedRest = model.RecommendedRest,
                ProgressionStrategy = model.ProgressionStrategy,
                RecoveryGuidelines = model.RecoveryGuidelines,
                CreatedAt = DateTime.UtcNow
            };

            if (model.Exercises != null && model.Exercises.Any())
            {
                int order = 1;
                foreach (var item in model.Exercises)
                {
                    entity.WorkoutPlanExercises.Add(new WorkoutPlanExercises
                    {
                        ExerciseId = item.ExerciseId,
                        DayOfWeek = item.DayOfWeek,
                        OrderIndex = order++,
                        Sets = item.Sets,
                        Reps = item.Reps,
                        DurationMinutes = item.DurationMinutes,
                        RestSeconds = item.RestSeconds,
                        Instructions = item.Instructions,
                        ExerciseRole = item.ExerciseRole
                    });
                }
            }

            db.WorkoutPlans.Add(entity);
            db.SaveChanges();

            var client = db.Clients.Find(entity.ClientId);
            TempData["SuccessMessage"] = $"Personalized plan created for {client?.FullName}!";
            return RedirectToAction("Details", new { id = entity.WorkoutPlanId });
        }

        // 5. GET: /WorkoutPlans/Edit/5
        // Modify client's personalized workout plan
        [HttpGet]
        public ActionResult Edit(int id)
        {
            string trainerId = GetCurrentTrainerId();
            var plan = db.WorkoutPlans
                .Include(p => p.Clients)
                .Include(p => p.Trainers)
                .Include(p => p.WorkoutPlanExercises.Select(wpe => wpe.Exercises))
                .FirstOrDefault(p => p.WorkoutPlanId == id);

            if (plan == null)
            {
                TempData["ErrorMessage"] = "Workout plan not found.";
                return RedirectToAction("Index");
            }

            if (plan.TrainerId != trainerId || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, plan.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only modify workout plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            var viewModel = new WorkoutPlanFormViewModel
            {
                Id = plan.WorkoutPlanId,
                ClientId = plan.ClientId,
                ClientName = plan.Clients != null ? plan.Clients.FullName : null,
                ClientEmail = plan.Clients != null ? plan.Clients.Email : null,
                Title = plan.Title,
                Description = plan.Description,
                Goal = plan.Goal,
                DifficultyLevel = plan.DifficultyLevel,
                DurationWeeks = plan.DurationWeeks,
                DaysPerWeek = plan.DaysPerWeek,
                StartDate = plan.StartDate,
                EndDate = plan.EndDate,
                ScheduledStartTime = plan.ScheduledStartTime,
                TrainerNotes = plan.TrainerNotes,
                IsActive = plan.IsActive,
                TargetRepRange = plan.TargetRepRange,
                WeeklySetVolume = plan.WeeklySetVolume,
                RirTarget = plan.RirTarget,
                RecommendedRest = plan.RecommendedRest,
                ProgressionStrategy = plan.ProgressionStrategy,
                RecoveryGuidelines = plan.RecoveryGuidelines,
                AvailableClients = GetAssignedClients(db, trainerId),
                AvailableExercises = db.Exercises.OrderBy(e => e.Name).ToList(),
                Exercises = plan.WorkoutPlanExercises.Select(pe => new WorkoutPlanExerciseItem
                {
                    Id = pe.WorkoutPlanExerciseId,
                    ExerciseId = pe.ExerciseId,
                    ExerciseName = pe.Exercises != null ? pe.Exercises.Name : null,
                    DayOfWeek = pe.DayOfWeek,
                    OrderIndex = orderIndexSafe(pe.OrderIndex),
                    Sets = pe.Sets,
                    Reps = pe.Reps,
                    DurationMinutes = pe.DurationMinutes,
                    RestSeconds = pe.RestSeconds,
                    Instructions = pe.Instructions,
                    ExerciseRole = pe.ExerciseRole
                }).ToList()
            };

            return View(viewModel);
        }

        private static int orderIndexSafe(int? orderIndex)
        {
            return orderIndex ?? 1;
        }

        // 6. POST: /WorkoutPlans/Edit/5
        // Saves changes to the client's personalized plan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(WorkoutPlanFormViewModel model)
        {
            string trainerId = GetCurrentTrainerId();
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var entity = db.WorkoutPlans
                .Include(p => p.WorkoutPlanExercises)
                .FirstOrDefault(p => p.WorkoutPlanId == model.Id);

            if (entity == null)
            {
                TempData["ErrorMessage"] = "Could not update the workout plan.";
                return RedirectToAction("Index");
            }

            if (entity.TrainerId != trainerId || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, entity.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only modify workout plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableClients = GetAssignedClients(db, trainerId);
                model.AvailableExercises = db.Exercises.OrderBy(e => e.Name).ToList();
                return View(model);
            }

            entity.Title = model.Title;
            entity.Description = model.Description;
            entity.Goal = model.Goal;
            entity.DifficultyLevel = model.DifficultyLevel;
            entity.DurationWeeks = model.DurationWeeks;
            entity.DaysPerWeek = model.DaysPerWeek;
            entity.StartDate = model.StartDate;
            entity.EndDate = model.EndDate;
            entity.ScheduledStartTime = model.ScheduledStartTime;
            entity.TrainerNotes = model.TrainerNotes;
            entity.IsActive = model.IsActive;
            entity.TargetRepRange = model.TargetRepRange;
            entity.WeeklySetVolume = model.WeeklySetVolume;
            entity.RirTarget = model.RirTarget;
            entity.RecommendedRest = model.RecommendedRest;
            entity.ProgressionStrategy = model.ProgressionStrategy;
            entity.RecoveryGuidelines = model.RecoveryGuidelines;
            entity.UpdatedAt = DateTime.UtcNow;

            db.WorkoutPlanExercises.RemoveRange(entity.WorkoutPlanExercises);

            if (model.Exercises != null && model.Exercises.Any())
            {
                int order = 1;
                foreach (var item in model.Exercises)
                {
                    entity.WorkoutPlanExercises.Add(new WorkoutPlanExercises
                    {
                        WorkoutPlanId = entity.WorkoutPlanId,
                        ExerciseId = item.ExerciseId,
                        DayOfWeek = item.DayOfWeek,
                        OrderIndex = order++,
                        Sets = item.Sets,
                        Reps = item.Reps,
                        DurationMinutes = item.DurationMinutes,
                        RestSeconds = item.RestSeconds,
                        Instructions = item.Instructions,
                        ExerciseRole = item.ExerciseRole
                    });
                }
            }

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Personalized plan for {model.ClientName ?? "client"} updated successfully!";
            return RedirectToAction("Details", new { id = model.Id });
        }

        // 7. POST: /WorkoutPlans/ToggleStatus/5
        // Toggles active/deactivated status for this client's plan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleStatus(int id)
        {
            string trainerId = GetCurrentTrainerId();
            var plan = db.WorkoutPlans
                .Include(p => p.Clients)
                .FirstOrDefault(p => p.WorkoutPlanId == id);

            if (plan == null)
            {
                TempData["ErrorMessage"] = "Plan not found.";
                return RedirectToAction("Index");
            }

            if (plan.TrainerId != trainerId || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, plan.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only modify workout plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            plan.IsActive = !plan.IsActive;
            plan.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();

            var statusText = plan.IsActive ? "activated" : "deactivated";
            TempData["SuccessMessage"] = $"Plan for {plan.Clients?.FullName} is now {statusText}.";

            return RedirectToAction("Index");
        }

        // 8. POST: /WorkoutPlans/Delete/5
        // Deletes client's plan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            string trainerId = GetCurrentTrainerId();
            var plan = db.WorkoutPlans
                .Include(p => p.Clients)
                .Include(p => p.WorkoutPlanExercises)
                .FirstOrDefault(p => p.WorkoutPlanId == id);

            if (plan != null)
            {
                if (plan.TrainerId != trainerId || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, plan.ClientId))
                {
                    TempData["ErrorMessage"] = "Access restricted: You can only remove workout plans for your currently assigned clients.";
                    return RedirectToAction("Index");
                }

                // FR-S-34 / FR-S-41: Preserve historical workout assignment records and completed logs
                if (db.ClientWorkoutLogs.Any(w => w.WorkoutPlanId == plan.WorkoutPlanId))
                {
                    plan.IsActive = false;
                    plan.UpdatedAt = DateTime.UtcNow;
                    db.SaveChanges();
                    TempData["SuccessMessage"] = $"Workout plan '{plan.Title}' has historical workout logs and has been archived/deactivated to preserve historical completion records.";
                }
                else
                {
                    db.WorkoutPlanExercises.RemoveRange(plan.WorkoutPlanExercises);
                    db.WorkoutPlans.Remove(plan);
                    db.SaveChanges();
                    TempData["SuccessMessage"] = $"Workout plan for {plan.Clients?.FullName} was removed.";
                }
            }

            return RedirectToAction("Index");
        }

        // 9. GET: /WorkoutPlans/Assign/5
        // Assign a workout plan to an assigned client
        [HttpGet]
        public ActionResult Assign(int id)
        {
            string trainerId = GetCurrentTrainerId();
            var plan = db.WorkoutPlans.FirstOrDefault(p => p.WorkoutPlanId == id);
            if (plan == null)
            {
                TempData["ErrorMessage"] = "Workout plan not found.";
                return RedirectToAction("Index");
            }

            if (plan.TrainerId != trainerId || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, plan.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only assign workout plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            var viewModel = new AssignPlanViewModel
            {
                WorkoutPlanId = plan.WorkoutPlanId,
                WorkoutPlanTitle = plan.Title,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(plan.DurationWeeks > 0 ? plan.DurationWeeks * 7 : 28),
                AvailableClients = GetAssignedClients(db, trainerId)
            };

            return View(viewModel);
        }

        // 10. POST: /WorkoutPlans/Assign
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Assign(AssignPlanViewModel model)
        {
            string trainerId = GetCurrentTrainerId();
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            if (!AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, model.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: Target client is not currently assigned to you.";
                return RedirectToAction("Index");
            }

            var plan = db.WorkoutPlans.FirstOrDefault(p => p.WorkoutPlanId == model.WorkoutPlanId);
            if (plan == null)
            {
                TempData["ErrorMessage"] = "Workout plan not found.";
                return RedirectToAction("Index");
            }

            if (plan.TrainerId != trainerId || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, plan.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only assign workout plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableClients = GetAssignedClients(db, trainerId);
                return View(model);
            }

            plan.ClientId = model.ClientId;
            plan.StartDate = model.StartDate;
            plan.EndDate = model.EndDate;
            if (!string.IsNullOrWhiteSpace(model.Notes))
            {
                plan.TrainerNotes = model.Notes;
            }
            plan.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();

            var client = db.Clients.FirstOrDefault(c => c.ClientId == model.ClientId);
            TempData["SuccessMessage"] = $"Plan '{plan.Title}' was assigned to {client?.FullName ?? "client"} successfully!";
            return RedirectToAction("Details", new { id = plan.WorkoutPlanId });
        }

        // GET: /Trainer/WorkoutPlans/GetClientHealthProfile
        // Dynamic AJAX endpoint for client physiological snapshot, injuries, and medical conditions
        [HttpGet]
        public JsonResult GetClientHealthProfile(string clientId)
        {
            if (string.IsNullOrEmpty(clientId))
                return Json(new { success = false, message = "No client selected" }, JsonRequestBehavior.AllowGet);

            string trainerId = GetCurrentTrainerId();
            if (string.IsNullOrEmpty(trainerId) || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, clientId))
            {
                HealthAuditHelper.Log(
                    actorUserId: trainerId ?? "Anonymous",
                    actorRole: "Trainer",
                    clientId: clientId,
                    recordType: "HealthProfile",
                    recordId: null,
                    action: "View",
                    wasSuccessful: false,
                    details: "Blocked unauthorized attempt to retrieve client health profile for workout plan");
                return Json(new { success = false, message = "Access restricted: Client is not currently assigned to you." }, JsonRequestBehavior.AllowGet);
            }

            var clients = GetAssignedClients(db, trainerId);
            var client = clients.FirstOrDefault(c => c.Id == clientId);
            if (client == null)
                return Json(new { success = false, message = "Client not found" }, JsonRequestBehavior.AllowGet);

            HealthAuditHelper.Log(
                actorUserId: trainerId,
                actorRole: "Trainer",
                clientId: client.Id,
                recordType: "HealthProfile",
                recordId: null,
                action: "View",
                wasSuccessful: true,
                details: "Retrieved client clinical and injury profile for workout plan builder");

            return Json(new
            {
                success = true,
                clientId = client.Id,
                fullName = client.FullName,
                email = client.Email,
                primaryGoal = client.PrimaryGoal,
                gender = client.Gender,
                heightCm = client.HeightCm,
                allergies = client.Allergies,
                dietaryRestrictions = client.DietaryRestrictions,
                medicalConditions = client.MedicalConditions,
                currentWeightKg = client.CurrentWeightKg,
                baselineWeightKg = client.BaselineWeightKg,
                targetWeightKg = client.TargetWeightKg,
                currentBmi = client.CurrentBmi,
                bmiCategory = client.BmiCategory
            }, JsonRequestBehavior.AllowGet);
        }

        // 11. GET: /Trainer/WorkoutPlans/PopularRoutines
        // FR-T-49: Popular Workout Routine Report
        // Generates report ranking popular workout routines by client assignment counts
        [HttpGet]
        public ActionResult PopularRoutines(string goal = null, string preset = "all", DateTime? fromDate = null, DateTime? toDate = null, string status = "active")
        {
            string trainerId = GetCurrentTrainerId();

            var dateRange = ResolveReportDateRange(preset, fromDate, toDate);
            DateTime start = dateRange.Item1;
            DateTime end = dateRange.Item2;
            string activePreset = dateRange.Item3;

            // Authorized trainer's workout plans only
            var query = db.WorkoutPlans.Where(p => p.TrainerId == trainerId);

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

            // Optional Filter: Goal
            if (!IsAllOrEmpty(goal))
            {
                var g = goal.Trim();
                query = query.Where(p => p.Goal == g);
            }

            // Optional Filter: Date Range (applied to CreatedAt or StartDate)
            if (activePreset != "all")
            {
                query = query.Where(p => p.CreatedAt >= start && p.CreatedAt <= end);
            }

            // Select lightweight database projection (no joins with exercises or logs, avoiding cartesian duplicates)
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

            // Group assignments strictly by workout routine title/identifier
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

            var viewModel = new PopularRoutinesReportViewModel
            {
                RankedRoutines = rankedList,
                TotalUniqueRoutines = rankedList.Count,
                TotalValidAssignments = totalValidAssignments,
                TotalClientsServed = planSummaries.Select(p => p.ClientId).Distinct().Count(),
                TopRoutineTitle = topRoutine != null ? topRoutine.RoutineTitle : "None",
                TopRoutineAssignments = topRoutine != null ? topRoutine.TotalAssignments : 0,

                SelectedGoal = goal,
                StatusFilter = status,
                DatePreset = activePreset,
                FromDate = fromDate,
                ToDate = toDate,
                AvailableGoals = new List<string> { "All Goals", "Weight Loss", "Muscle Building", "Endurance", "Strength", "General Fitness" }
            };

            return View(viewModel);
        }

        private string GetCurrentTrainerId()
        {
            return SessionHelper.GetCurrentTrainerId(Session);
        }

        private static Tuple<DateTime, DateTime, string> ResolveReportDateRange(string preset, DateTime? fromDate, DateTime? toDate)
        {
            var end = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : DateTime.Today.AddDays(1).AddTicks(-1);
            var activePreset = string.IsNullOrEmpty(preset) ? "all" : preset.ToLower();

            DateTime start;
            switch (activePreset)
            {
                case "7days":
                    start = DateTime.Today.AddDays(-7);
                    break;
                case "30days":
                    start = DateTime.Today.AddDays(-30);
                    break;
                case "90days":
                    start = DateTime.Today.AddDays(-90);
                    break;
                case "custom":
                    start = fromDate.HasValue ? fromDate.Value.Date : new DateTime(2000, 1, 1);
                    if (toDate.HasValue)
                    {
                        end = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    }
                    break;
                case "all":
                default:
                    start = new DateTime(2000, 1, 1);
                    activePreset = "all";
                    break;
            }

            return Tuple.Create(start, end, activePreset);
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
