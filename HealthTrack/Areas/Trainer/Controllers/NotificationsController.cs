using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Trainer.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.Controllers
{
    [TrainerAuthorize]
    public class NotificationsController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // POST: /Trainer/Notifications/MarkAsRead
        // Marks a specific notification as read in database and session
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAsRead(string id, string returnUrl = null)
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (!string.IsNullOrEmpty(trainerId) && !string.IsNullOrEmpty(id))
            {
                NotificationHelper.MarkAsRead(db, "Trainer", trainerId, id, Session);
            }

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true });
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard", new { area = "Trainer" });
        }

        // POST: /Trainer/Notifications/MarkAllAsRead
        // Marks all active notifications as read in database and session
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAllAsRead(string returnUrl = null)
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (!string.IsNullOrEmpty(trainerId))
            {
                var all = NotificationHelper.GetTrainerNotifications(db, trainerId, Session);
                NotificationHelper.MarkAllAsRead(db, "Trainer", trainerId, all.Select(n => n.Id), Session);
            }

            TempData["SuccessMessage"] = "All notifications marked as read.";

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true });
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard", new { area = "Trainer" });
        }

        // GET: /Trainer/Notifications/GetSummaryJson
        // Returns unread count and latest 5 notifications for navbar preview
        [HttpGet]
        public ActionResult GetSummaryJson()
        {
            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (string.IsNullOrEmpty(trainerId))
            {
                return Json(new { unreadCount = 0, items = new object[0] }, JsonRequestBehavior.AllowGet);
            }

            var all = NotificationHelper.GetTrainerNotifications(db, trainerId, Session);
            var unreadCount = all.Count(n => !n.IsRead);
            var previewItems = all.Take(5).Select(n => new
            {
                id = n.Id,
                title = n.Title,
                message = n.Message,
                category = n.Category,
                timeAgo = n.TimeAgo,
                url = n.ActionUrl,
                icon = n.Icon,
                color = n.Color,
                isRead = n.IsRead
            }).ToList();

            return Json(new
            {
                unreadCount = unreadCount,
                items = previewItems
            }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
