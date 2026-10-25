using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    // Client snapshot preview shown alongside the feedback form
    public class ClientFeedbackSnapshotDto
    {
        public string ClientId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public decimal? CurrentWeightKg { get; set; }
        public decimal? CurrentBmi { get; set; }
        public string BmiCategory { get; set; }
        public string PrimaryGoal { get; set; }
        public string ActiveWorkoutPlan { get; set; }
        public string ActiveDietPlan { get; set; }
        public string LastWorkoutSummary { get; set; }
        public int LoggedMealsCount { get; set; }
    }

    // Page 1: Send Feedback ViewModel
    public class SendFeedbackViewModel
    {
        public int? FeedbackId { get; set; }

        [Required(ErrorMessage = "Please select an assigned client.")]
        [Display(Name = "Assigned Client")]
        public string ClientId { get; set; }

        public List<SelectListItem> AvailableClients { get; set; } = new List<SelectListItem>();

        [Required(ErrorMessage = "Please choose a feedback category.")]
        [Display(Name = "Feedback Category")]
        public string Category { get; set; } = "Workout"; // Workout, Diet, General Progress

        [Required(ErrorMessage = "Please provide a subject or headline.")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        [Display(Name = "Feedback Title / Subject")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Please enter your feedback and observations.")]
        [Display(Name = "Feedback & Observations")]
        public string FeedbackText { get; set; }

        [Display(Name = "Actionable Recommendations & Next Steps")]
        public string Recommendations { get; set; }

        public bool IsDraft { get; set; }

        // Follow-up context
        public int? ParentFeedbackId { get; set; }
        public string ParentFeedbackTitle { get; set; }
        public DateTime? ParentFeedbackDate { get; set; }

        // Live client snapshot stats
        public ClientFeedbackSnapshotDto ClientSnapshot { get; set; }
    }

    // Individual item for Page 2: Feedback History
    public class FeedbackHistoryItemViewModel
    {
        public int FeedbackId { get; set; }
        public string TrainerId { get; set; }
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string ClientEmail { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string FeedbackText { get; set; }
        public string Recommendations { get; set; }
        public bool IsDraft { get; set; }
        public DateTime SentAt { get; set; }
        public int? ParentFeedbackId { get; set; }
        public string ParentFeedbackTitle { get; set; }
        public int FollowUpCount { get; set; }
    }

    // Page 2: Feedback History ViewModel
    public class FeedbackHistoryViewModel
    {
        public string SelectedClientId { get; set; }
        public string SelectedCategory { get; set; }
        public string SearchTerm { get; set; }

        public List<SelectListItem> AvailableClients { get; set; } = new List<SelectListItem>();
        public List<FeedbackHistoryItemViewModel> FeedbackList { get; set; } = new List<FeedbackHistoryItemViewModel>();

        public int TotalSentCount { get; set; }
        public int TotalDraftsCount { get; set; }
    }

    // FR-T-44: Edit Existing Feedback ViewModel
    public class EditFeedbackViewModel
    {
        [Required]
        public int FeedbackId { get; set; }

        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string ClientEmail { get; set; }

        [Required(ErrorMessage = "Please choose a feedback category.")]
        [Display(Name = "Feedback Category")]
        public string Category { get; set; } = "Workout";

        [Required(ErrorMessage = "Please provide a subject or headline.")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        [Display(Name = "Feedback Title / Subject")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Please enter your feedback and observations.")]
        [Display(Name = "Feedback & Observations")]
        public string FeedbackText { get; set; }

        [Display(Name = "Actionable Recommendations & Next Steps")]
        public string Recommendations { get; set; }

        public bool IsDraft { get; set; }

        [Display(Name = "Submitted Date")]
        public DateTime SentAt { get; set; }

        // Follow-up context
        public int? ParentFeedbackId { get; set; }
        public string ParentFeedbackTitle { get; set; }

        // Live client snapshot stats
        public ClientFeedbackSnapshotDto ClientSnapshot { get; set; }
    }
}
