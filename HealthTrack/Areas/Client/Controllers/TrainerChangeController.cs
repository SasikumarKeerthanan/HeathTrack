using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Client.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Client.Controllers
{
    /// Controller for Client Trainer-Change requests.
    [ClientAuthorize]
    public class TrainerChangeController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // GET: /Client/TrainerChange
        [HttpGet]
        public ActionResult Index()
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            var model = BuildViewModel(clientId);
            return View(model);
        }

        // POST: /Client/TrainerChange/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Submit(SubmitTrainerChangeViewModel form)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            // Guard: Disallow multiple pending requests simultaneously
            bool hasPending = db.TrainerChangeRequests.Any(r => r.ClientId == clientId && r.Status == "Pending");
            if (hasPending)
            {
                TempData["ErrorMessage"] = "You already have a pending trainer-change request under review. You cannot submit another until it is resolved.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                var model = BuildViewModel(clientId);
                model.Form = form;
                return View("Index", model);
            }

            try
            {
                var client = db.Clients.FirstOrDefault(c => c.ClientId == clientId);
                if (client == null)
                {
                    return RedirectToAction("Login", "Account", new { area = "Client" });
                }

                if (string.IsNullOrEmpty(client.AssignedTrainerId))
                {
                    TempData["ErrorMessage"] = "You do not currently have an assigned coach to change.";
                    return RedirectToAction("Index");
                }

                var request = new TrainerChangeRequests
                {
                    ClientId = clientId,
                    CurrentTrainerId = client.AssignedTrainerId,
                    ReasonCategory = form.ReasonCategory.Trim(),
                    ReasonDetails = form.ReasonDetails.Trim(),
                    PreferredGender = form.PreferredGender,
                    Status = "Pending",
                    RequestedAt = DateTime.UtcNow
                };

                db.TrainerChangeRequests.Add(request);

                // Audit Log
                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = clientId,
                    UserRole = "Client",
                    EventType = "TRAINER_CHANGE_REQUESTED",
                    Details = $"Client {client.Email} requested coach change (Category: {form.ReasonCategory}).",
                    TimestampUtc = DateTime.UtcNow
                });

                db.SaveChanges();
                TempData["SuccessMessage"] = "Your coach change request has been submitted successfully and is now under review by our admin team!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to submit change request: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        private ClientTrainerChangePageViewModel BuildViewModel(string clientId)
        {
            var client = db.Clients
                .Include(c => c.Trainers)
                .FirstOrDefault(c => c.ClientId == clientId);

            var allRequests = db.TrainerChangeRequests
                .Where(r => r.ClientId == clientId)
                .OrderByDescending(r => r.RequestedAt)
                .ToList();

            var pending = allRequests.FirstOrDefault(r => r.Status == "Pending");

            // Collect all trainer IDs to load their full names for clean display
            var trainerIds = allRequests
                .SelectMany(r => new[] { r.CurrentTrainerId, r.NewTrainerId })
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            if (client?.AssignedTrainerId != null && !trainerIds.Contains(client.AssignedTrainerId))
            {
                trainerIds.Add(client.AssignedTrainerId);
            }

            var trainerDict = db.Trainers
                .Where(t => trainerIds.Contains(t.TrainerId))
                .ToDictionary(t => t.TrainerId, t => t.FullName);

            return new ClientTrainerChangePageViewModel
            {
                Client = client,
                CurrentTrainer = client?.Trainers,
                HasPendingRequest = pending != null,
                PendingRequest = pending,
                PastRequests = allRequests,
                TrainerNames = trainerDict,
                Form = new SubmitTrainerChangeViewModel()
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
