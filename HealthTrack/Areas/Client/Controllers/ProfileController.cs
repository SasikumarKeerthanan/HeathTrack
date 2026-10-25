using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Client.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Client.Controllers
{
    /// Controller for Client Profile management.
    [ClientAuthorize]
    public class ProfileController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // GET: /Client/Profile
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
                Session.Clear();
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            // Sync session variables
            Session["CurrentClientId"] = client.ClientId;
            Session["CurrentClientName"] = client.FullName;
            Session["CurrentClientEmail"] = client.Email;
            Session["CurrentClientAvatar"] = client.ProfilePicturePath;
            Session["Role"] = "Client";

            var trainer = !string.IsNullOrEmpty(client.AssignedTrainerId)
                ? db.Trainers.FirstOrDefault(t => t.TrainerId == client.AssignedTrainerId)
                : null;

            var preferences = db.ClientPreferences.FirstOrDefault(p => p.ClientId == clientId);
            
            // Fetch real weight strictly from database (ClientProgressRecords or ClientGoals)
            var latestProgress = db.ClientProgressRecords
                .Where(p => p.ClientId == clientId)
                .OrderByDescending(p => p.RecordedDate)
                .FirstOrDefault();

            decimal? currentWeight = null;
            if (latestProgress != null && latestProgress.WeightKg > 0)
            {
                currentWeight = latestProgress.WeightKg;
            }
            else
            {
                var weightGoal = db.ClientGoals.FirstOrDefault(g => g.ClientId == clientId && (g.Category == "Weight" || g.GoalName.Contains("Weight")));
                if (weightGoal != null && weightGoal.CurrentValue > 0)
                {
                    currentWeight = weightGoal.CurrentValue;
                }
                else if (weightGoal != null && weightGoal.BaselineValue > 0)
                {
                    currentWeight = weightGoal.BaselineValue;
                }
            }

            // Fetch real height strictly from database (Clients.HeightCm or ClientHealthProfile.BaselineHeightCm)
            decimal? height = client.HeightCm;
            if (!height.HasValue || height.Value <= 0)
            {
                var healthProfile = db.ClientHealthProfile.FirstOrDefault(h => h.ClientId == clientId);
                if (healthProfile != null && healthProfile.BaselineHeightCm > 0)
                {
                    height = healthProfile.BaselineHeightCm;
                }
            }

            // Calculate BMI ONLY if real height and real weight actually exist in the database
            decimal? bmi = null;
            string bmiCategory = null;
            if (height.HasValue && height.Value > 0 && currentWeight.HasValue && currentWeight.Value > 0)
            {
                decimal heightMeters = height.Value / 100.0m;
                bmi = Math.Round(currentWeight.Value / (heightMeters * heightMeters), 1);
                if (bmi < 18.5m) bmiCategory = "Underweight";
                else if (bmi < 25.0m) bmiCategory = "Normal weight";
                else if (bmi < 30.0m) bmiCategory = "Overweight";
                else bmiCategory = "Obesity";
            }

            var model = new ClientProfileViewModel
            {
                Client = client,
                AssignedTrainer = trainer,
                CurrentWeightKg = currentWeight,
                Bmi = bmi,
                BmiCategory = bmiCategory,
                PrimaryGoal = preferences?.PrimaryGoal,

                // Edit Form fields
                FullName = client.FullName,
                Email = client.Email,
                PhoneNumber = client.PhoneNumber,
                Gender = client.Gender,
                DateOfBirth = client.DateOfBirth,
                HeightCm = client.HeightCm
            };

            // FR-S-20: Audit Client health profile view
            HealthAuditHelper.LogAccess(Session, clientId, "HealthProfile", null, "View", true, "Client viewed profile and health details");

            return View(model);
        }

        // POST: /Client/Profile/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Update(ClientProfileViewModel model)
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

            if (!ModelState.IsValid)
            {
                // Reload display data if validation fails
                model.Client = client;
                model.AssignedTrainer = !string.IsNullOrEmpty(client.AssignedTrainerId)
                    ? db.Trainers.FirstOrDefault(t => t.TrainerId == client.AssignedTrainerId)
                    : null;
                return View("Index", model);
            }

            try
            {
                // 1. Update Personal & Contact Information
                client.FullName = model.FullName.Trim();
                client.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
                client.Gender = model.Gender;
                client.DateOfBirth = model.DateOfBirth;
                client.HeightCm = model.HeightCm;

                // Sync HealthProfile baseline height if record exists
                var healthProfile = db.ClientHealthProfile.FirstOrDefault(h => h.ClientId == clientId);
                if (healthProfile != null && model.HeightCm.HasValue)
                {
                    healthProfile.BaselineHeightCm = model.HeightCm.Value;
                    healthProfile.UpdatedAt = DateTime.UtcNow;
                }

                // 2. Handle Optional Profile Picture Upload
                if (model.ProfileImage != null && model.ProfileImage.ContentLength > 0)
                {
                    string ext = Path.GetExtension(model.ProfileImage.FileName).ToLower();
                    string[] allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    if (allowed.Contains(ext))
                    {
                        string uploadFolder = Server.MapPath("~/Uploads/ProfilePictures/");
                        if (!Directory.Exists(uploadFolder))
                        {
                            Directory.CreateDirectory(uploadFolder);
                        }

                        string uniqueFileName = $"{client.ClientId}_{Guid.NewGuid().ToString("N").Substring(0, 8)}{ext}";
                        string filePath = Path.Combine(uploadFolder, uniqueFileName);
                        model.ProfileImage.SaveAs(filePath);

                        client.ProfilePicturePath = $"/Uploads/ProfilePictures/{uniqueFileName}";
                        Session["CurrentClientAvatar"] = client.ProfilePicturePath;
                    }
                }

                // 3. Save changes & update Session
                db.SaveChanges();
                Session["CurrentClientName"] = client.FullName;

                // FR-S-20: Audit health profile update
                HealthAuditHelper.LogAccess(Session, clientId, "HealthProfile", null, "Update", true, "Client updated personal and baseline health profile");

                TempData["SuccessMessage"] = "Your profile information has been updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to update profile: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
