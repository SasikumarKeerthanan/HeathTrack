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
    public class DietPlansController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        private static bool IsAllOrEmpty(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            var trimmed = value.Trim();
            return trimmed.Equals("all", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("all ", StringComparison.OrdinalIgnoreCase);
        }

        private string GetCurrentTrainerId()
        {
            return SessionHelper.GetCurrentTrainerId(Session);
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

        private static List<DietPlans> GetFilteredPlans(HealthTrackEntities context, string trainerId, string search = null, string preference = null, string status = "all", string clientId = null)
        {
            var query = context.DietPlans
                .Include(dp => dp.Clients)
                .Include(dp => dp.Trainers)
                .Include(dp => dp.DietMeals)
                .Where(dp => dp.TrainerId == trainerId && dp.Clients.AssignedTrainerId == trainerId && dp.Clients.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(dp =>
                    dp.Title.Contains(term)
                    || dp.Description.Contains(term)
                    || dp.DietaryPreference.Contains(term)
                    || (dp.SpecialInstructions != null && dp.SpecialInstructions.Contains(term))
                    || dp.Clients.FullName.Contains(term)
                    || dp.Clients.Email.Contains(term)
                    || dp.Trainers.FullName.Contains(term)
                    || dp.DietMeals.Any(dm =>
                        dm.MealName.Contains(term)
                        || dm.MealType.Contains(term)
                        || (dm.IngredientsAndPortion != null && dm.IngredientsAndPortion.Contains(term))
                        || (dm.Instructions != null && dm.Instructions.Contains(term)))
                );
            }

            if (!IsAllOrEmpty(preference))
            {
                var pref = preference.Trim();
                query = query.Where(dp => dp.DietaryPreference == pref);
            }

            if (!IsAllOrEmpty(status))
            {
                if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(dp => dp.IsActive);
                }
                else if (status.Equals("deactivated", StringComparison.OrdinalIgnoreCase)
                      || status.Equals("inactive", StringComparison.OrdinalIgnoreCase)
                      || status.Equals("archived", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(dp => !dp.IsActive);
                }
            }

            if (!IsAllOrEmpty(clientId))
            {
                var cId = clientId.Trim();
                query = query.Where(dp => dp.ClientId == cId);
            }

            return query.OrderByDescending(dp => dp.CreatedAt).ToList();
        }

        // 1. GET: /DietPlans
        // Lists client personalized diet plans with filters and status counters
        [HttpGet]
        public ActionResult Index(string search, string preference, string status = "all", string clientId = null)
        {
            string trainerId = GetCurrentTrainerId();
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var allPlans = GetFilteredPlans(db, trainerId);
            var plans = GetFilteredPlans(db, trainerId, search, preference, status, clientId);
            var clients = GetAssignedClients(db, trainerId);

            var viewModel = new DietPlanIndexViewModel
            {
                Plans = plans,
                AllPlans = allPlans,
                AssignedClients = clients,
                SearchTerm = search,
                SelectedPreference = preference,
                SelectedStatus = status,
                SelectedClientId = clientId
            };

            return View(viewModel);
        }

        // 2. GET: /DietPlans/Details/5
        // View client's personalized diet plan, daily calorie budget, and scheduled meals
        [HttpGet]
        public ActionResult Details(int id)
        {
            string trainerId = GetCurrentTrainerId();
            var plan = db.DietPlans
                .Include(dp => dp.Clients)
                .Include(dp => dp.Clients.ClientHealthProfile)
                .Include(dp => dp.Trainers)
                .Include(dp => dp.DietMeals)
                .FirstOrDefault(dp => dp.DietPlanId == id);

            if (plan == null)
            {
                TempData["ErrorMessage"] = "The requested diet plan was not found.";
                return RedirectToAction("Index");
            }

            if (plan.TrainerId != trainerId || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, plan.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only view diet plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            var clientProfile = plan.Clients?.ClientHealthProfile?.FirstOrDefault();
            HealthDataProtectionHelper.DecryptHealthProfile(clientProfile);

            return View(plan);
        }

        // GET: /Trainer/DietPlans/Create
        // Form to assign and design an individual personalized diet plan for a client
        [HttpGet]
        public ActionResult Create(string clientId)
        {
            string trainerId = GetCurrentTrainerId();
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            if (!string.IsNullOrEmpty(clientId) && !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, clientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only create diet plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            var availableClients = GetAssignedClients(db, trainerId);

            var selectedClient = !string.IsNullOrEmpty(clientId)
                ? availableClients.FirstOrDefault(c => c.Id == clientId)
                : null;

            var blankMeal = new DietMealFormItem
            {
                DayOfWeek = "Daily Routine",
                MealType = "Breakfast",
                ScheduledTime = string.Empty,
                MealName = string.Empty,
                IngredientsAndPortion = string.Empty,
                Calories = null,
                ProteinGrams = null,
                CarbsGrams = null,
                FatsGrams = null,
                Instructions = string.Empty
            };

            var viewModel = new DietPlanFormViewModel
            {
                ClientId = selectedClient != null ? selectedClient.Id : string.Empty,
                ClientName = selectedClient != null ? selectedClient.FullName : null,
                ClientEmail = selectedClient != null ? selectedClient.Email : null,
                Title = string.Empty,
                Description = string.Empty,
                DietaryPreference = string.Empty,
                DailyCalorieTarget = null,
                TargetProteinGrams = null,
                TargetCarbsGrams = null,
                TargetFatsGrams = null,
                WaterIntakeLiters = null,
                DurationWeeks = null,
                StartDate = null,
                EndDate = null,
                SpecialInstructions = string.Empty,
                IsActive = true,
                AvailableClients = availableClients,
                Meals = new List<DietMealFormItem> { blankMeal }
            };

            return View(viewModel);
        }

        // 4. POST: /DietPlans/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(DietPlanFormViewModel model)
        {
            string trainerId = GetCurrentTrainerId();
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            if (!AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, model.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only create diet plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableClients = GetAssignedClients(db, trainerId);
                return View(model);
            }

            // Deactivate other plans for this client if newly created plan is active
            if (model.IsActive)
            {
                var existingActive = db.DietPlans.Where(dp => dp.ClientId == model.ClientId && dp.IsActive).ToList();
                foreach (var p in existingActive)
                {
                    p.IsActive = false;
                }
            }

            string effectiveTrainerId = trainerId;

            var entity = new DietPlans
            {
                ClientId = model.ClientId,
                TrainerId = effectiveTrainerId,
                Title = model.Title,
                Description = model.Description,
                DietaryPreference = model.DietaryPreference,
                DailyCalorieTarget = model.DailyCalorieTarget.GetValueOrDefault(),
                TargetProteinGrams = model.TargetProteinGrams.GetValueOrDefault(),
                TargetCarbsGrams = model.TargetCarbsGrams.GetValueOrDefault(),
                TargetFatsGrams = model.TargetFatsGrams.GetValueOrDefault(),
                WaterIntakeLiters = model.WaterIntakeLiters.HasValue ? (decimal?)model.WaterIntakeLiters.Value : null,
                DurationWeeks = model.DurationWeeks.GetValueOrDefault(),
                StartDate = model.StartDate.GetValueOrDefault(DateTime.Today),
                EndDate = model.EndDate.GetValueOrDefault(DateTime.Today.AddDays(28)),
                SpecialInstructions = model.SpecialInstructions,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            if (model.Meals != null && model.Meals.Any())
            {
                int order = 1;
                for (int i = 0; i < model.Meals.Count; i++)
                {
                    var m = model.Meals[i];
                    if (!string.IsNullOrWhiteSpace(m.MealName))
                    {
                        var uploadedFile = m.ImageFile ?? Request.Files[$"Meals[{i}].ImageFile"];
                        string savedPath = SaveMealImage(uploadedFile);
                        string finalImagePath = !string.IsNullOrEmpty(savedPath) ? savedPath : m.ImagePath;

                        entity.DietMeals.Add(new DietMeals
                        {
                            DayOfWeek = string.IsNullOrEmpty(m.DayOfWeek) ? "Daily Routine" : m.DayOfWeek,
                            MealType = string.IsNullOrEmpty(m.MealType) ? "Breakfast" : m.MealType,
                            ScheduledTime = m.ScheduledTime,
                            MealName = m.MealName,
                            IngredientsAndPortion = m.IngredientsAndPortion,
                            Calories = m.Calories.GetValueOrDefault(),
                            ProteinGrams = m.ProteinGrams,
                            CarbsGrams = m.CarbsGrams,
                            FatsGrams = m.FatsGrams,
                            Instructions = m.Instructions,
                            OrderIndex = order++,
                            ImagePath = finalImagePath,
                            ServingSize = m.ServingSize,
                            Description = m.Description,
                            Ingredients = m.Ingredients,
                            Allergens = m.Allergens,
                            DietaryInfo = m.DietaryInfo,
                            TrainerNotes = m.TrainerNotes
                        });
                    }
                }
            }

            db.DietPlans.Add(entity);
            db.SaveChanges();

            var client = db.Clients.Find(entity.ClientId);
            TempData["SuccessMessage"] = $"Diet plan '{entity.Title}' successfully created and assigned to {client?.FullName}!";
            return RedirectToAction("Details", new { id = entity.DietPlanId });
        }

        // 5. GET: /DietPlans/Edit/5
        [HttpGet]
        public ActionResult Edit(int id)
        {
            string trainerId = GetCurrentTrainerId();
            var plan = db.DietPlans
                .Include(dp => dp.Clients)
                .Include(dp => dp.Clients.ClientHealthProfile)
                .Include(dp => dp.DietMeals)
                .FirstOrDefault(dp => dp.DietPlanId == id);

            if (plan == null)
            {
                TempData["ErrorMessage"] = "Diet plan not found.";
                return RedirectToAction("Index");
            }

            if (plan.TrainerId != trainerId || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, plan.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only modify diet plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            var assignedClients = GetAssignedClients(db, trainerId);
            var clientSummary = assignedClients.FirstOrDefault(c => c.Id == plan.ClientId);
            var healthProfile = plan.Clients?.ClientHealthProfile.FirstOrDefault();

            var viewModel = new DietPlanFormViewModel
            {
                Id = plan.DietPlanId,
                ClientId = plan.ClientId,
                ClientName = plan.Clients != null ? plan.Clients.FullName : null,
                ClientEmail = plan.Clients != null ? plan.Clients.Email : null,
                ClientAllergies = clientSummary != null ? clientSummary.Allergies : (healthProfile?.Allergies ?? "None"),
                ClientDietaryRestrictions = clientSummary != null ? clientSummary.DietaryRestrictions : (healthProfile?.DietaryRestrictions ?? "Balanced"),
                ClientMedicalConditions = clientSummary != null ? clientSummary.MedicalConditions : (healthProfile?.MedicalConditions ?? "None"),
                ClientWeightKg = clientSummary != null ? clientSummary.CurrentWeightKg : null,
                ClientTargetWeightKg = clientSummary != null ? clientSummary.TargetWeightKg : null,
                ClientBmi = clientSummary != null ? clientSummary.CurrentBmi : null,
                ClientBmiCategory = clientSummary != null ? clientSummary.BmiCategory : "Normal",
                Title = plan.Title,
                Description = plan.Description,
                DietaryPreference = plan.DietaryPreference,
                DailyCalorieTarget = plan.DailyCalorieTarget,
                TargetProteinGrams = plan.TargetProteinGrams,
                TargetCarbsGrams = plan.TargetCarbsGrams,
                TargetFatsGrams = plan.TargetFatsGrams,
                WaterIntakeLiters = plan.WaterIntakeLiters.HasValue ? (double)plan.WaterIntakeLiters.Value : (double?)null,
                DurationWeeks = plan.DurationWeeks,
                StartDate = plan.StartDate,
                EndDate = plan.EndDate,
                SpecialInstructions = plan.SpecialInstructions,
                IsActive = plan.IsActive,
                AvailableClients = assignedClients,
                Meals = plan.DietMeals.OrderBy(m => m.OrderIndex).Select(m => new DietMealFormItem
                {
                    Id = m.DietMealId,
                    DayOfWeek = m.DayOfWeek,
                    MealType = m.MealType,
                    ScheduledTime = m.ScheduledTime,
                    MealName = m.MealName,
                    IngredientsAndPortion = m.IngredientsAndPortion,
                    Calories = m.Calories,
                    ProteinGrams = m.ProteinGrams,
                    CarbsGrams = m.CarbsGrams,
                    FatsGrams = m.FatsGrams,
                    Instructions = m.Instructions,
                    OrderIndex = m.OrderIndex,
                    ImagePath = m.ImagePath,
                    ServingSize = m.ServingSize,
                    Description = m.Description,
                    Ingredients = m.Ingredients,
                    Allergens = m.Allergens,
                    DietaryInfo = m.DietaryInfo,
                    TrainerNotes = m.TrainerNotes
                }).ToList()
            };

            return View(viewModel);
        }

        // 6. POST: /DietPlans/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(DietPlanFormViewModel model)
        {
            string trainerId = GetCurrentTrainerId();
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var entity = db.DietPlans
                .Include(dp => dp.DietMeals)
                .FirstOrDefault(dp => dp.DietPlanId == model.Id);

            if (entity == null)
            {
                TempData["ErrorMessage"] = "Failed to update diet plan.";
                return RedirectToAction("Index");
            }

            if (entity.TrainerId != trainerId || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, entity.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only modify diet plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableClients = GetAssignedClients(db, trainerId);
                return View(model);
            }

            entity.Title = model.Title;
            entity.Description = model.Description;
            entity.DietaryPreference = model.DietaryPreference;
            entity.DailyCalorieTarget = model.DailyCalorieTarget.GetValueOrDefault();
            entity.TargetProteinGrams = model.TargetProteinGrams.GetValueOrDefault();
            entity.TargetCarbsGrams = model.TargetCarbsGrams.GetValueOrDefault();
            entity.TargetFatsGrams = model.TargetFatsGrams.GetValueOrDefault();
            entity.WaterIntakeLiters = model.WaterIntakeLiters.HasValue ? (decimal?)model.WaterIntakeLiters.Value : null;
            entity.DurationWeeks = model.DurationWeeks.GetValueOrDefault();
            entity.StartDate = model.StartDate.GetValueOrDefault(DateTime.Today);
            entity.EndDate = model.EndDate.GetValueOrDefault(DateTime.Today.AddDays(28));
            entity.SpecialInstructions = model.SpecialInstructions;
            entity.IsActive = model.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            // Remove existing meals and insert new ones
            db.DietMeals.RemoveRange(entity.DietMeals);

            if (model.Meals != null && model.Meals.Any())
            {
                int order = 1;
                for (int i = 0; i < model.Meals.Count; i++)
                {
                    var m = model.Meals[i];
                    if (!string.IsNullOrWhiteSpace(m.MealName))
                    {
                        var uploadedFile = m.ImageFile ?? Request.Files[$"Meals[{i}].ImageFile"];
                        string savedPath = SaveMealImage(uploadedFile);
                        string finalImagePath = !string.IsNullOrEmpty(savedPath) ? savedPath : m.ImagePath;

                        entity.DietMeals.Add(new DietMeals
                        {
                            DietPlanId = entity.DietPlanId,
                            DayOfWeek = string.IsNullOrEmpty(m.DayOfWeek) ? "Daily Routine" : m.DayOfWeek,
                            MealType = string.IsNullOrEmpty(m.MealType) ? "Breakfast" : m.MealType,
                            ScheduledTime = m.ScheduledTime,
                            MealName = m.MealName,
                            IngredientsAndPortion = m.IngredientsAndPortion,
                            Calories = m.Calories.GetValueOrDefault(),
                            ProteinGrams = m.ProteinGrams,
                            CarbsGrams = m.CarbsGrams,
                            FatsGrams = m.FatsGrams,
                            Instructions = m.Instructions,
                            OrderIndex = order++,
                            ImagePath = finalImagePath,
                            ServingSize = m.ServingSize,
                            Description = m.Description,
                            Ingredients = m.Ingredients,
                            Allergens = m.Allergens,
                            DietaryInfo = m.DietaryInfo,
                            TrainerNotes = m.TrainerNotes
                        });
                    }
                }
            }

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Diet plan '{model.Title}' updated successfully!";
            return RedirectToAction("Details", new { id = model.Id });
        }

        // 7. POST: /DietPlans/ToggleStatus/5
        // Deactivate / Reactivate a client's diet plan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleStatus(int id)
        {
            string trainerId = GetCurrentTrainerId();
            var plan = db.DietPlans
                .Include(dp => dp.Clients)
                .FirstOrDefault(dp => dp.DietPlanId == id);

            if (plan == null)
            {
                TempData["ErrorMessage"] = "Diet plan not found.";
                return RedirectToAction("Index");
            }

            if (plan.TrainerId != trainerId || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, plan.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: You can only modify diet plans for your currently assigned clients.";
                return RedirectToAction("Index");
            }

            plan.IsActive = !plan.IsActive;
            plan.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();

            string statusText = plan.IsActive ? "reactivated and published" : "deactivated / archived";
            string clientName = plan.Clients != null ? plan.Clients.FullName : "client";
            TempData["SuccessMessage"] = $"Diet plan for {clientName} was {statusText}.";
            return RedirectToAction("Details", new { id });
        }

        // 8. POST: /DietPlans/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            string trainerId = GetCurrentTrainerId();
            var plan = db.DietPlans
                .Include(dp => dp.Clients)
                .Include(dp => dp.DietMeals)
                .FirstOrDefault(dp => dp.DietPlanId == id);

            if (plan != null)
            {
                if (plan.TrainerId != trainerId || !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, plan.ClientId))
                {
                    TempData["ErrorMessage"] = "Access restricted: You can only remove diet plans for your currently assigned clients.";
                    return RedirectToAction("Index");
                }

                string clientName = plan.Clients != null ? plan.Clients.FullName : "client";
                string title = plan.Title;
                db.DietMeals.RemoveRange(plan.DietMeals);
                db.DietPlans.Remove(plan);
                db.SaveChanges();
                TempData["SuccessMessage"] = $"Diet plan '{title}' for {clientName} was deleted.";
            }

            return RedirectToAction("Index");
        }

        // GET: /Trainer/DietPlans/GetClientHealthProfile
        // Dynamic AJAX endpoint for client physiological snapshot, allergies, and dietary constraints
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
                    details: "Blocked unauthorized attempt to retrieve client health profile for diet plan");
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
                details: "Retrieved client clinical and dietary profile for diet plan builder");

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

        private string SaveMealImage(System.Web.HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0) return null;

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = System.IO.Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext)) return null;

            // 5MB limit
            if (file.ContentLength > 5 * 1024 * 1024) return null;

            var folder = Server.MapPath("~/Content/images/meals/uploads/");
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }

            var uniqueName = $"meal_{DateTime.UtcNow.Ticks}_{Guid.NewGuid().ToString().Substring(0, 8)}{ext}";
            var physicalPath = System.IO.Path.Combine(folder, uniqueName);
            file.SaveAs(physicalPath);

            return $"~/Content/images/meals/uploads/{uniqueName}";
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
