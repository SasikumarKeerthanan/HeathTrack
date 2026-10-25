using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Areas.Client.ViewModels
{
    public class ClientAppointmentItem
    {
        public int AppointmentId { get; set; }
        public string AppointmentCode { get; set; }
        public DateTime AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Title { get; set; }
        public string ClientNotes { get; set; }
        public string TrainerNotes { get; set; }
        public string Status { get; set; }
        public string MeetingLocation { get; set; }
        public string CancellationReason { get; set; }
        public string TrainerName { get; set; }
        public string TrainerSpecialization { get; set; }
        public string TrainerEmail { get; set; }
        public string TrainerAvatar { get; set; }
        public DateTime CreatedAt { get; set; }

        public DateTime FullStartDateTime => AppointmentDate.Date.Add(StartTime);
        public DateTime FullEndDateTime => AppointmentDate.Date.Add(EndTime);

        // FR-C-43 & FR-C-44: 24-Hour Policy check
        public bool IsEligibleFor24HrChange =>
            (Status == "Pending" || Status == "Approved") &&
            (FullStartDateTime - DateTime.Now).TotalHours >= 24;

        public double HoursUntilStart => (FullStartDateTime - DateTime.Now).TotalHours;

        // FR-C-45: Rescheduling Request tracking
        public bool HasPendingRescheduleRequest { get; set; }
        public int? PendingRescheduleRequestId { get; set; }
        public DateTime? PendingRescheduleNewDate { get; set; }
        public TimeSpan? PendingRescheduleNewStartTime { get; set; }
        public TimeSpan? PendingRescheduleNewEndTime { get; set; }
        public string PendingRescheduleReason { get; set; }
        public DateTime? PendingRescheduleRequestedAt { get; set; }

        public string PendingRescheduleDisplayDateTime =>
            PendingRescheduleNewDate.HasValue && PendingRescheduleNewStartTime.HasValue && PendingRescheduleNewEndTime.HasValue
                ? PendingRescheduleNewDate.Value.ToString("ddd, MMM dd, yyyy") + " • " +
                  DateTime.Today.Add(PendingRescheduleNewStartTime.Value).ToString("hh:mm tt") + " - " +
                  DateTime.Today.Add(PendingRescheduleNewEndTime.Value).ToString("hh:mm tt")
                : string.Empty;

        public bool CanRequestReschedule => IsEligibleFor24HrChange && !HasPendingRescheduleRequest;

        // FR-T-38: Alternative Proposal by Trainer
        public bool HasTrainerProposal { get; set; }
        public int? TrainerProposalId { get; set; }
        public DateTime? ProposedDate { get; set; }
        public TimeSpan? ProposedStartTime { get; set; }
        public TimeSpan? ProposedEndTime { get; set; }
        public string TrainerProposalMessage { get; set; }
        public DateTime? ProposedAt { get; set; }

        public string ProposedDisplayDateTime =>
            ProposedDate.HasValue && ProposedStartTime.HasValue && ProposedEndTime.HasValue
                ? ProposedDate.Value.ToString("ddd, MMM dd, yyyy") + " • " +
                  DateTime.Today.Add(ProposedStartTime.Value).ToString("hh:mm tt") + " - " +
                  DateTime.Today.Add(ProposedEndTime.Value).ToString("hh:mm tt")
                : string.Empty;
    }

    public class TrainerSlotItem
    {
        public int AvailabilityId { get; set; }
        public DateTime AvailableDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string DisplayText { get; set; }
    }

    public class ClientAppointmentIndexViewModel
    {
        public string ClientName { get; set; }
        public string AssignedTrainerId { get; set; }
        public string AssignedTrainerName { get; set; }
        public string AssignedTrainerSpecialization { get; set; }
        public string AssignedTrainerEmail { get; set; }
        public bool HasAssignedTrainer => !string.IsNullOrEmpty(AssignedTrainerId);

        public List<ClientAppointmentItem> UpcomingAppointments { get; set; } = new List<ClientAppointmentItem>();
        public List<ClientAppointmentItem> PastAppointments { get; set; } = new List<ClientAppointmentItem>();
        public List<TrainerSlotItem> AvailableSlots { get; set; } = new List<TrainerSlotItem>();

        public int UpcomingCount => UpcomingAppointments?.Count ?? 0;
        public int PendingCount => UpcomingAppointments != null ? System.Linq.Enumerable.Count(UpcomingAppointments, a => a.Status == "Pending") : 0;
        public int ApprovedCount => UpcomingAppointments != null ? System.Linq.Enumerable.Count(UpcomingAppointments, a => a.Status == "Approved") : 0;
        public int PastCount => PastAppointments?.Count ?? 0;

        // FR-C-62: Filtering parameters
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string FilterStatus { get; set; } = "all";
        public bool HasActiveFilter => FromDate.HasValue || ToDate.HasValue || (!string.IsNullOrEmpty(FilterStatus) && FilterStatus != "all");
    }

    public class BookAppointmentInputModel
    {
        [Required(ErrorMessage = "Please select an available session time slot")]
        [Display(Name = "Available Slot")]
        public int? AvailabilityId { get; set; }

        [Required(ErrorMessage = "Please enter a consultation topic or title")]
        [StringLength(150, ErrorMessage = "Title cannot exceed 150 characters")]
        [Display(Name = "Consultation Topic / Title")]
        public string Title { get; set; }

        [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
        [Display(Name = "Session Goals / Questions for Coach")]
        public string ClientNotes { get; set; }
    }

    public class RescheduleAppointmentInputModel
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required(ErrorMessage = "Please select a new available time slot")]
        [Display(Name = "New Time Slot")]
        public int? NewAvailabilityId { get; set; }

        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        [Display(Name = "Reason for Rescheduling")]
        public string Reason { get; set; }
    }

    public class CancelAppointmentInputModel
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required(ErrorMessage = "Please state a reason for cancelling your session")]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        [Display(Name = "Cancellation Reason")]
        public string Reason { get; set; }
    }

    public class AcceptAlternativeProposalInputModel
    {
        [Required]
        public int ProposalId { get; set; }
    }

    public class RejectAlternativeProposalInputModel
    {
        [Required]
        public int ProposalId { get; set; }

        [StringLength(500, ErrorMessage = "Decline reason cannot exceed 500 characters")]
        [Display(Name = "Reason for Declining (Optional)")]
        public string ClientResponse { get; set; }
    }
}
