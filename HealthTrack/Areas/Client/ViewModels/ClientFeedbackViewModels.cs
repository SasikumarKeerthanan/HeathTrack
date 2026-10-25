using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthTrack.Areas.Client.ViewModels
{
    // ========================================================================
    // Client Feedback Item DTO: Clean, beginner-friendly representation of a feedback entry
    // ========================================================================
    public class ClientFeedbackItemDto
    {
        public int FeedbackId { get; set; }
        public string TrainerId { get; set; }
        public string TrainerName { get; set; }
        public string TrainerEmail { get; set; }
        public string TrainerSpecialization { get; set; }
        public string ClientId { get; set; }
        public string Category { get; set; } // "Workout", "Diet", "General Progress"
        public string Title { get; set; }
        public string FeedbackText { get; set; }
        public string Recommendations { get; set; }
        public DateTime SentAt { get; set; }
        public int? ParentFeedbackId { get; set; }

        // Helper: Formatted display dates
        public string SentDateFormatted
        {
            get { return SentAt.ToString("MMM dd, yyyy"); }
        }

        public string SentTimeFormatted
        {
            get { return SentAt.ToString("hh:mm tt"); }
        }

        // Helper: Friendly relative time
        public string RelativeTime
        {
            get
            {
                var span = DateTime.Now - SentAt;
                if (span.TotalDays < 1 && span.TotalHours < 24 && DateTime.Today == SentAt.Date)
                    return "Today";
                if (span.TotalDays < 2 && DateTime.Today.AddDays(-1) == SentAt.Date)
                    return "Yesterday";
                if (span.TotalDays < 7)
                    return $"{(int)Math.Max(1, span.TotalDays)} days ago";
                if (span.TotalDays < 30)
                    return $"{(int)(span.TotalDays / 7)} weeks ago";
                return SentDateFormatted;
            }
        }

        // Helper: UI Category Badge Class
        public string CategoryBadgeClass
        {
            get
            {
                if (string.Equals(Category, "Workout", StringComparison.OrdinalIgnoreCase))
                    return "bg-primary-subtle text-primary border border-primary-subtle";
                if (string.Equals(Category, "Diet", StringComparison.OrdinalIgnoreCase))
                    return "bg-success-subtle text-success border border-success-subtle";
                return "bg-info-subtle text-info border border-info-subtle";
            }
        }

        // Helper: Category Icon
        public string CategoryIconClass
        {
            get
            {
                if (string.Equals(Category, "Workout", StringComparison.OrdinalIgnoreCase))
                    return "bi bi-lightning-charge-fill";
                if (string.Equals(Category, "Diet", StringComparison.OrdinalIgnoreCase))
                    return "bi bi-egg-fried";
                return "bi bi-graph-up-arrow";
            }
        }

        // Helper: Category Color Name
        public string CategoryColor
        {
            get
            {
                if (string.Equals(Category, "Workout", StringComparison.OrdinalIgnoreCase))
                    return "primary";
                if (string.Equals(Category, "Diet", StringComparison.OrdinalIgnoreCase))
                    return "success";
                return "info";
            }
        }

        // Helper: Split recommendations into readable bullet items if multi-line
        public List<string> RecommendationsList
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Recommendations))
                    return new List<string>();

                var lines = Recommendations.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();

                return lines.Any() ? lines : new List<string> { Recommendations.Trim() };
            }
        }
    }

    // ========================================================================
    // Assigned Trainer Profile Summary DTO
    // ========================================================================
    public class AssignedTrainerSummaryDto
    {
        public string TrainerId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Specialization { get; set; }
    }

    // ========================================================================
    // Main View Model for Client Feedback Page
    // ========================================================================
    public class ClientFeedbackDashboardViewModel
    {
        public ClientSummaryDto CurrentClient { get; set; }
        public AssignedTrainerSummaryDto AssignedTrainer { get; set; }

        public string SelectedCategory { get; set; } = "all";
        public string SearchTerm { get; set; }

        // Metrics & KPI Counts
        public int TotalCount { get; set; }
        public int WorkoutCount { get; set; }
        public int DietCount { get; set; }
        public int GeneralCount { get; set; }

        // Highlighted Latest Feedback
        public ClientFeedbackItemDto LatestFeedback { get; set; }

        // All Feedback History matching filters
        public List<ClientFeedbackItemDto> FeedbackHistory { get; set; } = new List<ClientFeedbackItemDto>();
    }
}
