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
    public class NotificationsController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // =====================================================================
        // GET: /Admin/Notifications
        // Full notifications list page with categories, announcements & unread count
        // =====================================================================
        [HttpGet]
        public ActionResult Index(string filter = "all")
        {
            var allNotifications = NotificationHelper.GetAdminNotifications(db, Session);
            int total = allNotifications.Count;
            int unread = allNotifications.Count(n => !n.IsRead);

            var filtered = allNotifications;
            if (string.Equals(filter, "unread", StringComparison.OrdinalIgnoreCase))
            {
                filtered = allNotifications.Where(n => !n.IsRead).ToList();
            }
            else if (string.Equals(filter, "announcements", StringComparison.OrdinalIgnoreCase))
            {
                filtered = allNotifications.Where(n => n.Category == "Announcement").ToList();
            }
            else if (string.Equals(filter, "support", StringComparison.OrdinalIgnoreCase))
            {
                filtered = allNotifications.Where(n => n.Category == "Support").ToList();
            }
            else if (string.Equals(filter, "requests", StringComparison.OrdinalIgnoreCase))
            {
                filtered = allNotifications.Where(n => n.Category == "Requests").ToList();
            }
            else if (string.Equals(filter, "assignments", StringComparison.OrdinalIgnoreCase))
            {
                filtered = allNotifications.Where(n => n.Category == "Assignments").ToList();
            }
            else if (string.Equals(filter, "security", StringComparison.OrdinalIgnoreCase))
            {
                filtered = allNotifications.Where(n => n.Category == "Security").ToList();
            }

            // Retrieve published announcements for Administrator management
            var publishedAnnouncements = NotificationHelper.GetPublishedAnnouncements(db);

            // Calculate active recipient audience counts
            int activeClients = 0;
            int activeTrainers = 0;
            int activeAdmins = 0;
            try
            {
                activeClients = db.Clients.Count(c => c.IsActive);
                activeTrainers = db.Trainers.Count(t => t.IsActive);
                activeAdmins = db.Admins.Count(a => a.IsActive);
            }
            catch { }

            var model = new AdminNotificationIndexViewModel
            {
                Notifications = filtered,
                PublishedAnnouncements = publishedAnnouncements,
                CreateAnnouncementForm = new CreateAnnouncementViewModel(),
                TotalCount = total,
                UnreadCount = unread,
                Filter = string.IsNullOrEmpty(filter) ? "all" : filter.ToLower(),
                ActiveClientsCount = activeClients,
                ActiveTrainersCount = activeTrainers,
                ActiveAdminsCount = activeAdmins
            };

            return View(model);
        }

        // =====================================================================
        // POST: /Admin/Notifications/PublishAnnouncement (FR-A-37)
        // Publishes an in-app announcement to selected roles with confirmation
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PublishAnnouncement(CreateAnnouncementViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Message))
            {
                TempData["ErrorMessage"] = "Announcement Title and Message are required and cannot be blank.";
                return RedirectToAction("Index", new { filter = "announcements" });
            }

            var targetRoles = new List<string>();
            if (model.TargetClients) targetRoles.Add("Client");
            if (model.TargetTrainers) targetRoles.Add("Trainer");
            if (model.TargetAdmins) targetRoles.Add("Admin");

            if (!targetRoles.Any())
            {
                TempData["ErrorMessage"] = "Please select at least one target recipient role (Clients, Trainers, or Administrators).";
                return RedirectToAction("Index", new { filter = "announcements" });
            }

            string currentAdminId = SessionHelper.GetCurrentAdminId(Session) ?? "ADM-DEFAULT";

            try
            {
                int deliveredCount = NotificationHelper.PublishAnnouncement(db, model.Title, model.Message, targetRoles, currentAdminId);
                TempData["SuccessMessage"] = $"Announcement \"{model.Title.Trim()}\" was published successfully and delivered to {deliveredCount} active recipient{(deliveredCount == 1 ? "" : "s")}.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to broadcast announcement: " + ex.Message;
            }

            return RedirectToAction("Index", new { filter = "announcements" });
        }

        // =====================================================================
        // POST: /Admin/Notifications/MarkAsRead (FR-S-31)
        // Marks a specific notification as read in SQL Server and Session
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAsRead(string id, string returnUrl = null)
        {
            string adminId = SessionHelper.GetCurrentAdminId(Session) ?? "ADM-DEFAULT";
            if (!string.IsNullOrEmpty(id))
            {
                NotificationHelper.MarkAsRead(db, "Admin", adminId, id, Session);
            }

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true });
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index");
        }

        // =====================================================================
        // POST: /Admin/Notifications/MarkAllAsRead (FR-S-31)
        // Marks all active admin notifications as read in SQL Server and Session
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAllAsRead(string returnUrl = null)
        {
            string adminId = SessionHelper.GetCurrentAdminId(Session) ?? "ADM-DEFAULT";
            var all = NotificationHelper.GetAdminNotifications(db, Session);
            NotificationHelper.MarkAllAsRead(db, "Admin", adminId, all.Select(n => n.Id), Session);

            TempData["SuccessMessage"] = "All system notifications have been marked as read.";

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true });
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index");
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
