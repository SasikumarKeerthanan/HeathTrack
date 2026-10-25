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
    [ClientAuthorize]
    public class AppointmentsController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // GET: /Client/Appointments
        [HttpGet]
        public ActionResult Index(DateTime? fromDate = null, DateTime? toDate = null, string status = null)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            var client = db.Clients.FirstOrDefault(c => c.ClientId == clientId);
            if (client == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            // Server-side validation for date range (FR-C-62)
            if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            {
                var temp = fromDate;
                fromDate = toDate;
                toDate = temp;
                TempData["ErrorMessage"] = "From Date cannot be later than To Date. Date range was automatically adjusted.";
            }

            string normStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();

            var model = new ClientAppointmentIndexViewModel
            {
                ClientName = client.FullName,
                AssignedTrainerId = client.AssignedTrainerId,
                FromDate = fromDate,
                ToDate = toDate,
                FilterStatus = normStatus
            };

            // 1. Fetch assigned trainer info
            if (!string.IsNullOrEmpty(client.AssignedTrainerId))
            {
                var trainer = db.Trainers.FirstOrDefault(t => t.TrainerId == client.AssignedTrainerId);
                if (trainer != null)
                {
                    model.AssignedTrainerName = trainer.FullName;
                    model.AssignedTrainerSpecialization = trainer.PrimarySpecialization ?? trainer.Specialization ?? "Certified Fitness Coach";
                    model.AssignedTrainerEmail = trainer.Email;

                    // 2. Fetch future available unbooked slots from this trainer (FR-C-15, FR-S-31)
                    var today = DateTime.Today;
                    var nowTime = DateTime.Now.TimeOfDay;

                    var rawSlots = db.TrainerAvailability
                        .Where(s => s.TrainerId == client.AssignedTrainerId && !s.IsBooked && s.AvailableDate >= today)
                        .OrderBy(s => s.AvailableDate)
                        .ThenBy(s => s.StartTime)
                        .ToList();

                    // Filter out past slots for today
                    model.AvailableSlots = rawSlots
                        .Where(s => s.AvailableDate > today || s.StartTime > nowTime)
                        .Select(s => new TrainerSlotItem
                        {
                            AvailabilityId = s.AvailabilityId,
                            AvailableDate = s.AvailableDate,
                            StartTime = s.StartTime,
                            EndTime = s.EndTime,
                            DisplayText = $"{s.AvailableDate:ddd, MMM dd, yyyy} &bull; {DateTime.Today.Add(s.StartTime):hh:mm tt} - {DateTime.Today.Add(s.EndTime):hh:mm tt}"
                        })
                        .ToList();
                }
            }

            // 3. Fetch Client's appointments with server-side date & status filtering (FR-C-62)
            var appointmentsQuery = db.Appointments
                .Include(a => a.Trainers)
                .Include(a => a.AppointmentRescheduleRequests)
                .Include(a => a.AppointmentAlternativeProposals)
                .Where(a => a.ClientId == clientId);

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                appointmentsQuery = appointmentsQuery.Where(a => DbFunctions.TruncateTime(a.AppointmentDate) >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date;
                appointmentsQuery = appointmentsQuery.Where(a => DbFunctions.TruncateTime(a.AppointmentDate) <= to);
            }

            if (!string.IsNullOrEmpty(normStatus) && normStatus != "all")
            {
                appointmentsQuery = appointmentsQuery.Where(a => a.Status.ToLower() == normStatus);
            }

            var allAppointments = appointmentsQuery
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToList();

            var todayDate = DateTime.Today;
            var currentTime = DateTime.Now.TimeOfDay;

            foreach (var a in allAppointments)
            {
                var item = new ClientAppointmentItem
                {
                    AppointmentId = a.AppointmentId,
                    AppointmentCode = a.AppointmentCode,
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Title = a.Title,
                    ClientNotes = a.ClientNotes,
                    TrainerNotes = a.TrainerNotes,
                    Status = a.Status,
                    MeetingLocation = a.MeetingLocation,
                    CancellationReason = a.CancellationReason,
                    TrainerName = a.Trainers != null ? a.Trainers.FullName : "Not Assigned",
                    TrainerSpecialization = a.Trainers != null ? (a.Trainers.PrimarySpecialization ?? a.Trainers.Specialization ?? "Coach") : string.Empty,
                    TrainerEmail = a.Trainers != null ? a.Trainers.Email : null,
                    TrainerAvatar = a.Trainers != null ? a.Trainers.ProfilePicturePath : null,
                    CreatedAt = a.CreatedAt
                };

                // FR-C-45: Check for active pending reschedule request
                var pendingReq = a.AppointmentRescheduleRequests?.FirstOrDefault(r => r.Status == "Pending");
                if (pendingReq != null)
                {
                    item.HasPendingRescheduleRequest = true;
                    item.PendingRescheduleRequestId = pendingReq.RescheduleRequestId;
                    item.PendingRescheduleNewDate = pendingReq.RequestedDate;
                    item.PendingRescheduleNewStartTime = pendingReq.RequestedStartTime;
                    item.PendingRescheduleNewEndTime = pendingReq.RequestedEndTime;
                    item.PendingRescheduleReason = pendingReq.Reason;
                    item.PendingRescheduleRequestedAt = pendingReq.RequestedAt;
                }

                // FR-T-38: Check for active pending alternative proposal from trainer
                var pendingProposal = a.AppointmentAlternativeProposals?.FirstOrDefault(p => p.Status == "Pending");
                if (pendingProposal != null)
                {
                    item.HasTrainerProposal = true;
                    item.TrainerProposalId = pendingProposal.ProposalId;
                    item.ProposedDate = pendingProposal.ProposedDate;
                    item.ProposedStartTime = pendingProposal.ProposedStartTime;
                    item.ProposedEndTime = pendingProposal.ProposedEndTime;
                    item.TrainerProposalMessage = pendingProposal.TrainerMessage;
                    item.ProposedAt = pendingProposal.ProposedAt;
                }

                bool isUpcoming = (a.Status == "Pending" || a.Status == "Approved") &&
                                  (a.AppointmentDate > todayDate || (a.AppointmentDate == todayDate && a.EndTime >= currentTime));

                if (isUpcoming)
                {
                    model.UpcomingAppointments.Add(item);
                }
                else
                {
                    model.PastAppointments.Add(item);
                }
            }

            // Sort upcoming in ascending chronological order
            model.UpcomingAppointments = model.UpcomingAppointments
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .ToList();

            return View(model);
        }

        // POST: /Client/Appointments/Book
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Book(BookAppointmentInputModel model)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            var client = db.Clients.FirstOrDefault(c => c.ClientId == clientId);
            if (client == null || string.IsNullOrEmpty(client.AssignedTrainerId))
            {
                TempData["ErrorMessage"] = "You must have an assigned trainer to book a consultation session.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please select an available session slot and enter a consultation topic.";
                return RedirectToAction("Index");
            }

            // Find selected slot and verify it belongs to assigned trainer (FR-S-17, FR-S-31)
            var slot = db.TrainerAvailability.FirstOrDefault(s =>
                s.AvailabilityId == model.AvailabilityId.Value &&
                s.TrainerId == client.AssignedTrainerId);

            if (slot == null || slot.IsBooked || slot.AvailableDate < DateTime.Today)
            {
                TempData["ErrorMessage"] = "The selected time slot is no longer available. Please select another slot.";
                return RedirectToAction("Index");
            }

            if (slot.AvailableDate == DateTime.Today && slot.StartTime <= DateTime.Now.TimeOfDay)
            {
                TempData["ErrorMessage"] = "Cannot book an appointment for a past time slot.";
                return RedirectToAction("Index");
            }

            try
            {
                int count = db.Appointments.Count() + 1001;
                string code = $"APT-{count}";
                while (db.Appointments.Any(a => a.AppointmentCode == code))
                {
                    count++;
                    code = $"APT-{count}";
                }

                var appointment = new Appointments
                {
                    AppointmentCode = code,
                    ClientId = clientId,
                    TrainerId = client.AssignedTrainerId,
                    AvailabilityId = slot.AvailabilityId,
                    AppointmentDate = slot.AvailableDate,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    Title = model.Title.Trim(),
                    ClientNotes = string.IsNullOrWhiteSpace(model.ClientNotes) ? null : model.ClientNotes.Trim(),
                    TrainerNotes = null,
                    Status = "Pending", // Awaiting trainer confirmation
                    MeetingLocation = "Fitness Center / To be confirmed by Trainer",
                    CancellationReason = null,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = null
                };

                // Lock slot to prevent double-booking (FR-S-17)
                slot.IsBooked = true;

                db.Appointments.Add(appointment);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Your session '{appointment.Title}' has been requested for {appointment.AppointmentDate:ddd, MMM dd} ({DateTime.Today.Add(appointment.StartTime):hh:mm tt})! Your coach has been notified.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Could not book appointment: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /Client/Appointments/Cancel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(CancelAppointmentInputModel model)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please provide a reason for cancelling.";
                return RedirectToAction("Index");
            }

            var apt = db.Appointments.FirstOrDefault(a => a.AppointmentId == model.AppointmentId && a.ClientId == clientId);
            if (apt == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Index");
            }

            // FR-C-44: 24-Hour Policy check
            var startDateTime = apt.AppointmentDate.Date.Add(apt.StartTime);
            if ((startDateTime - DateTime.Now).TotalHours < 24)
            {
                TempData["ErrorMessage"] = "Policy Notice: Appointments cannot be cancelled online within 24 hours of start time. Please contact your coach directly.";
                return RedirectToAction("Index");
            }

            try
            {
                apt.Status = "Cancelled";
                apt.CancellationReason = model.Reason.Trim();
                apt.UpdatedAt = DateTime.Now;

                // Release availability slot so another client can use it
                if (apt.AvailabilityId.HasValue)
                {
                    var slot = db.TrainerAvailability.Find(apt.AvailabilityId.Value);
                    if (slot != null)
                    {
                        slot.IsBooked = false;
                    }
                }

                db.SaveChanges();
                TempData["SuccessMessage"] = "Your appointment has been cancelled successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error cancelling appointment: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /Client/Appointments/Reschedule (FR-C-45)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reschedule(RescheduleAppointmentInputModel model)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            if (!ModelState.IsValid || !model.NewAvailabilityId.HasValue)
            {
                TempData["ErrorMessage"] = "Please select a new time slot.";
                return RedirectToAction("Index");
            }

            var apt = db.Appointments.FirstOrDefault(a => a.AppointmentId == model.AppointmentId && a.ClientId == clientId);
            if (apt == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Index");
            }

            // FR-C-45: Only Pending or Approved appointments can be rescheduled
            if (apt.Status != "Pending" && apt.Status != "Approved")
            {
                TempData["ErrorMessage"] = "Only Pending or Approved appointments can be rescheduled.";
                return RedirectToAction("Index");
            }

            // FR-C-45: Ensure the Client can request rescheduling only at least 24 hours before original appointment
            var startDateTime = apt.AppointmentDate.Date.Add(apt.StartTime);
            if ((startDateTime - DateTime.Now).TotalHours < 24)
            {
                TempData["ErrorMessage"] = "Policy Notice: Rescheduling requests must be submitted at least 24 hours before the scheduled appointment start time. Please contact your coach directly.";
                return RedirectToAction("Index");
            }

            // FR-C-45: Prevent duplicate pending requests for the same appointment
            bool hasPendingRequest = db.AppointmentRescheduleRequests.Any(r => r.AppointmentId == apt.AppointmentId && r.Status == "Pending");
            if (hasPendingRequest)
            {
                TempData["ErrorMessage"] = "A rescheduling request for this appointment is already pending coach review.";
                return RedirectToAction("Index");
            }

            // Check new slot
            var newSlot = db.TrainerAvailability.FirstOrDefault(s => s.AvailabilityId == model.NewAvailabilityId.Value && s.TrainerId == apt.TrainerId);
            if (newSlot == null || newSlot.IsBooked || newSlot.AvailableDate < DateTime.Today)
            {
                TempData["ErrorMessage"] = "The selected new slot is no longer available. Please select another slot.";
                return RedirectToAction("Index");
            }

            if (newSlot.AvailableDate == DateTime.Today && newSlot.StartTime <= DateTime.Now.TimeOfDay)
            {
                TempData["ErrorMessage"] = "Cannot request a past time slot for rescheduling.";
                return RedirectToAction("Index");
            }

            // FR-C-45: Prevent overlapping appointments for this client
            bool hasClientConflict = db.Appointments.Any(a =>
                a.ClientId == clientId &&
                a.AppointmentId != apt.AppointmentId &&
                (a.Status == "Pending" || a.Status == "Approved") &&
                a.AppointmentDate == newSlot.AvailableDate &&
                a.StartTime < newSlot.EndTime &&
                a.EndTime > newSlot.StartTime);

            if (hasClientConflict)
            {
                TempData["ErrorMessage"] = "You already have another active appointment scheduled during this time slot.";
                return RedirectToAction("Index");
            }

            try
            {
                // FR-C-45: Create reschedule request record
                // Original appointment schedule and slot are NOT overwritten until the Trainer approves.
                var rescheduleRequest = new AppointmentRescheduleRequests
                {
                    AppointmentId = apt.AppointmentId,
                    RequestedByClientId = clientId,
                    RequestedAvailabilityId = newSlot.AvailabilityId,
                    RequestedDate = newSlot.AvailableDate,
                    RequestedStartTime = newSlot.StartTime,
                    RequestedEndTime = newSlot.EndTime,
                    Reason = string.IsNullOrWhiteSpace(model.Reason) ? null : model.Reason.Trim(),
                    Status = "Pending",
                    RequestedAt = DateTime.Now,
                    ReviewedAt = null,
                    ReviewedByTrainerId = null,
                    TrainerResponse = null
                };

                db.AppointmentRescheduleRequests.Add(rescheduleRequest);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Your rescheduling request for {rescheduleRequest.RequestedDate:ddd, MMM dd} ({DateTime.Today.Add(rescheduleRequest.RequestedStartTime):hh:mm tt}) has been submitted! Your original appointment remains active until your coach reviews and approves the request.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error requesting appointment reschedule: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /Client/Appointments/AcceptAlternativeProposal (FR-T-38)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AcceptAlternativeProposal(AcceptAlternativeProposalInputModel model)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid proposal request.";
                return RedirectToAction("Index");
            }

            var proposal = db.AppointmentAlternativeProposals
                .Include(p => p.Appointments)
                .FirstOrDefault(p => p.ProposalId == model.ProposalId && p.ClientId == clientId);

            if (proposal == null)
            {
                TempData["ErrorMessage"] = "Alternative appointment proposal not found.";
                return RedirectToAction("Index");
            }

            if (proposal.Status != "Pending")
            {
                TempData["ErrorMessage"] = $"This proposal has already been {proposal.Status.ToLower()}.";
                return RedirectToAction("Index");
            }

            var apt = proposal.Appointments;
            if (apt == null || apt.ClientId != clientId)
            {
                TempData["ErrorMessage"] = "Associated appointment not found or unauthorized.";
                return RedirectToAction("Index");
            }

            if (apt.Status == "Cancelled" || apt.Status == "Rejected" || apt.Status == "Completed")
            {
                TempData["ErrorMessage"] = $"Cannot accept a proposal for an appointment that is already {apt.Status.ToLower()}.";
                return RedirectToAction("Index");
            }

            // Check if proposed slot is in the past
            var proposedStart = proposal.ProposedDate.Date.Add(proposal.ProposedStartTime);
            if (proposedStart <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "The proposed alternative time has already passed.";
                return RedirectToAction("Index");
            }

            // Check proposed availability slot
            TrainerAvailability newSlot = null;
            if (proposal.ProposedAvailabilityId.HasValue)
            {
                newSlot = db.TrainerAvailability.Find(proposal.ProposedAvailabilityId.Value);
                if (newSlot == null || (newSlot.IsBooked && newSlot.AvailabilityId != apt.AvailabilityId))
                {
                    TempData["ErrorMessage"] = "The proposed trainer availability slot is no longer available. Please ask your trainer to propose another slot.";
                    return RedirectToAction("Index");
                }
            }

            // Prevent overlapping client appointments (excluding this appointment)
            bool hasClientConflict = db.Appointments.Any(a =>
                a.ClientId == clientId &&
                a.AppointmentId != apt.AppointmentId &&
                (a.Status == "Pending" || a.Status == "Approved") &&
                a.AppointmentDate == proposal.ProposedDate &&
                a.StartTime < proposal.ProposedEndTime &&
                a.EndTime > proposal.ProposedStartTime);

            if (hasClientConflict)
            {
                TempData["ErrorMessage"] = "You already have another active appointment scheduled during the proposed alternative time.";
                return RedirectToAction("Index");
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // 1. Release previous availability slot if it exists and differs from new slot
                    if (apt.AvailabilityId.HasValue && apt.AvailabilityId != proposal.ProposedAvailabilityId)
                    {
                        var oldSlot = db.TrainerAvailability.Find(apt.AvailabilityId.Value);
                        if (oldSlot != null)
                        {
                            oldSlot.IsBooked = false;
                        }
                    }

                    // 2. Lock the new slot
                    if (newSlot != null)
                    {
                        newSlot.IsBooked = true;
                    }

                    // 3. Update appointment to the alternative date/time
                    apt.AppointmentDate = proposal.ProposedDate;
                    apt.StartTime = proposal.ProposedStartTime;
                    apt.EndTime = proposal.ProposedEndTime;
                    apt.AvailabilityId = proposal.ProposedAvailabilityId;
                    apt.Status = "Approved"; // Confirmed appointment
                    apt.UpdatedAt = DateTime.Now;

                    // 4. Update the proposal record
                    proposal.Status = "Accepted";
                    proposal.RespondedAt = DateTime.Now;

                    // 5. If this appointment had an active pending client reschedule request, supersede it
                    var pendingReschedule = db.AppointmentRescheduleRequests
                        .FirstOrDefault(r => r.AppointmentId == apt.AppointmentId && r.Status == "Pending");
                    if (pendingReschedule != null)
                    {
                        pendingReschedule.Status = "Approved";
                        pendingReschedule.ReviewedAt = DateTime.Now;
                        pendingReschedule.ReviewedByTrainerId = proposal.TrainerId;
                        pendingReschedule.TrainerResponse = "Resolved via accepted alternative appointment proposal.";
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = $"Alternative appointment time accepted! Your consultation is now confirmed for {apt.AppointmentDate:ddd, MMM dd, yyyy} at {DateTime.Today.Add(apt.StartTime):hh:mm tt}.";
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["ErrorMessage"] = "Error accepting proposal: " + ex.Message;
                }
            }

            return RedirectToAction("Index");
        }

        // POST: /Client/Appointments/RejectAlternativeProposal (FR-T-38)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectAlternativeProposal(RejectAlternativeProposalInputModel model)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid proposal request.";
                return RedirectToAction("Index");
            }

            var proposal = db.AppointmentAlternativeProposals
                .Include(p => p.Appointments)
                .FirstOrDefault(p => p.ProposalId == model.ProposalId && p.ClientId == clientId);

            if (proposal == null)
            {
                TempData["ErrorMessage"] = "Alternative appointment proposal not found.";
                return RedirectToAction("Index");
            }

            if (proposal.Status != "Pending")
            {
                TempData["ErrorMessage"] = $"This proposal has already been {proposal.Status.ToLower()}.";
                return RedirectToAction("Index");
            }

            try
            {
                // Rejecting proposal keeps original appointment completely unchanged
                proposal.Status = "Rejected";
                proposal.RespondedAt = DateTime.Now;
                proposal.ClientResponse = string.IsNullOrWhiteSpace(model.ClientResponse) ? null : model.ClientResponse.Trim();

                db.SaveChanges();

                TempData["SuccessMessage"] = "You have declined the proposed alternative time. Your original appointment schedule remains unchanged.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error declining proposal: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
