using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Admin.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class AppointmentsController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // GET: /Admin/Appointments
        [HttpGet]
        public ActionResult Index(string search = null, string status = "all")
        {
            var query = db.Appointments
                .Include(a => a.Clients)
                .Include(a => a.Trainers)
                .AsQueryable();

            var all = query.ToList();

            var model = new AdminAppointmentIndexViewModel
            {
                ActiveStatusFilter = string.IsNullOrEmpty(status) ? "all" : status.ToLower(),
                SearchTerm = search,
                TotalCount = all.Count,
                PendingCount = all.Count(a => a.Status == "Pending"),
                ApprovedCount = all.Count(a => a.Status == "Approved"),
                CompletedCount = all.Count(a => a.Status == "Completed"),
                CancelledCount = all.Count(a => a.Status == "Cancelled" || a.Status == "Rejected")
            };

            // Filter by status
            if (!string.IsNullOrEmpty(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (status.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    all = all.Where(a => a.Status == "Cancelled" || a.Status == "Rejected").ToList();
                }
                else
                {
                    all = all.Where(a => a.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            // Search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLower();
                all = all.Where(a =>
                    (a.AppointmentCode != null && a.AppointmentCode.ToLower().Contains(s)) ||
                    (a.Title != null && a.Title.ToLower().Contains(s)) ||
                    (a.Clients != null && a.Clients.FullName.ToLower().Contains(s)) ||
                    (a.Trainers != null && a.Trainers.FullName.ToLower().Contains(s))
                ).ToList();
            }

            model.Appointments = all
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .Select(a => new AdminAppointmentItem
                {
                    AppointmentId = a.AppointmentId,
                    AppointmentCode = a.AppointmentCode,
                    ClientId = a.ClientId,
                    ClientName = a.Clients != null ? a.Clients.FullName : a.ClientId,
                    ClientEmail = a.Clients != null ? a.Clients.Email : "",
                    TrainerId = a.TrainerId,
                    TrainerName = a.Trainers != null ? a.Trainers.FullName : a.TrainerId,
                    TrainerSpecialization = a.Trainers != null ? (a.Trainers.PrimarySpecialization ?? a.Trainers.Specialization) : "",
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Title = a.Title,
                    Status = a.Status,
                    MeetingLocation = a.MeetingLocation,
                    ClientNotes = a.ClientNotes,
                    TrainerNotes = a.TrainerNotes,
                    CancellationReason = a.CancellationReason,
                    CreatedAt = a.CreatedAt
                })
                .ToList();

            return View(model);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
