using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Admin.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Admin.Controllers
{
    /// Admin Controller for managing Trainers.
    [AdminAuthorize]
    public class TrainersController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // =====================================================================
        // 1. LIST TRAINERS (GET: /Admin/Trainers)
        // =====================================================================
        [HttpGet]
        public ActionResult Index()
        {
            var trainers = db.Trainers
                .OrderByDescending(t => t.CreatedAt)
                .ToList();

            var list = trainers.Select(t => new TrainerListItemViewModel
            {
                TrainerId = t.TrainerId,
                FullName = t.FullName,
                Email = t.Email,
                PhoneNumber = t.PhoneNumber,
                Gender = t.Gender,
                PrimarySpecialization = t.PrimarySpecialization ?? t.Specialization ?? "General Fitness",
                MaxClientCapacity = t.MaxClientCapacity > 0 ? t.MaxClientCapacity : 15,
                ActiveClientsCount = db.Clients.Count(c => c.AssignedTrainerId == t.TrainerId && c.IsActive),
                IsActive = t.IsActive,
                IsInvitationAccepted = t.IsInvitationAccepted,
                InvitationExpiresUtc = t.InvitationExpiresUtc,
                CreatedAt = t.CreatedAt
            }).ToList();

            return View(list);
        }

        // =====================================================================
        // 2. CREATE TRAINER (GET: /Admin/Trainers/Create)
        // =====================================================================
        [HttpGet]
        public ActionResult Create()
        {
            var model = new AddTrainerViewModel();
            return View(model);
        }

        // =====================================================================
        // 3. CREATE TRAINER (POST: /Admin/Trainers/Create)
        // Creates the trainer, generates a secure 48h invitation token,
        // and sends the initial password-reset invitation email.
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(AddTrainerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string normalizedEmail = model.Email.Trim().ToLower();

            // 1. Check if email already exists in Trainers or Clients
            if (db.Trainers.Any(t => t.Email.ToLower() == normalizedEmail))
            {
                ModelState.AddModelError("Email", "A trainer with this email address already exists.");
                return View(model);
            }

            if (db.Clients.Any(c => c.Email.ToLower() == normalizedEmail))
            {
                ModelState.AddModelError("Email", "This email address is already registered to a Client account.");
                return View(model);
            }

            try
            {
                // 2. Generate Next Trainer ID (e.g. TR-001, TR-002)
                int count = db.Trainers.Count();
                string newTrainerId = $"TR-{(count + 1):D3}";
                while (db.Trainers.Any(t => t.TrainerId == newTrainerId))
                {
                    count++;
                    newTrainerId = $"TR-{(count + 1):D3}";
                }

                // 3. Generate Secure 48-Hour Invitation Token
                string invitationToken = Guid.NewGuid().ToString("N");

                // 4. Construct Trainer Entity
                var trainer = new Trainers
                {
                    TrainerId = newTrainerId,
                    FullName = model.FullName.Trim(),
                    Email = normalizedEmail,
                    PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                    Gender = model.Gender, // Strictly 'Male' or 'Female'
                    PrimarySpecialization = model.PrimarySpecialization,
                    Specialization = model.PrimarySpecialization, // Keeps legacy column synced
                    SupportedWorkoutTypes = (model.SelectedWorkoutTypes != null && model.SelectedWorkoutTypes.Any())
                        ? string.Join(", ", model.SelectedWorkoutTypes)
                        : null,
                    SupportedFitnessLevels = (model.SelectedFitnessLevels != null && model.SelectedFitnessLevels.Any())
                        ? string.Join(", ", model.SelectedFitnessLevels)
                        : null,
                    SupportedDietTypes = (model.SelectedDietTypes != null && model.SelectedDietTypes.Any())
                        ? string.Join(", ", model.SelectedDietTypes)
                        : null,
                    ExperienceYears = model.ExperienceYears,
                    MaxClientCapacity = model.MaxClientCapacity > 0 ? model.MaxClientCapacity : 15,
                    Bio = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim(),
                    Role = "Trainer",
                    IsActive = true,
                    Password = null, // Salted & hashed password will be created by the trainer upon invitation setup
                    InvitationToken = invitationToken,
                    InvitationExpiresUtc = DateTime.UtcNow.AddHours(48),
                    IsInvitationAccepted = false,
                    FailedLoginAttempts = 0,
                    CreatedAt = DateTime.UtcNow
                };

                db.Trainers.Add(trainer);

                // 5. Audit Log Event
                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = "Admin",
                    UserRole = "Admin",
                    EventType = "TRAINER_CREATED",
                    Details = $"Admin created trainer {trainer.TrainerId} ({trainer.Email}) and sent password-reset invitation",
                    TimestampUtc = DateTime.UtcNow
                });

                db.SaveChanges();

                // 6. Build the Invitation URL
                string invitationLink = Url.Action("AcceptInvitation", "Account", new
                {
                    area = "Trainer",
                    email = trainer.Email,
                    token = trainer.InvitationToken
                }, Request.Url.Scheme);

                // 7. Dispatch Invitation Email
                string smtpError;
                bool emailSent = EmailHelper.SendTrainerInvitationEmail(trainer.Email, trainer.FullName, invitationLink, out smtpError);

                if (emailSent)
                {
                    TempData["SuccessMessage"] = $"Trainer '{trainer.FullName}' ({trainer.TrainerId}) was created successfully! An initial password-reset invitation email has been sent to {trainer.Email}.";
                }
                else
                {
                    TempData["WarningMessage"] = $"Trainer '{trainer.FullName}' was created in the database, but email dispatch failed (Note: {smtpError}). Direct invitation link: {invitationLink}";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating trainer: " + ex.Message);
                return View(model);
            }
        }

        // =====================================================================
        // 4. RESEND INVITATION (POST: /Admin/Trainers/ResendInvitation)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResendInvitation(string trainerId)
        {
            if (string.IsNullOrWhiteSpace(trainerId))
            {
                TempData["ErrorMessage"] = "Trainer ID is required.";
                return RedirectToAction("Index");
            }

            var trainer = db.Trainers.FirstOrDefault(t => t.TrainerId == trainerId);
            if (trainer == null)
            {
                TempData["ErrorMessage"] = "Trainer not found.";
                return RedirectToAction("Index");
            }

            if (trainer.IsInvitationAccepted)
            {
                TempData["InfoMessage"] = $"Trainer {trainer.FullName} has already activated their account.";
                return RedirectToAction("Index");
            }

            // Regenerate token and set 48h expiry
            trainer.InvitationToken = Guid.NewGuid().ToString("N");
            trainer.InvitationExpiresUtc = DateTime.UtcNow.AddHours(48);

            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = "Admin",
                UserRole = "Admin",
                EventType = "TRAINER_INVITATION_RESENT",
                Details = $"Admin resent invitation to trainer {trainer.TrainerId} ({trainer.Email})",
                TimestampUtc = DateTime.UtcNow
            });
            db.SaveChanges();

            string invitationLink = Url.Action("AcceptInvitation", "Account", new
            {
                area = "Trainer",
                email = trainer.Email,
                token = trainer.InvitationToken
            }, Request.Url.Scheme);

            string smtpError;
            bool emailSent = EmailHelper.SendTrainerInvitationEmail(trainer.Email, trainer.FullName, invitationLink, out smtpError);

            if (emailSent)
            {
                TempData["SuccessMessage"] = $"A new invitation email has been successfully sent to {trainer.Email}!";
            }
            else
            {
                TempData["WarningMessage"] = $"New invitation token generated, but email could not be sent ({smtpError}). Link: {invitationLink}";
            }

            return RedirectToAction("Index");
        }

        // =====================================================================
        // 5. TOGGLE TRAINER STATUS (POST: /Admin/Trainers/ToggleStatus)
        // FR-A-18, FR-A-19, FR-S-06, FR-S-25
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleStatus(string trainerId)
        {
            if (string.IsNullOrWhiteSpace(trainerId))
            {
                TempData["ErrorMessage"] = "Trainer ID is required.";
                return RedirectToAction("Index");
            }

            var trainer = db.Trainers.FirstOrDefault(t => t.TrainerId == trainerId);
            if (trainer == null)
            {
                TempData["ErrorMessage"] = "Trainer not found.";
                return RedirectToAction("Index");
            }

            // Toggle state
            trainer.IsActive = !trainer.IsActive;

            // If reactivating account, clear lockout state
            if (trainer.IsActive)
            {
                trainer.FailedLoginAttempts = 0;
                trainer.LockoutEndUtc = null;
            }

            // Record security audit log (FR-S-25)
            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = "Admin",
                UserRole = "Admin",
                EventType = trainer.IsActive ? "TRAINER_ACTIVATED" : "TRAINER_DEACTIVATED",
                Details = $"Admin {(trainer.IsActive ? "activated" : "deactivated")} trainer account {trainer.TrainerId} ({trainer.Email})",
                TimestampUtc = DateTime.UtcNow
            });

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Trainer '{trainer.FullName}' ({trainer.TrainerId}) was successfully {(trainer.IsActive ? "activated" : "deactivated")}.";
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
