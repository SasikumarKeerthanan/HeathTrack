using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Admin.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class DashboardController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        [HttpGet]
        public ActionResult Index()
        {
            var model = new AdminDashboardViewModel();

            try
            {
                // 1. Client Metrics
                model.TotalClients = db.Clients.Count();
                model.ActiveClients = db.Clients.Count(c => c.IsActive);
                model.InactiveClients = db.Clients.Count(c => !c.IsActive);
                model.UnassignedClients = db.Clients.Count(c => c.AssignedTrainerId == null || c.AssignedTrainerId == "");

                // 2. Trainer Metrics
                model.TotalTrainers = db.Trainers.Count();
                model.ActiveTrainers = db.Trainers.Count(t => t.IsActive);
                model.InactiveTrainers = db.Trainers.Count(t => !t.IsActive);
                model.PendingInviteTrainers = db.Trainers.Count(t => !t.IsInvitationAccepted);

                // 3. Appointments & Requests
                model.TotalAppointments = db.Appointments.Count();
                model.ScheduledAppointments = db.Appointments.Count(a => a.Status == "Scheduled");
                model.CompletedAppointments = db.Appointments.Count(a => a.Status == "Completed");
                model.PendingTrainerChangeRequests = db.TrainerChangeRequests.Count(r => r.Status == "Pending");

                // 4. Trainer Workload Summaries
                var trainers = db.Trainers
                    .OrderBy(t => t.FullName)
                    .ToList();

                model.TrainerWorkloads = trainers.Select(t =>
                {
                    int maxCap = t.MaxClientCapacity > 0 ? t.MaxClientCapacity : 15;
                    int activeCount = db.Clients.Count(c => c.AssignedTrainerId == t.TrainerId && c.IsActive);
                    int utilPercent = maxCap > 0 ? (int)Math.Min(100, Math.Round((double)activeCount / maxCap * 100)) : 0;

                    return new TrainerWorkloadSummaryItem
                    {
                        TrainerId = t.TrainerId,
                        FullName = t.FullName,
                        Specialization = t.PrimarySpecialization ?? t.Specialization ?? "General Fitness",
                        ActiveClientsCount = activeCount,
                        MaxCapacity = maxCap,
                        UtilizationPercentage = utilPercent,
                        IsActive = t.IsActive
                    };
                }).ToList();

                // 5. Recent System & Security Activity Feed (Latest 8 logs)
                var recentLogs = db.AuthAuditLogs
                    .OrderByDescending(l => l.TimestampUtc)
                    .Take(8)
                    .ToList();

                model.RecentActivities = recentLogs.Select(l => new RecentActivityItem
                {
                    LogId = l.LogId,
                    UserId = l.UserId,
                    UserRole = l.UserRole,
                    EventType = l.EventType,
                    Details = l.Details,
                    TimestampUtc = l.TimestampUtc
                }).ToList();
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error loading dashboard metrics: " + ex.Message;
            }

            return View(model);
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
