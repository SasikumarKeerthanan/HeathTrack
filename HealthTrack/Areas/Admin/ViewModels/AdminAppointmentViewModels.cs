using System;
using System.Collections.Generic;

namespace HealthTrack.Areas.Admin.ViewModels
{
    public class AdminAppointmentItem
    {
        public int AppointmentId { get; set; }
        public string AppointmentCode { get; set; }
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string ClientEmail { get; set; }
        public string TrainerId { get; set; }
        public string TrainerName { get; set; }
        public string TrainerSpecialization { get; set; }
        public DateTime AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public string MeetingLocation { get; set; }
        public string ClientNotes { get; set; }
        public string TrainerNotes { get; set; }
        public string CancellationReason { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminAppointmentIndexViewModel
    {
        public List<AdminAppointmentItem> Appointments { get; set; } = new List<AdminAppointmentItem>();
        public string ActiveStatusFilter { get; set; } = "all";
        public string SearchTerm { get; set; }

        public int TotalCount { get; set; }
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
    }
}
