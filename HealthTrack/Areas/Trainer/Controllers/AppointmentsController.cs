using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Trainer.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.Controllers
{
    [TrainerAuthorize]
    public class AppointmentsController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // GET: /Trainer/Appointments
        [HttpGet]
        public ActionResult Index()
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var trainer = db.Trainers.FirstOrDefault(t => t.TrainerId == trainerId);
            if (trainer == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var model = new TrainerAppointmentIndexViewModel
            {
                TrainerName = trainer.FullName
            };

            // 1. Fetch all appointments assigned to this trainer (FR-T-16, FR-T-38)
            var allAppointments = db.Appointments
                .Include(a => a.Clients)
                .Include(a => a.AppointmentAlternativeProposals)
                .Where(a => a.TrainerId == trainerId)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToList();

            foreach (var a in allAppointments)
            {
                var item = new TrainerAppointmentItem
                {
                    AppointmentId = a.AppointmentId,
                    AppointmentCode = a.AppointmentCode,
                    ClientId = a.ClientId,
                    ClientName = a.Clients != null ? a.Clients.FullName : "Client",
                    ClientEmail = a.Clients != null ? a.Clients.Email : "",
                    ClientAvatar = a.Clients != null ? a.Clients.ProfilePicturePath : null,
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Title = a.Title,
                    ClientNotes = a.ClientNotes,
                    TrainerNotes = a.TrainerNotes,
                    Status = a.Status,
                    MeetingLocation = a.MeetingLocation,
                    CancellationReason = a.CancellationReason,
                    CreatedAt = a.CreatedAt
                };

                // FR-T-38: Active alternative appointment proposal awaiting client confirmation
                var activeProposal = a.AppointmentAlternativeProposals?.FirstOrDefault(p => p.Status == "Pending");
                if (activeProposal != null)
                {
                    item.HasActiveProposal = true;
                    item.PendingProposalId = activeProposal.ProposalId;
                    item.ProposedDate = activeProposal.ProposedDate;
                    item.ProposedStartTime = activeProposal.ProposedStartTime;
                    item.ProposedEndTime = activeProposal.ProposedEndTime;
                    item.TrainerProposalMessage = activeProposal.TrainerMessage;
                    item.ProposedAt = activeProposal.ProposedAt;
                }

                if (a.Status == "Pending")
                {
                    model.PendingRequests.Add(item);
                }
                else if (a.Status == "Approved")
                {
                    model.ApprovedUpcoming.Add(item);
                }
                else if (a.Status == "Completed")
                {
                    model.CompletedHistory.Add(item);
                }
                else
                {
                    model.CancelledOrRejected.Add(item);
                }
            }

            // Order approved upcoming ascending
            model.ApprovedUpcoming = model.ApprovedUpcoming
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .ToList();

            // 2. Fetch all availability slots for this trainer (FR-T-34, FR-T-35, FR-T-38)
            var today = DateTime.Today;
            var nowTime = DateTime.Now.TimeOfDay;
            var slots = db.TrainerAvailability
                .Include(s => s.Appointments)
                .Where(s => s.TrainerId == trainerId && s.AvailableDate >= today)
                .OrderBy(s => s.AvailableDate)
                .ThenBy(s => s.StartTime)
                .ToList();

            foreach (var s in slots)
            {
                var bookedApt = s.Appointments.FirstOrDefault(a => a.Status == "Pending" || a.Status == "Approved");
                var slotItem = new TrainerAvailabilitySlotItem
                {
                    AvailabilityId = s.AvailabilityId,
                    AvailableDate = s.AvailableDate,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    IsBooked = s.IsBooked,
                    BookedClientName = bookedApt?.Clients?.FullName,
                    BookedAppointmentCode = bookedApt?.AppointmentCode
                };
                model.OpenAvailabilitySlots.Add(slotItem);

                // FR-T-38: Collect future unbooked slots for alternative time proposals
                if (!s.IsBooked && (s.AvailableDate > today || (s.AvailableDate == today && s.StartTime > nowTime)))
                {
                    model.FutureUnbookedSlots.Add(slotItem);
                }
            }

            // 3. Fetch pending reschedule requests for this trainer (FR-C-45)
            var rescheduleRequests = db.AppointmentRescheduleRequests
                .Include(r => r.Appointments)
                .Include(r => r.Appointments.Clients)
                .Where(r => r.Appointments.TrainerId == trainerId && r.Status == "Pending")
                .OrderBy(r => r.RequestedDate)
                .ThenBy(r => r.RequestedStartTime)
                .ToList();

            foreach (var r in rescheduleRequests)
            {
                model.RescheduleRequests.Add(new TrainerRescheduleRequestItem
                {
                    RescheduleRequestId = r.RescheduleRequestId,
                    AppointmentId = r.AppointmentId,
                    AppointmentCode = r.Appointments != null ? r.Appointments.AppointmentCode : string.Empty,
                    ClientId = r.RequestedByClientId,
                    ClientName = r.Appointments?.Clients != null ? r.Appointments.Clients.FullName : "Client",
                    ClientEmail = r.Appointments?.Clients != null ? r.Appointments.Clients.Email : string.Empty,
                    ClientAvatar = r.Appointments?.Clients != null ? r.Appointments.Clients.ProfilePicturePath : null,
                    Title = r.Appointments != null ? r.Appointments.Title : string.Empty,
                    MeetingLocation = r.Appointments != null ? r.Appointments.MeetingLocation : string.Empty,
                    OriginalDate = r.Appointments != null ? r.Appointments.AppointmentDate : DateTime.MinValue,
                    OriginalStartTime = r.Appointments != null ? r.Appointments.StartTime : TimeSpan.Zero,
                    OriginalEndTime = r.Appointments != null ? r.Appointments.EndTime : TimeSpan.Zero,
                    RequestedAvailabilityId = r.RequestedAvailabilityId,
                    RequestedDate = r.RequestedDate,
                    RequestedStartTime = r.RequestedStartTime,
                    RequestedEndTime = r.RequestedEndTime,
                    Reason = r.Reason,
                    RequestedAt = r.RequestedAt,
                    Status = r.Status
                });
            }

            return View(model);
        }

        // POST: /Trainer/Appointments/AddAvailability (FR-T-34, FR-S-17)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddAvailability(CreateAvailabilityInputModel model)
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            if (model.AvailableDate < DateTime.Today)
            {
                TempData["ErrorMessage"] = "Cannot create availability for a date in the past.";
                return RedirectToAction("Index");
            }

            TimeSpan startTime, endTime;
            if (!TimeSpan.TryParse(model.StartTimeString, out startTime) || !TimeSpan.TryParse(model.EndTimeString, out endTime))
            {
                TempData["ErrorMessage"] = "Please select valid start and end times.";
                return RedirectToAction("Index");
            }

            if (endTime <= startTime)
            {
                TempData["ErrorMessage"] = "End time must be after start time.";
                return RedirectToAction("Index");
            }

            // Prevent overlapping availability slots for the same trainer (FR-S-17)
            bool overlaps = db.TrainerAvailability.Any(s =>
                s.TrainerId == trainerId &&
                s.AvailableDate == model.AvailableDate.Date &&
                s.StartTime < endTime &&
                s.EndTime > startTime);

            if (overlaps)
            {
                TempData["ErrorMessage"] = "An availability slot already overlaps with this time range.";
                return RedirectToAction("Index");
            }

            try
            {
                var slot = new TrainerAvailability
                {
                    TrainerId = trainerId,
                    AvailableDate = model.AvailableDate.Date,
                    StartTime = startTime,
                    EndTime = endTime,
                    IsBooked = false,
                    CreatedAt = DateTime.Now
                };

                db.TrainerAvailability.Add(slot);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Availability slot added for {slot.AvailableDate:ddd, MMM dd} ({DateTime.Today.Add(slot.StartTime):hh:mm tt} - {DateTime.Today.Add(slot.EndTime):hh:mm tt})!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error adding slot: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /Trainer/Appointments/DeleteAvailability (FR-T-35)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteAvailability(int id)
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var slot = db.TrainerAvailability.FirstOrDefault(s => s.AvailabilityId == id && s.TrainerId == trainerId);
            if (slot == null)
            {
                TempData["ErrorMessage"] = "Availability slot not found.";
                return RedirectToAction("Index");
            }

            // FR-T-35: Only remove if no active booking
            if (slot.IsBooked)
            {
                TempData["ErrorMessage"] = "Cannot delete this slot because it is currently booked by an appointment. Cancel the appointment first.";
                return RedirectToAction("Index");
            }

            try
            {
                db.TrainerAvailability.Remove(slot);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Availability slot removed.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error removing slot: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /Trainer/Appointments/Approve (FR-T-17)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(ApproveAppointmentInputModel model)
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var apt = db.Appointments.FirstOrDefault(a => a.AppointmentId == model.AppointmentId && a.TrainerId == trainerId);
            if (apt == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Index");
            }

            try
            {
                apt.Status = "Approved";
                apt.MeetingLocation = !string.IsNullOrWhiteSpace(model.MeetingLocation)
                    ? model.MeetingLocation.Trim()
                    : "Main Fitness Center - Floor Consult Desk";
                apt.UpdatedAt = DateTime.Now;

                db.SaveChanges();
                TempData["SuccessMessage"] = $"Appointment {apt.AppointmentCode} confirmed and approved!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error approving appointment: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /Trainer/Appointments/Reject (FR-T-19)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reject(RejectOrCancelAppointmentInputModel model)
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var apt = db.Appointments.FirstOrDefault(a => a.AppointmentId == model.AppointmentId && a.TrainerId == trainerId);
            if (apt == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Index");
            }

            try
            {
                apt.Status = "Rejected";
                apt.CancellationReason = model.Reason.Trim();
                apt.UpdatedAt = DateTime.Now;

                // Release availability slot
                if (apt.AvailabilityId.HasValue)
                {
                    var slot = db.TrainerAvailability.Find(apt.AvailabilityId.Value);
                    if (slot != null) slot.IsBooked = false;
                }

                db.SaveChanges();
                TempData["SuccessMessage"] = $"Appointment {apt.AppointmentCode} declined.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error declining appointment: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /Trainer/Appointments/Complete (FR-T-36, FR-T-37)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Complete(CompleteAppointmentInputModel model)
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var apt = db.Appointments.FirstOrDefault(a => a.AppointmentId == model.AppointmentId && a.TrainerId == trainerId);
            if (apt == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Index");
            }

            try
            {
                apt.Status = "Completed";
                apt.TrainerNotes = model.TrainerNotes.Trim();
                apt.UpdatedAt = DateTime.Now;

                db.SaveChanges();
                TempData["SuccessMessage"] = $"Consultation {apt.AppointmentCode} marked completed and session notes recorded successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error completing appointment: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /Trainer/Appointments/Cancel (FR-T-19)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(RejectOrCancelAppointmentInputModel model)
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var apt = db.Appointments.FirstOrDefault(a => a.AppointmentId == model.AppointmentId && a.TrainerId == trainerId);
            if (apt == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Index");
            }

            try
            {
                apt.Status = "Cancelled";
                apt.CancellationReason = "Trainer Cancelled: " + model.Reason.Trim();
                apt.UpdatedAt = DateTime.Now;

                if (apt.AvailabilityId.HasValue)
                {
                    var slot = db.TrainerAvailability.Find(apt.AvailabilityId.Value);
                    if (slot != null) slot.IsBooked = false;
                }

                db.SaveChanges();
                TempData["SuccessMessage"] = $"Appointment {apt.AppointmentCode} cancelled.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error cancelling appointment: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /Trainer/Appointments/ApproveReschedule (FR-C-45)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveReschedule(ApproveRescheduleInputModel model)
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid request.";
                return RedirectToAction("Index");
            }

            var req = db.AppointmentRescheduleRequests
                .Include(r => r.Appointments)
                .FirstOrDefault(r => r.RescheduleRequestId == model.RescheduleRequestId && r.Appointments.TrainerId == trainerId);

            if (req == null)
            {
                TempData["ErrorMessage"] = "Rescheduling request not found.";
                return RedirectToAction("Index");
            }

            if (req.Status != "Pending")
            {
                TempData["ErrorMessage"] = "This request has already been processed.";
                return RedirectToAction("Index");
            }

            var apt = req.Appointments;
            if (apt == null)
            {
                TempData["ErrorMessage"] = "Associated appointment not found.";
                return RedirectToAction("Index");
            }

            // Verify requested slot is still valid and unbooked
            TrainerAvailability newSlot = null;
            if (req.RequestedAvailabilityId.HasValue)
            {
                newSlot = db.TrainerAvailability.FirstOrDefault(s => s.AvailabilityId == req.RequestedAvailabilityId.Value && s.TrainerId == trainerId);
                if (newSlot == null || newSlot.IsBooked)
                {
                    TempData["ErrorMessage"] = "The requested availability slot is no longer available. Please reject this request and inform the client.";
                    return RedirectToAction("Index");
                }
            }

            try
            {
                // 1. Release old slot if different
                if (apt.AvailabilityId.HasValue && apt.AvailabilityId != req.RequestedAvailabilityId)
                {
                    var oldSlot = db.TrainerAvailability.Find(apt.AvailabilityId.Value);
                    if (oldSlot != null)
                    {
                        oldSlot.IsBooked = false;
                    }
                }

                // 2. Lock new slot
                if (newSlot != null)
                {
                    newSlot.IsBooked = true;
                }

                // 3. Update appointment with requested schedule
                apt.AvailabilityId = req.RequestedAvailabilityId;
                apt.AppointmentDate = req.RequestedDate;
                apt.StartTime = req.RequestedStartTime;
                apt.EndTime = req.RequestedEndTime;
                apt.Status = "Approved";
                if (!string.IsNullOrWhiteSpace(model.MeetingLocation))
                {
                    apt.MeetingLocation = model.MeetingLocation.Trim();
                }
                apt.UpdatedAt = DateTime.Now;

                // 4. Update reschedule request record
                req.Status = "Approved";
                req.ReviewedAt = DateTime.Now;
                req.ReviewedByTrainerId = trainerId;
                req.TrainerResponse = "Approved by Coach";

                db.SaveChanges();

                TempData["SuccessMessage"] = $"Rescheduling request for appointment {apt.AppointmentCode} approved! The session has been rescheduled to {apt.AppointmentDate:ddd, MMM dd} ({DateTime.Today.Add(apt.StartTime):hh:mm tt}).";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error approving rescheduling request: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /Trainer/Appointments/RejectReschedule (FR-C-45)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectReschedule(RejectRescheduleInputModel model)
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please state a reason for declining the rescheduling request.";
                return RedirectToAction("Index");
            }

            var req = db.AppointmentRescheduleRequests
                .Include(r => r.Appointments)
                .FirstOrDefault(r => r.RescheduleRequestId == model.RescheduleRequestId && r.Appointments.TrainerId == trainerId);

            if (req == null)
            {
                TempData["ErrorMessage"] = "Rescheduling request not found.";
                return RedirectToAction("Index");
            }

            if (req.Status != "Pending")
            {
                TempData["ErrorMessage"] = "This request has already been processed.";
                return RedirectToAction("Index");
            }

            try
            {
                req.Status = "Rejected";
                req.ReviewedAt = DateTime.Now;
                req.ReviewedByTrainerId = trainerId;
                req.TrainerResponse = model.Reason.Trim();

                // Original appointment schedule and slots remain untouched!

                db.SaveChanges();

                TempData["SuccessMessage"] = $"Rescheduling request for appointment {req.Appointments?.AppointmentCode} has been declined. The original appointment schedule remains active.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error declining rescheduling request: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /Trainer/Appointments/ProposeAlternative (FR-T-38)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProposeAlternative(ProposeAlternativeInputModel model)
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please select a valid alternative availability slot.";
                return RedirectToAction("Index");
            }

            var apt = db.Appointments
                .Include(a => a.AppointmentAlternativeProposals)
                .FirstOrDefault(a => a.AppointmentId == model.AppointmentId && a.TrainerId == trainerId);

            if (apt == null)
            {
                TempData["ErrorMessage"] = "Appointment not found or you are not authorized to propose changes for it.";
                return RedirectToAction("Index");
            }

            // FR-T-38 Req 2: Only eligible appointments that are not Cancelled, Rejected, or Completed
            if (apt.Status == "Cancelled" || apt.Status == "Rejected" || apt.Status == "Completed")
            {
                TempData["ErrorMessage"] = $"Cannot propose alternative times for an appointment with status '{apt.Status}'.";
                return RedirectToAction("Index");
            }

            // FR-T-38 Req 10: Prevent multiple conflicting active proposals for the same appointment
            bool hasActivePending = db.AppointmentAlternativeProposals.Any(p => p.AppointmentId == apt.AppointmentId && p.Status == "Pending");
            if (hasActivePending)
            {
                TempData["ErrorMessage"] = "An active alternative appointment proposal is already pending client review for this session.";
                return RedirectToAction("Index");
            }

            // FR-T-38 Req 4 & 5: Ensure selected slot exists, belongs to trainer, is future, and unbooked
            var today = DateTime.Today;
            var nowTime = DateTime.Now.TimeOfDay;
            var slot = db.TrainerAvailability.FirstOrDefault(s => s.AvailabilityId == model.AvailabilityId && s.TrainerId == trainerId);
            if (slot == null || slot.IsBooked || slot.AvailableDate < today || (slot.AvailableDate == today && slot.StartTime <= nowTime))
            {
                TempData["ErrorMessage"] = "The selected time slot is no longer available or is in the past. Please select an open future slot.";
                return RedirectToAction("Index");
            }

            // Ensure slot is not identical to current appointment time
            if (apt.AppointmentDate == slot.AvailableDate && apt.StartTime == slot.StartTime && apt.EndTime == slot.EndTime)
            {
                TempData["ErrorMessage"] = "The selected time slot is identical to the current appointment time. Please pick a different alternative slot.";
                return RedirectToAction("Index");
            }

            try
            {
                // Create alternative proposal without changing original appointment date, time, or status (Req 8 & 9)
                var proposal = new AppointmentAlternativeProposals
                {
                    AppointmentId = apt.AppointmentId,
                    TrainerId = trainerId,
                    ClientId = apt.ClientId,
                    ProposedAvailabilityId = slot.AvailabilityId,
                    ProposedDate = slot.AvailableDate,
                    ProposedStartTime = slot.StartTime,
                    ProposedEndTime = slot.EndTime,
                    TrainerMessage = !string.IsNullOrWhiteSpace(model.TrainerMessage) ? model.TrainerMessage.Trim() : null,
                    Status = "Pending",
                    ProposedAt = DateTime.Now
                };

                db.AppointmentAlternativeProposals.Add(proposal);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Alternative time proposed for appointment {apt.AppointmentCode}: {slot.AvailableDate:ddd, MMM dd} ({DateTime.Today.Add(slot.StartTime):hh:mm tt} - {DateTime.Today.Add(slot.EndTime):hh:mm tt}). Awaiting client confirmation.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error saving alternative appointment proposal: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // GET: /Trainer/Appointments/ActivityReport
        // FR-T-51: Generate Appointment Activity Reports showing appointment counts and statuses within a selected date range
        [HttpGet]
        public ActionResult ActivityReport(DateTime? fromDate = null, DateTime? toDate = null, string preset = null, string status = null)
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account", new { area = "Trainer" });
            }

            var trainer = db.Trainers.FirstOrDefault(t => t.TrainerId == trainerId);
            string trainerName = trainer != null ? trainer.FullName : "Trainer";

            var model = new TrainerAppointmentActivityReportViewModel
            {
                TrainerName = trainerName,
                StatusFilter = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim()
            };

            DateTime today = DateTime.Today;
            DateTime? effectiveFrom = fromDate;
            DateTime? effectiveTo = toDate;
            string activePreset = preset?.ToLower();

            // Default to "all" when no filter parameters were passed
            if (string.IsNullOrEmpty(preset) && !fromDate.HasValue && !toDate.HasValue)
            {
                activePreset = "all";
            }

            if (!string.IsNullOrEmpty(activePreset) && activePreset != "custom")
            {
                switch (activePreset)
                {
                    case "7days":
                        effectiveFrom = today.AddDays(-7);
                        effectiveTo = today;
                        break;
                    case "30days":
                        effectiveFrom = today.AddDays(-30);
                        effectiveTo = today;
                        break;
                    case "90days":
                        effectiveFrom = today.AddDays(-90);
                        effectiveTo = today;
                        break;
                    case "all":
                    default:
                        effectiveFrom = null;
                        effectiveTo = null;
                        activePreset = "all";
                        break;
                }
            }
            else
            {
                activePreset = "custom";
            }

            model.DatePreset = activePreset;
            model.FromDate = effectiveFrom;
            model.ToDate = effectiveTo;

            // Date validation: From Date must not be later than To Date (FR-T-51 Section 2.C.4)
            if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveFrom.Value.Date > effectiveTo.Value.Date)
            {
                model.ValidationError = "The 'From Date' cannot be later than the 'To Date'. Please select a valid date range.";
                return View(model);
            }

            // Base query: Only appointments belonging to the authenticated trainer (FR-T-51 Section 4.2)
            var query = db.Appointments.Where(a => a.TrainerId == trainerId);

            // Apply date range filters on AppointmentDate (inclusive of both boundary dates)
            if (effectiveFrom.HasValue)
            {
                DateTime startBoundary = effectiveFrom.Value.Date;
                query = query.Where(a => a.AppointmentDate >= startBoundary);
            }

            if (effectiveTo.HasValue)
            {
                DateTime endBoundary = effectiveTo.Value.Date.AddDays(1);
                query = query.Where(a => a.AppointmentDate < endBoundary);
            }

            // Lightweight projection avoiding table joins (FR-T-51 Section 2.B.12, Section 4.5)
            var appointmentList = query
                .Select(a => new
                {
                    a.AppointmentId,
                    a.AppointmentCode,
                    a.AppointmentDate,
                    a.StartTime,
                    a.EndTime,
                    ClientName = a.Clients != null ? a.Clients.FullName : "Client",
                    ClientEmail = a.Clients != null ? a.Clients.Email : "",
                    a.Title,
                    a.Status,
                    a.MeetingLocation,
                    a.CancellationReason
                })
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToList();

            // Calculate core metrics using actual existing appointment status values (FR-T-51 Section 2.A.4 & 2.B)
            model.TotalAppointments = appointmentList.Count;
            model.PendingAppointments = appointmentList.Count(a => string.Equals(a.Status, "Pending", StringComparison.OrdinalIgnoreCase));
            model.ApprovedAppointments = appointmentList.Count(a => string.Equals(a.Status, "Approved", StringComparison.OrdinalIgnoreCase));
            model.CompletedAppointments = appointmentList.Count(a => string.Equals(a.Status, "Completed", StringComparison.OrdinalIgnoreCase));
            model.CancelledAppointments = appointmentList.Count(a => string.Equals(a.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
            model.RejectedAppointments = appointmentList.Count(a => string.Equals(a.Status, "Rejected", StringComparison.OrdinalIgnoreCase));

            int knownSum = model.PendingAppointments + model.ApprovedAppointments + model.CompletedAppointments + model.CancelledAppointments + model.RejectedAppointments;
            model.OtherStatusAppointments = Math.Max(0, model.TotalAppointments - knownSum);

            // Detail table records with optional status filter
            var detailList = appointmentList;
            if (!string.IsNullOrEmpty(model.StatusFilter) && !string.Equals(model.StatusFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                detailList = appointmentList
                    .Where(a => string.Equals(a.Status, model.StatusFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            model.ActivityDetails = detailList.Select(a => new AppointmentActivityDetailItem
            {
                AppointmentId = a.AppointmentId,
                AppointmentCode = a.AppointmentCode,
                AppointmentDate = a.AppointmentDate,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                ClientName = a.ClientName,
                ClientEmail = a.ClientEmail,
                Title = a.Title,
                Status = a.Status,
                MeetingLocation = a.MeetingLocation,
                CancellationReason = a.CancellationReason
            }).ToList();

            return View(model);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
