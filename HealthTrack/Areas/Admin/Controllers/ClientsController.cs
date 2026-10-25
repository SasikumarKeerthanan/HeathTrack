using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Admin.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class ClientsController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // =====================================================================
        // 1. LIST CLIENTS (GET: /Admin/Clients)
        // =====================================================================
        [HttpGet]
        public ActionResult Index(string search, string status)
        {
            var model = new AdminClientIndexViewModel
            {
                SearchQuery = search?.Trim(),
                StatusFilter = string.IsNullOrWhiteSpace(status) ? "All" : status.Trim()
            };

            // Summary counts before applying search filter
            model.TotalClientsCount = db.Clients.Count();
            model.ActiveClientsCount = db.Clients.Count(c => c.IsActive);
            model.InactiveClientsCount = db.Clients.Count(c => !c.IsActive);
            model.UnassignedClientsCount = db.Clients.Count(c => c.AssignedTrainerId == null || c.AssignedTrainerId == "");

            // Query clients with eager navigation
            IQueryable<Clients> query = db.Clients;

            // Apply Status Filter
            if (model.StatusFilter.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => c.IsActive);
            }
            else if (model.StatusFilter.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => !c.IsActive);
            }
            else if (model.StatusFilter.Equals("Unassigned", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => c.AssignedTrainerId == null || c.AssignedTrainerId == "");
            }

            // Apply Search Query
            if (!string.IsNullOrWhiteSpace(model.SearchQuery))
            {
                string term = model.SearchQuery.ToLower();
                query = query.Where(c => c.FullName.ToLower().Contains(term)
                                      || c.Email.ToLower().Contains(term)
                                      || c.ClientId.ToLower().Contains(term));
            }

            var clientsList = query
                .OrderByDescending(c => c.CreatedAt)
                .ToList();

            // Load trainer lookup in memory for fast resolution
            var trainerLookup = db.Trainers
                .Select(t => new { t.TrainerId, t.FullName })
                .ToDictionary(t => t.TrainerId, t => t.FullName);

            model.Clients = clientsList.Select(c =>
            {
                string trainerName = "Unassigned";
                if (!string.IsNullOrEmpty(c.AssignedTrainerId) && trainerLookup.ContainsKey(c.AssignedTrainerId))
                {
                    trainerName = trainerLookup[c.AssignedTrainerId];
                }

                return new AdminClientListItemViewModel
                {
                    ClientId = c.ClientId,
                    FullName = c.FullName,
                    Email = c.Email,
                    PhoneNumber = c.PhoneNumber,
                    Gender = c.Gender,
                    DateOfBirth = c.DateOfBirth,
                    AssignedTrainerId = c.AssignedTrainerId,
                    AssignedTrainerName = trainerName,
                    IsActive = c.IsActive,
                    FailedLoginAttempts = c.FailedLoginAttempts,
                    LockoutEndUtc = c.LockoutEndUtc,
                    LastLoginAt = c.LastLoginAt,
                    CreatedAt = c.CreatedAt,
                    ProfilePicturePath = c.ProfilePicturePath
                };
            }).ToList();

            return View(model);
        }

        // =====================================================================
        // 2. TOGGLE CLIENT STATUS (POST: /Admin/Clients/ToggleStatus)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleStatus(string clientId, string returnStatus = "All")
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                TempData["ErrorMessage"] = "Client ID is required.";
                return RedirectToAction("Index", new { status = returnStatus });
            }

            var client = db.Clients.FirstOrDefault(c => c.ClientId == clientId);
            if (client == null)
            {
                TempData["ErrorMessage"] = "Client not found.";
                return RedirectToAction("Index", new { status = returnStatus });
            }

            // Toggle state
            client.IsActive = !client.IsActive;

            // If reactivating account, clear any lockout state
            if (client.IsActive)
            {
                client.FailedLoginAttempts = 0;
                client.LockoutEndUtc = null;
            }

            // Record security audit log (FR-S-25, FR-S-26)
            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = "Admin",
                UserRole = "Admin",
                EventType = client.IsActive ? "CLIENT_ACTIVATED" : "CLIENT_DEACTIVATED",
                Details = $"Admin {(client.IsActive ? "activated" : "deactivated")} client account {client.ClientId} ({client.Email})",
                TimestampUtc = DateTime.UtcNow
            });

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Client '{client.FullName}' ({client.ClientId}) was successfully {(client.IsActive ? "activated" : "deactivated")}.";
            return RedirectToAction("Index", new { status = returnStatus });
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
