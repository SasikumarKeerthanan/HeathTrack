using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    public class TrainerAppointmentItem
    {
        public int AppointmentId { get; set; }
        public string AppointmentCode { get; set; }
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string ClientEmail { get; set; }
        public string ClientAvatar { get; set; }
        public DateTime AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Title { get; set; }
        public string ClientNotes { get; set; }
        public string TrainerNotes { get; set; }
        public string Status { get; set; }
        public string MeetingLocation { get; set; }
        public string CancellationReason { get; set; }
        public DateTime CreatedAt { get; set; }

        public DateTime FullStartDateTime => AppointmentDate.Date.Add(StartTime);
        public DateTime FullEndDateTime => AppointmentDate.Date.Add(EndTime);

        // FR-T-36: Trainer can mark completed after scheduled start
        public bool CanBeCompleted => Status == "Approved" && DateTime.Now >= FullStartDateTime;

        // FR-T-38: Alternative Appointment Proposal properties
        public bool CanProposeAlternative => (Status == "Pending" || Status == "Approved");
        public bool HasActiveProposal { get; set; }
        public int? PendingProposalId { get; set; }
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

    public class TrainerAvailabilitySlotItem
    {
        public int AvailabilityId { get; set; }
        public DateTime AvailableDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsBooked { get; set; }
        public string BookedClientName { get; set; }
        public string BookedAppointmentCode { get; set; }

        // FR-T-35: Remove future availability slot with no active booking
        public bool CanDelete => !IsBooked && AvailableDate.Date >= DateTime.Today;
    }
 
    // FR-C-45: Trainer view model for incoming rescheduling requests
    public class TrainerRescheduleRequestItem
    {
        public int RescheduleRequestId { get; set; }
        public int AppointmentId { get; set; }
        public string AppointmentCode { get; set; }
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string ClientEmail { get; set; }
        public string ClientAvatar { get; set; }
        public string Title { get; set; }
        public string MeetingLocation { get; set; }

        // Original session schedule
        public DateTime OriginalDate { get; set; }
        public TimeSpan OriginalStartTime { get; set; }
        public TimeSpan OriginalEndTime { get; set; }

        // Requested session schedule
        public int? RequestedAvailabilityId { get; set; }
        public DateTime RequestedDate { get; set; }
        public TimeSpan RequestedStartTime { get; set; }
        public TimeSpan RequestedEndTime { get; set; }
        public string Reason { get; set; }
        public DateTime RequestedAt { get; set; }
        public string Status { get; set; }

        public DateTime OriginalFullStartDateTime => OriginalDate.Date.Add(OriginalStartTime);
        public DateTime RequestedFullStartDateTime => RequestedDate.Date.Add(RequestedStartTime);
        public DateTime RequestedFullEndDateTime => RequestedDate.Date.Add(RequestedEndTime);

        public string OriginalDisplayDateTime =>
            OriginalDate.ToString("ddd, MMM dd, yyyy") + " • " +
            DateTime.Today.Add(OriginalStartTime).ToString("hh:mm tt") + " - " +
            DateTime.Today.Add(OriginalEndTime).ToString("hh:mm tt");

        public string RequestedDisplayDateTime =>
            RequestedDate.ToString("ddd, MMM dd, yyyy") + " • " +
            DateTime.Today.Add(RequestedStartTime).ToString("hh:mm tt") + " - " +
            DateTime.Today.Add(RequestedEndTime).ToString("hh:mm tt");
    }

    public class TrainerAppointmentIndexViewModel
    {
        public string TrainerName { get; set; }
        public List<TrainerAppointmentItem> PendingRequests { get; set; } = new List<TrainerAppointmentItem>();
        public List<TrainerAppointmentItem> ApprovedUpcoming { get; set; } = new List<TrainerAppointmentItem>();
        public List<TrainerAppointmentItem> CompletedHistory { get; set; } = new List<TrainerAppointmentItem>();
        public List<TrainerAppointmentItem> CancelledOrRejected { get; set; } = new List<TrainerAppointmentItem>();
        public List<TrainerAvailabilitySlotItem> OpenAvailabilitySlots { get; set; } = new List<TrainerAvailabilitySlotItem>();
        public List<TrainerRescheduleRequestItem> RescheduleRequests { get; set; } = new List<TrainerRescheduleRequestItem>();
        public List<TrainerAvailabilitySlotItem> FutureUnbookedSlots { get; set; } = new List<TrainerAvailabilitySlotItem>();

        public int PendingCount => PendingRequests?.Count ?? 0;
        public int ApprovedCount => ApprovedUpcoming?.Count ?? 0;
        public int CompletedCount => CompletedHistory?.Count ?? 0;
        public int RescheduleRequestsCount => RescheduleRequests?.Count ?? 0;
        public int OpenSlotsCount => OpenAvailabilitySlots != null ? System.Linq.Enumerable.Count(OpenAvailabilitySlots, s => !s.IsBooked && s.AvailableDate >= DateTime.Today) : 0;
        public int TotalSlotsCount => OpenAvailabilitySlots?.Count ?? 0;
    }

    public class CreateAvailabilityInputModel
    {
        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Available Date")]
        public DateTime AvailableDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Start time is required")]
        [Display(Name = "Start Time")]
        public string StartTimeString { get; set; } = "09:00";

        [Required(ErrorMessage = "End time is required")]
        [Display(Name = "End Time")]
        public string EndTimeString { get; set; } = "10:00";
    }

    public class ApproveAppointmentInputModel
    {
        [Required]
        public int AppointmentId { get; set; }

        [StringLength(250, ErrorMessage = "Location cannot exceed 250 characters")]
        [Display(Name = "Meeting Location or Video Call Link")]
        public string MeetingLocation { get; set; }
    }

    public class RejectOrCancelAppointmentInputModel
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required(ErrorMessage = "Please state the reason")]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        [Display(Name = "Reason")]
        public string Reason { get; set; }
    }

    public class CompleteAppointmentInputModel
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required(ErrorMessage = "Please enter consultation notes and workout advice for the client")]
        [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
        [Display(Name = "Session & Consultation Notes")]
        public string TrainerNotes { get; set; }
    }

    // FR-C-45: Input models for handling appointment rescheduling requests
    public class ApproveRescheduleInputModel
    {
        [Required]
        public int RescheduleRequestId { get; set; }

        [StringLength(250, ErrorMessage = "Location cannot exceed 250 characters")]
        [Display(Name = "Updated Meeting Location or Link (optional)")]
        public string MeetingLocation { get; set; }
    }

    public class RejectRescheduleInputModel
    {
        [Required]
        public int RescheduleRequestId { get; set; }

        [Required(ErrorMessage = "Please state a reason for declining the rescheduling request")]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        [Display(Name = "Reason for Declining")]
        public string Reason { get; set; }
    }

    // FR-T-38: Input model for Trainer proposing alternative appointment time
    public class ProposeAlternativeInputModel
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required(ErrorMessage = "Please select an available alternative time slot")]
        [Display(Name = "Alternative Time Slot")]
        public int AvailabilityId { get; set; }

        [StringLength(500, ErrorMessage = "Message cannot exceed 500 characters")]
        [Display(Name = "Message / Reason to Client (Optional)")]
        public string TrainerMessage { get; set; }
    }
}
