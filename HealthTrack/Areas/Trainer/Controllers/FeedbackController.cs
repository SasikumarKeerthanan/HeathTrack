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
    // FeedbackController: Trainer Feedback Workflow
    // Page 1: Send Feedback (Create & send or save draft with client snapshot)
    // Page 2: Feedback History (Searchable audit log with read-only enforcement)
    // ========================================================================
    [TrainerAuthorize]
    public class FeedbackController : Controller
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
        // Helper: Get Assigned Clients for current trainer as SelectListItem list
        // --------------------------------------------------------------------
        private List<SelectListItem> GetAssignedClientsSelectList(string trainerId, string selectedClientId = null)
        {
            return db.Clients
                .Where(c => c.AssignedTrainerId == trainerId && c.IsActive)
                .OrderBy(c => c.FullName)
                .Select(c => new SelectListItem
                {
                    Value = c.ClientId,
                    Text = c.FullName + " (" + c.Email + ")",
                    Selected = (selectedClientId != null && c.ClientId == selectedClientId)
                })
                .ToList();
        }

        // --------------------------------------------------------------------
        // Helper: Build live client snapshot stats (used in Page 1 side panel)
        // --------------------------------------------------------------------
        private ClientFeedbackSnapshotDto BuildClientSnapshot(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId)) return null;

            string trainerId = GetCurrentTrainerId();
            if (!AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, clientId)) return null;

            var client = db.Clients
                .Include(c => c.ClientHealthProfile)
                .Include(c => c.ClientPreferences)
                .Include(c => c.ClientProgressRecords)
                .Include(c => c.WorkoutPlans)
                .Include(c => c.DietPlans)
                .FirstOrDefault(c => c.ClientId == clientId && c.AssignedTrainerId == trainerId && c.IsActive);

            if (client == null) return null;

            var latestProgress = client.ClientProgressRecords.OrderByDescending(r => r.RecordedDate).FirstOrDefault();
            var activeWorkout = client.WorkoutPlans.FirstOrDefault(p => p.IsActive);
            var activeDiet = client.DietPlans.FirstOrDefault(p => p.IsActive);
            var preference = client.ClientPreferences.FirstOrDefault();

            // Last workout summary
            var lastWorkout = db.ClientWorkoutLogs
                .Where(w => w.ClientId == clientId)
                .OrderByDescending(w => w.LogDate)
                .FirstOrDefault();

            string lastWorkoutSummary = "No workouts recorded yet";
            if (lastWorkout != null)
            {
                lastWorkoutSummary = $"{lastWorkout.SessionName} ({lastWorkout.DurationMinutes}m, RPE {lastWorkout.PerceivedExertion}) on {lastWorkout.LogDate:MMM dd}";
            }

            // Logged meals count
            int loggedMeals = 0;
            try
            {
                loggedMeals = db.ClientMealLogs.Count(m => m.ClientId == clientId);
            }
            catch
            {
                loggedMeals = client.ClientProgressRecords.Count(p => p.CaloriesConsumed > 0);
            }

            decimal? bmi = latestProgress != null ? latestProgress.Bmi : null;
            string bmiCat = "Normal";
            if (bmi.HasValue)
            {
                if (bmi < 18.5m) bmiCat = "Underweight";
                else if (bmi < 25.0m) bmiCat = "Normal";
                else if (bmi < 30.0m) bmiCat = "Overweight";
                else bmiCat = "Obese";
            }

            return new ClientFeedbackSnapshotDto
            {
                ClientId = client.ClientId,
                FullName = client.FullName,
                Email = client.Email,
                CurrentWeightKg = latestProgress != null ? (decimal?)latestProgress.WeightKg : null,
                CurrentBmi = bmi,
                BmiCategory = bmiCat,
                PrimaryGoal = preference != null && !string.IsNullOrWhiteSpace(preference.PrimaryGoal) ? preference.PrimaryGoal : "General Fitness",
                ActiveWorkoutPlan = activeWorkout != null ? activeWorkout.Title : "None assigned",
                ActiveDietPlan = activeDiet != null ? activeDiet.Title : "None assigned",
                LastWorkoutSummary = lastWorkoutSummary,
                LoggedMealsCount = loggedMeals
            };
        }

        // ====================================================================
        // Default Redirect: /Trainer/Feedback -> /Trainer/Feedback/History
        // ====================================================================
        [HttpGet]
        public ActionResult Index(string clientId = null, string category = null, string search = null)
        {
            return RedirectToAction("History", new { clientId, category, search });
        }

        // ====================================================================
        // PAGE 2: GET /Trainer/Feedback/History
        // Review all previously sent feedback with search, filter & follow-up
        // ====================================================================
        [HttpGet]
        public ActionResult History(string clientId = null, string category = null, string search = null)
        {
            string trainerId = GetCurrentTrainerId();

            var query = db.TrainerFeedback
                .Include(f => f.Clients)
                .Include(f => f.TrainerFeedback2) // Parent
                .Include(f => f.TrainerFeedback1) // Children (follow-ups)
                .Where(f => f.TrainerId == trainerId && f.Clients.AssignedTrainerId == trainerId && f.Clients.IsActive);

            // Filter by Client
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                query = query.Where(f => f.ClientId == clientId);
            }

            // Filter by Category (Workout, Diet, General Progress)
            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(f => f.Category == category);
            }

            // Filter by Search Term (Title or Content)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(f => f.Title.Contains(term) || f.FeedbackText.Contains(term) || f.Clients.FullName.Contains(term));
            }

            var feedbackEntities = query
                .OrderByDescending(f => f.SentAt)
                .ToList();

            var items = feedbackEntities.Select(f => new FeedbackHistoryItemViewModel
            {
                FeedbackId = f.FeedbackId,
                TrainerId = f.TrainerId,
                ClientId = f.ClientId,
                ClientName = f.Clients != null ? f.Clients.FullName : "Client",
                ClientEmail = f.Clients != null ? f.Clients.Email : "",
                Category = f.Category,
                Title = f.Title,
                FeedbackText = f.FeedbackText,
                Recommendations = f.Recommendations,
                IsDraft = f.IsDraft,
                SentAt = f.SentAt,
                ParentFeedbackId = f.ParentFeedbackId,
                ParentFeedbackTitle = f.TrainerFeedback2 != null ? f.TrainerFeedback2.Title : null,
                FollowUpCount = f.TrainerFeedback1 != null ? f.TrainerFeedback1.Count : 0
            }).ToList();

            var viewModel = new FeedbackHistoryViewModel
            {
                SelectedClientId = clientId,
                SelectedCategory = category ?? "all",
                SearchTerm = search,
                AvailableClients = GetAssignedClientsSelectList(trainerId, clientId),
                FeedbackList = items,
                TotalSentCount = items.Count(i => !i.IsDraft),
                TotalDraftsCount = items.Count(i => i.IsDraft)
            };

            return View(viewModel);
        }

        // ====================================================================
        // PAGE 1: GET /Trainer/Feedback/Send
        // Create new feedback or resume a draft
        // ====================================================================
        [HttpGet]
        public ActionResult Send(string clientId = null, int? parentId = null, int? draftId = null, string category = null)
        {
            string trainerId = GetCurrentTrainerId();

            if (!string.IsNullOrWhiteSpace(clientId) && !AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, clientId))
            {
                TempData["ErrorMessage"] = "Access restricted: Selected client is not currently assigned to your trainer profile.";
                return RedirectToAction("History");
            }

            var availableClients = GetAssignedClientsSelectList(trainerId, clientId);

            // Determine active target client ID
            string targetClientId = clientId;
            if (string.IsNullOrWhiteSpace(targetClientId) && availableClients.Any())
            {
                targetClientId = availableClients.First().Value;
            }

            var viewModel = new SendFeedbackViewModel
            {
                ClientId = targetClientId,
                AvailableClients = availableClients,
                Category = !string.IsNullOrWhiteSpace(category) ? category : "Workout",
                ParentFeedbackId = parentId,
                ClientSnapshot = BuildClientSnapshot(targetClientId)
            };

            // If editing an existing draft
            if (draftId.HasValue)
            {
                var draft = db.TrainerFeedback.FirstOrDefault(f => f.FeedbackId == draftId.Value && f.TrainerId == trainerId);
                if (draft != null && draft.IsDraft)
                {
                    viewModel.FeedbackId = draft.FeedbackId;
                    viewModel.ClientId = draft.ClientId;
                    viewModel.Category = draft.Category;
                    viewModel.Title = draft.Title;
                    viewModel.FeedbackText = draft.FeedbackText;
                    viewModel.Recommendations = draft.Recommendations;
                    viewModel.IsDraft = true;
                    viewModel.ParentFeedbackId = draft.ParentFeedbackId;
                    viewModel.ClientSnapshot = BuildClientSnapshot(draft.ClientId);
                }
            }

            // If creating a follow-up to previous feedback
            if (parentId.HasValue)
            {
                var parent = db.TrainerFeedback.Include(f => f.Clients).FirstOrDefault(f => f.FeedbackId == parentId.Value && f.TrainerId == trainerId);
                if (parent != null)
                {
                    viewModel.ParentFeedbackTitle = parent.Title;
                    viewModel.ParentFeedbackDate = parent.SentAt;
                    viewModel.ClientId = parent.ClientId;
                    viewModel.Category = parent.Category;
                    viewModel.ClientSnapshot = BuildClientSnapshot(parent.ClientId);
                    if (string.IsNullOrWhiteSpace(viewModel.Title))
                    {
                        viewModel.Title = "Follow-up: " + parent.Title;
                    }
                }
            }

            return View(viewModel);
        }

        // ====================================================================
        // PAGE 1: POST /Trainer/Feedback/Send
        // Handles "Send Feedback" (Read-Only) OR "Save as Draft" (Editable)
        // ====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Send(SendFeedbackViewModel model, string submitAction)
        {
            string trainerId = GetCurrentTrainerId();
            bool isDraft = string.Equals(submitAction, "draft", StringComparison.OrdinalIgnoreCase);

            if (!ModelState.IsValid)
            {
                model.AvailableClients = GetAssignedClientsSelectList(trainerId, model.ClientId);
                model.ClientSnapshot = BuildClientSnapshot(model.ClientId);
                return View(model);
            }

            // Security Check: Verify selected client is actively assigned to logged-in trainer
            var client = db.Clients.FirstOrDefault(c => c.ClientId == model.ClientId && c.AssignedTrainerId == trainerId && c.IsActive);
            if (client == null)
            {
                ModelState.AddModelError("ClientId", "Selected client is not currently assigned to your trainer profile.");
                model.AvailableClients = GetAssignedClientsSelectList(trainerId, model.ClientId);
                model.ClientSnapshot = BuildClientSnapshot(model.ClientId);
                return View(model);
            }

            // Check if updating an existing draft or record
            TrainerFeedback entity = null;
            if (model.FeedbackId.HasValue && model.FeedbackId.Value > 0)
            {
                entity = db.TrainerFeedback.FirstOrDefault(f => f.FeedbackId == model.FeedbackId.Value && f.TrainerId == trainerId);
            }

            if (entity == null)
            {
                // Create new record
                entity = new TrainerFeedback
                {
                    TrainerId = trainerId,
                    ClientId = model.ClientId,
                    Category = model.Category,
                    Title = model.Title.Trim(),
                    FeedbackText = model.FeedbackText.Trim(),
                    Recommendations = !string.IsNullOrWhiteSpace(model.Recommendations) ? model.Recommendations.Trim() : null,
                    IsDraft = isDraft,
                    SentAt = DateTime.UtcNow,
                    ParentFeedbackId = model.ParentFeedbackId
                };
                db.TrainerFeedback.Add(entity);
            }
            else
            {
                // Update existing record without inserting duplicates
                entity.ClientId = model.ClientId;
                entity.Category = model.Category;
                entity.Title = model.Title.Trim();
                entity.FeedbackText = model.FeedbackText.Trim();
                entity.Recommendations = !string.IsNullOrWhiteSpace(model.Recommendations) ? model.Recommendations.Trim() : null;
                if (entity.IsDraft && !isDraft)
                {
                    entity.SentAt = DateTime.UtcNow;
                }
                entity.IsDraft = isDraft;
            }

            db.SaveChanges();

            if (isDraft)
            {
                TempData["SuccessMessage"] = $"Feedback draft saved for {client.FullName}. You can review and send it anytime.";
            }
            else
            {
                TempData["SuccessMessage"] = $"Feedback successfully saved for {client.FullName}!";
            }

            return RedirectToAction("History", new { clientId = model.ClientId });
        }

        // ====================================================================
        // GET: /Trainer/Feedback/Details/{id} (FR-T-44)
        // View single feedback entry with full details and audit info
        // ====================================================================
        [HttpGet]
        public ActionResult Details(int id)
        {
            string trainerId = GetCurrentTrainerId();
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var f = db.TrainerFeedback
                .Include(fb => fb.Clients)
                .Include(fb => fb.TrainerFeedback2) // Parent
                .Include(fb => fb.TrainerFeedback1) // Children (follow-ups)
                .FirstOrDefault(fb => fb.FeedbackId == id && fb.TrainerId == trainerId);

            if (f == null)
            {
                TempData["ErrorMessage"] = "Feedback record not found or you are not authorized to view it.";
                return RedirectToAction("History");
            }

            if (!AuthorizationHelper.IsActiveTrainerClientAssignment(db, trainerId, f.ClientId))
            {
                TempData["ErrorMessage"] = "Access restricted: The client for this feedback is not currently assigned to your trainer profile.";
                return RedirectToAction("History");
            }

            var item = new FeedbackHistoryItemViewModel
            {
                FeedbackId = f.FeedbackId,
                TrainerId = f.TrainerId,
                ClientId = f.ClientId,
                ClientName = f.Clients != null ? f.Clients.FullName : "Client",
                ClientEmail = f.Clients != null ? f.Clients.Email : "",
                Category = f.Category,
                Title = f.Title,
                FeedbackText = f.FeedbackText,
                Recommendations = f.Recommendations,
                IsDraft = f.IsDraft,
                SentAt = f.SentAt,
                ParentFeedbackId = f.ParentFeedbackId,
                ParentFeedbackTitle = f.TrainerFeedback2 != null ? f.TrainerFeedback2.Title : null,
                FollowUpCount = f.TrainerFeedback1 != null ? f.TrainerFeedback1.Count : 0
            };

            ViewBag.ClientSnapshot = BuildClientSnapshot(f.ClientId);

            return View(item);
        }

        // ====================================================================
        // GET: /Trainer/Feedback/Edit/{id} (FR-T-44)
        // Open existing feedback record for editing
        // ====================================================================
        [HttpGet]
        public ActionResult Edit(int id)
        {
            string trainerId = GetCurrentTrainerId();
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var feedback = db.TrainerFeedback
                .Include(f => f.Clients)
                .Include(f => f.TrainerFeedback2)
                .FirstOrDefault(f => f.FeedbackId == id);

            if (feedback == null)
            {
                TempData["ErrorMessage"] = "Feedback record not found.";
                return RedirectToAction("History");
            }

            // FR-T-44: Feedback Ownership Check
            if (feedback.TrainerId != trainerId)
            {
                TempData["ErrorMessage"] = "Access Denied: You can only edit feedback you originally submitted.";
                return RedirectToAction("History");
            }

            // FR-T-44: Ensure feedback belongs to an appropriate Trainer-Client relationship
            var client = db.Clients.FirstOrDefault(c => c.ClientId == feedback.ClientId && c.AssignedTrainerId == trainerId && c.IsActive);
            if (client == null)
            {
                TempData["ErrorMessage"] = "Access Denied: The client for this feedback is not currently assigned to your trainer profile.";
                return RedirectToAction("History");
            }

            var viewModel = new EditFeedbackViewModel
            {
                FeedbackId = feedback.FeedbackId,
                ClientId = feedback.ClientId,
                ClientName = feedback.Clients != null ? feedback.Clients.FullName : client.FullName,
                ClientEmail = feedback.Clients != null ? feedback.Clients.Email : client.Email,
                Category = feedback.Category,
                Title = feedback.Title,
                FeedbackText = feedback.FeedbackText,
                Recommendations = feedback.Recommendations,
                IsDraft = feedback.IsDraft,
                SentAt = feedback.SentAt,
                ParentFeedbackId = feedback.ParentFeedbackId,
                ParentFeedbackTitle = feedback.TrainerFeedback2 != null ? feedback.TrainerFeedback2.Title : null,
                ClientSnapshot = BuildClientSnapshot(feedback.ClientId)
            };

            return View(viewModel);
        }

        // ====================================================================
        // POST: /Trainer/Feedback/Edit (FR-T-44)
        // Update existing feedback record without creating duplicates
        // ====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(EditFeedbackViewModel model)
        {
            string trainerId = GetCurrentTrainerId();
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            // Validation: Feedback text must not be empty or whitespace-only
            if (string.IsNullOrWhiteSpace(model.FeedbackText))
            {
                ModelState.AddModelError("FeedbackText", "Feedback observations cannot be empty or contain only whitespace.");
            }

            if (string.IsNullOrWhiteSpace(model.Title))
            {
                ModelState.AddModelError("Title", "Please provide a feedback title or subject.");
            }
            else if (model.Title.Trim().Length > 200)
            {
                ModelState.AddModelError("Title", "Title cannot exceed 200 characters.");
            }

            if (!ModelState.IsValid)
            {
                model.ClientSnapshot = BuildClientSnapshot(model.ClientId);
                return View(model);
            }

            var entity = db.TrainerFeedback
                .Include(f => f.Clients)
                .FirstOrDefault(f => f.FeedbackId == model.FeedbackId);

            if (entity == null)
            {
                TempData["ErrorMessage"] = "Feedback record not found.";
                return RedirectToAction("History");
            }

            // FR-T-44: Enforce ownership on the server
            if (entity.TrainerId != trainerId)
            {
                TempData["ErrorMessage"] = "Access Denied: You can only edit feedback you originally submitted.";
                return RedirectToAction("History");
            }

            // FR-T-44: Enforce Trainer-Client relationship
            if (entity.Clients == null || entity.Clients.AssignedTrainerId != trainerId || !entity.Clients.IsActive)
            {
                TempData["ErrorMessage"] = "Access Denied: The client for this feedback is not currently assigned to your trainer profile.";
                return RedirectToAction("History");
            }

            try
            {
                // Update existing record fields
                entity.Title = model.Title.Trim();
                entity.FeedbackText = model.FeedbackText.Trim();
                entity.Recommendations = !string.IsNullOrWhiteSpace(model.Recommendations) ? model.Recommendations.Trim() : null;
                if (!string.IsNullOrWhiteSpace(model.Category))
                {
                    entity.Category = model.Category;
                }

                // Preserve original feedback creation date (SentAt remains untouched)
                // No duplicate record is inserted

                db.SaveChanges();

                TempData["SuccessMessage"] = $"Feedback for {entity.Clients.FullName} has been updated successfully!";
                return RedirectToAction("History", new { clientId = entity.ClientId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error updating feedback: " + ex.Message);
                model.ClientSnapshot = BuildClientSnapshot(model.ClientId);
                return View(model);
            }
        }

        // ====================================================================
        // AJAX: GET /Trainer/Feedback/GetClientSnapshot?clientId={clientId}
        // Dynamic client activity snapshot for side panel
        // ====================================================================
        [HttpGet]
        public JsonResult GetClientSnapshot(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return Json(new { success = false, message = "No client selected" }, JsonRequestBehavior.AllowGet);
            }

            var snapshot = BuildClientSnapshot(clientId);
            if (snapshot == null)
            {
                return Json(new { success = false, message = "Client not found" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                success = true,
                clientId = snapshot.ClientId,
                fullName = snapshot.FullName,
                email = snapshot.Email,
                primaryGoal = snapshot.PrimaryGoal,
                currentWeight = snapshot.CurrentWeightKg.HasValue ? snapshot.CurrentWeightKg.Value.ToString("0.0") + " kg" : "—",
                currentBmi = snapshot.CurrentBmi.HasValue ? snapshot.CurrentBmi.Value.ToString("0.0") : "—",
                bmiCategory = snapshot.BmiCategory,
                activeWorkout = snapshot.ActiveWorkoutPlan,
                activeDiet = snapshot.ActiveDietPlan,
                lastWorkout = snapshot.LastWorkoutSummary,
                loggedMeals = snapshot.LoggedMealsCount
            }, JsonRequestBehavior.AllowGet);
        }

        // ====================================================================
        // POST: /Trainer/Feedback/DiscardDraft/5
        // Deletes an unsent draft (sent feedback cannot be deleted)
        // ====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DiscardDraft(int id)
        {
            string trainerId = GetCurrentTrainerId();
            var draft = db.TrainerFeedback.FirstOrDefault(f => f.FeedbackId == id && f.TrainerId == trainerId);

            if (draft != null && draft.IsDraft)
            {
                db.TrainerFeedback.Remove(draft);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Draft discarded successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Cannot delete: Only unsent drafts can be discarded.";
            }

            return RedirectToAction("History");
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
