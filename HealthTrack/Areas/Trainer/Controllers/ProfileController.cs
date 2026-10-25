using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Trainer.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.Controllers
{
    /// Controller for Trainer Profile management.
    [TrainerAuthorize]
    public class ProfileController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // GET: /Trainer/Profile
        [HttpGet]
        public ActionResult Index()
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var trainer = db.Trainers.FirstOrDefault(t => t.TrainerId == trainerId);
            if (trainer == null)
            {
                Session.Clear();
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            // Sync session variables
            Session["CurrentTrainerId"] = trainer.TrainerId;
            Session["CurrentTrainerName"] = trainer.FullName;
            Session["CurrentTrainerEmail"] = trainer.Email;
            Session["CurrentTrainerAvatar"] = trainer.ProfilePicturePath;
            Session["Role"] = "Trainer";

            int activeClients = db.Clients.Count(c => c.AssignedTrainerId == trainerId && c.IsActive);

            var model = new TrainerProfileViewModel
            {
                Trainer = trainer,
                ActiveClientsCount = activeClients,
                MaxClientCapacity = trainer.MaxClientCapacity,

                // Pre-populate Form Fields strictly from Database
                FullName = trainer.FullName,
                Email = trainer.Email,
                PhoneNumber = trainer.PhoneNumber,
                Gender = trainer.Gender,
                PrimarySpecialization = trainer.PrimarySpecialization ?? trainer.Specialization,
                ExperienceYears = trainer.ExperienceYears,
                Bio = trainer.Bio
            };

            return View(model);
        }

        // POST: /Trainer/Profile/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Update(TrainerProfileViewModel model)
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var trainer = db.Trainers.FirstOrDefault(t => t.TrainerId == trainerId);
            if (trainer == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            if (!ModelState.IsValid)
            {
                model.Trainer = trainer;
                model.ActiveClientsCount = db.Clients.Count(c => c.AssignedTrainerId == trainerId && c.IsActive);
                model.MaxClientCapacity = trainer.MaxClientCapacity;
                return View("Index", model);
            }

            try
            {
                // 1. Update Personal & Professional Information
                trainer.FullName = model.FullName.Trim();
                trainer.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
                trainer.Gender = model.Gender;
                trainer.PrimarySpecialization = model.PrimarySpecialization?.Trim();
                trainer.Specialization = model.PrimarySpecialization?.Trim();
                trainer.ExperienceYears = model.ExperienceYears;
                trainer.Bio = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim();

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

                        string uniqueFileName = $"{trainer.TrainerId}_{Guid.NewGuid().ToString("N").Substring(0, 8)}{ext}";
                        string filePath = Path.Combine(uploadFolder, uniqueFileName);
                        model.ProfileImage.SaveAs(filePath);

                        trainer.ProfilePicturePath = $"/Uploads/ProfilePictures/{uniqueFileName}";
                        Session["CurrentTrainerAvatar"] = trainer.ProfilePicturePath;
                    }
                }

                // 3. Save changes & update Session
                db.SaveChanges();
                Session["CurrentTrainerName"] = trainer.FullName;

                TempData["SuccessMessage"] = "Trainer profile updated successfully!";
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
