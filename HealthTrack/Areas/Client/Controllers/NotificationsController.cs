using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Client.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Client.Controllers
{
    [ClientAuthorize]
    public class NotificationsController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // GET: /Client/Notifications
        [HttpGet]
        public ActionResult Index(string filter = "all")
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            var allNotifications = NotificationHelper.GetClientNotifications(db, clientId, Session);
            int total = allNotifications.Count;
            int unread = allNotifications.Count(n => !n.IsRead);

            var filtered = allNotifications;
            if (string.Equals(filter, "unread", StringComparison.OrdinalIgnoreCase))
            {
                filtered = allNotifications.Where(n => !n.IsRead).ToList();
            }
            else if (string.Equals(filter, "coach", StringComparison.OrdinalIgnoreCase))
            {
                filtered = allNotifications.Where(n => n.Category == "Coach").ToList();
            }
            else if (string.Equals(filter, "requests", StringComparison.OrdinalIgnoreCase))
            {
                filtered = allNotifications.Where(n => n.Category == "Requests").ToList();
            }
            else if (string.Equals(filter, "plans", StringComparison.OrdinalIgnoreCase))
            {
                filtered = allNotifications.Where(n => n.Category == "Workouts" || n.Category == "Diet").ToList();
            }
            else if (string.Equals(filter, "announcements", StringComparison.OrdinalIgnoreCase))
            {
                filtered = allNotifications.Where(n => n.Category == "Announcement").ToList();
            }

            var model = new ClientNotificationsPageViewModel
            {
                TotalCount = total,
                UnreadCount = unread,
                ActiveFilter = filter.ToLower(),
                Notifications = filtered
            };

            return View(model);
        }

        // POST: /Client/Notifications/MarkAsRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAsRead(string id, string returnUrl = null)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(id))
            {
                NotificationHelper.MarkAsRead(db, "Client", clientId, id, Session);
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

        // POST: /Client/Notifications/MarkAllAsRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAllAsRead(string returnUrl = null)
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (!string.IsNullOrEmpty(clientId))
            {
                var allNotifications = NotificationHelper.GetClientNotifications(db, clientId, Session);
                NotificationHelper.MarkAllAsRead(db, "Client", clientId, allNotifications.Select(n => n.Id), Session);
            }

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true });
            }

            TempData["SuccessMessage"] = "All notifications marked as read.";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index");
        }

        // GET: /Client/Notifications/GetSummaryJson
        [HttpGet]
        public ActionResult GetSummaryJson()
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrEmpty(clientId))
            {
                return Json(new { unreadCount = 0, items = new object[0] }, JsonRequestBehavior.AllowGet);
            }

            var all = NotificationHelper.GetClientNotifications(db, clientId, Session);
            var unreadCount = all.Count(n => !n.IsRead);
            var previewItems = all.Take(5).Select(n => new
            {
                id = n.Id,
                title = n.Title,
                message = n.Message,
                category = n.Category,
                timeAgo = n.TimeAgo,
                url = n.Url,
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
