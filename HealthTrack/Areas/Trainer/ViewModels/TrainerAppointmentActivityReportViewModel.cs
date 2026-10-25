using System;
using System.Collections.Generic;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    /// <summary>
    /// Represents a single appointment row in the activity report (FR-T-51)
    /// </summary>
    public class AppointmentActivityDetailItem
    {
        public int AppointmentId { get; set; }
        public string AppointmentCode { get; set; }
        public DateTime AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string ClientName { get; set; }
        public string ClientEmail { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public string MeetingLocation { get; set; }
        public string CancellationReason { get; set; }

        public string FormattedTimeRange =>
            DateTime.Today.Add(StartTime).ToString("hh:mm tt") + " - " +
            DateTime.Today.Add(EndTime).ToString("hh:mm tt");
    }

    /// <summary>
    /// View model for the Trainer Appointment Activity Report (FR-T-51)
    /// </summary>
    public class TrainerAppointmentActivityReportViewModel
    {
        public string TrainerName { get; set; }

        // Date-Range Filtering (FR-T-51 Section 2.C)
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string DatePreset { get; set; } = "all"; // "all", "30days", "90days", "7days", "custom"
        public string StatusFilter { get; set; } = "all"; // "all", "Pending", "Approved", "Completed", "Cancelled", "Rejected"

        // Validation message for invalid date inputs
        public string ValidationError { get; set; }

        // Core Aggregate Metrics (FR-T-51 Section 2.A.4)
        public int TotalAppointments { get; set; }
        public int PendingAppointments { get; set; }
        public int ApprovedAppointments { get; set; }
        public int CompletedAppointments { get; set; }
        public int CancelledAppointments { get; set; }
        public int RejectedAppointments { get; set; }

        // Optional other statuses count if any exist in historical data
        public int OtherStatusAppointments { get; set; }

        // Detailed Activity Table (FR-T-51 Section 2.D)
        public List<AppointmentActivityDetailItem> ActivityDetails { get; set; } = new List<AppointmentActivityDetailItem>();
    }
}
