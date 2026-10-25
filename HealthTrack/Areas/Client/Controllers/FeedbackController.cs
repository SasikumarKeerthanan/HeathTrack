using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Client.ViewModels;
using HealthTrack.Models;
using HealthTrack.Helpers;

namespace HealthTrack.Areas.Client.Controllers
{
    // ========================================================================
    // FeedbackController: Client-Side Trainer Feedback & Guidance Hub
    // Allows clients/members to view personalized coach feedback, advice,
    // recommendations, and full feedback history in an intuitive timeline.
    // ========================================================================
    [ClientAuthorize]
    public class FeedbackController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // --------------------------------------------------------------------
        // Helper: Retrieve active logged-in client strictly from Session
        // --------------------------------------------------------------------
        private Clients GetCurrentClient()
        {
            var sessionClientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrWhiteSpace(sessionClientId))
            {
                return null;
            }

            var sessionClient = db.Clients
                .Include(c => c.Trainers)
                .FirstOrDefault(c => c.ClientId == sessionClientId);

            return sessionClient;
        }

        // ====================================================================
        // GET: /Client/Feedback or /Client/Feedback/Index
        // Main view for the logged-in client to view feedback from their assigned coach
        // ====================================================================
        [HttpGet]
        public ActionResult Index(string category = "all", string search = null)
        {
            var client = GetCurrentClient();
            if (client == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            // Store in ViewBag for layout header
            ViewBag.ClientName = client.FullName;
            ViewBag.ClientEmail = client.Email;

            // Load assigned trainer info
            var trainer = client.Trainers;
            var trainerDto = new AssignedTrainerSummaryDto
            {
                TrainerId = trainer != null ? trainer.TrainerId : (client.AssignedTrainerId ?? string.Empty),
                FullName = trainer != null ? trainer.FullName : "Not Assigned",
                Email = trainer != null ? trainer.Email : string.Empty,
                Specialization = trainer != null ? (trainer.PrimarySpecialization ?? trainer.Specialization ?? "Coach") : "General Fitness"
            };

            // Query strictly for this client and published by their assigned trainer
            var baseQuery = db.TrainerFeedback
                .Include(f => f.Trainers)
                .Where(f => f.ClientId == client.ClientId && !f.IsDraft);

            if (!string.IsNullOrWhiteSpace(client.AssignedTrainerId))
            {
                baseQuery = baseQuery.Where(f => f.TrainerId == client.AssignedTrainerId);
            }

            var allClientFeedback = baseQuery
                .OrderByDescending(f => f.SentAt)
                .ToList();

            // Compute KPI counts across all categories
            int totalCount = allClientFeedback.Count;
            int workoutCount = allClientFeedback.Count(f => string.Equals(f.Category, "Workout", StringComparison.OrdinalIgnoreCase));
            int dietCount = allClientFeedback.Count(f => string.Equals(f.Category, "Diet", StringComparison.OrdinalIgnoreCase));
            int generalCount = allClientFeedback.Count(f => string.Equals(f.Category, "General Progress", StringComparison.OrdinalIgnoreCase) || string.Equals(f.Category, "General", StringComparison.OrdinalIgnoreCase));

            // Determine latest feedback highlight (unfiltered)
            var latestEntity = allClientFeedback.FirstOrDefault();
            ClientFeedbackItemDto latestDto = null;
            if (latestEntity != null)
            {
                latestDto = MapFeedbackEntityToDto(latestEntity);
            }

            // Apply category filter
            var filteredList = allClientFeedback.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filteredList = filteredList.Where(f => string.Equals(f.Category, category, StringComparison.OrdinalIgnoreCase));
            }

            // Apply search term filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                filteredList = filteredList.Where(f =>
                    (f.Title != null && f.Title.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (f.FeedbackText != null && f.FeedbackText.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (f.Recommendations != null && f.Recommendations.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (f.Category != null && f.Category.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                );
            }

            // Map filtered results to DTOs
            var historyItems = filteredList
                .Select(MapFeedbackEntityToDto)
                .ToList();

            var currentClientDto = new ClientSummaryDto
            {
                ClientId = client.ClientId,
                FullName = client.FullName,
                Email = client.Email,
                Gender = client.Gender,
                AssignedTrainerId = client.AssignedTrainerId,
                TrainerName = trainerDto.FullName
            };

            var viewModel = new ClientFeedbackDashboardViewModel
            {
                CurrentClient = currentClientDto,
                AssignedTrainer = trainerDto,
                SelectedCategory = category ?? "all",
                SearchTerm = search,
                TotalCount = totalCount,
                WorkoutCount = workoutCount,
                DietCount = dietCount,
                GeneralCount = generalCount,
                LatestFeedback = latestDto,
                FeedbackHistory = historyItems
            };

            return View(viewModel);
        }

        // --------------------------------------------------------------------
        // Helper: Convert EF TrainerFeedback entity to clean ViewModel DTO
        // --------------------------------------------------------------------
        private ClientFeedbackItemDto MapFeedbackEntityToDto(TrainerFeedback f)
        {
            var trainer = f.Trainers;
            return new ClientFeedbackItemDto
            {
                FeedbackId = f.FeedbackId,
                TrainerId = f.TrainerId,
                TrainerName = trainer != null ? trainer.FullName : "Coach",
                TrainerEmail = trainer != null ? trainer.Email : string.Empty,
                TrainerSpecialization = trainer != null ? (trainer.PrimarySpecialization ?? trainer.Specialization ?? "Coach") : "Fitness Coach",
                ClientId = f.ClientId,
                Category = f.Category,
                Title = f.Title,
                FeedbackText = f.FeedbackText,
                Recommendations = f.Recommendations,
                SentAt = f.SentAt,
                ParentFeedbackId = f.ParentFeedbackId
            };
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
