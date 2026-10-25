using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.ViewModels;

namespace HealthTrack.Controllers
{
    /// Handles lightweight chat actions between assigned clients and trainers.
    public class ChatController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // GET: /Chat/GetChatState
        // Loads active conversation, partner details, and recent messages
        [HttpGet]
        public ActionResult GetChatState(string targetClientId = null)
        {
            var sessionUser = GetCurrentSessionUser();
            if (sessionUser == null)
            {
                return Json(new { success = false, message = "User is not authenticated." }, JsonRequestBehavior.AllowGet);
            }

            // CLIENT MODE: Can only chat with their assigned trainer
            if (sessionUser.Role == "Client")
            {
                var client = db.Clients.FirstOrDefault(c => c.ClientId == sessionUser.UserId);
                if (client == null || string.IsNullOrEmpty(client.AssignedTrainerId))
                {
                    return Json(new
                    {
                        success = true,
                        state = new ChatStateDto
                        {
                            IsConfigured = false,
                            NoticeMessage = "You do not have an assigned coach yet. You can chat here once a trainer is assigned."
                        }
                    }, JsonRequestBehavior.AllowGet);
                }

                var trainer = db.Trainers.FirstOrDefault(t => t.TrainerId == client.AssignedTrainerId);
                if (trainer == null)
                {
                    return Json(new
                    {
                        success = true,
                        state = new ChatStateDto
                        {
                            IsConfigured = false,
                            NoticeMessage = "Your assigned coach is currently inactive."
                        }
                    }, JsonRequestBehavior.AllowGet);
                }

                int conversationId = MessagingHelper.GetOrCreateConversation(db, trainer.TrainerId, client.ClientId);
                MessagingHelper.MarkConversationAsRead(db, conversationId, "Client");

                var messages = MessagingHelper.GetMessages(db, conversationId, client.ClientId);
                int unread = MessagingHelper.GetUnreadCount(db, client.ClientId, "Client");

                var state = new ChatStateDto
                {
                    IsConfigured = true,
                    ConversationId = conversationId,
                    ActivePartner = new ChatPartnerDto
                    {
                        Id = trainer.TrainerId,
                        Name = trainer.FullName,
                        Role = "Trainer",
                        Subtitle = string.IsNullOrEmpty(trainer.Specialization) ? "Personal Coach" : trainer.Specialization,
                        AvatarUrl = trainer.ProfilePicturePath
                    },
                    Messages = messages,
                    UnreadCount = unread
                };

                return Json(new { success = true, state = state }, JsonRequestBehavior.AllowGet);
            }

            // TRAINER MODE: Can chat with assigned clients
            if (sessionUser.Role == "Trainer")
            {
                var assignedClients = db.Clients
                    .Where(c => c.AssignedTrainerId == sessionUser.UserId && c.IsActive)
                    .OrderBy(c => c.FullName)
                    .ToList();

                if (!assignedClients.Any())
                {
                    return Json(new
                    {
                        success = true,
                        state = new ChatStateDto
                        {
                            IsConfigured = false,
                            NoticeMessage = "You have no assigned clients on your roster yet."
                        }
                    }, JsonRequestBehavior.AllowGet);
                }

                // Choose target client: use requested targetClientId or pick the first assigned client
                var activeClient = !string.IsNullOrWhiteSpace(targetClientId)
                    ? assignedClients.FirstOrDefault(c => c.ClientId == targetClientId)
                    : assignedClients.FirstOrDefault();

                if (activeClient == null)
                {
                    if (!string.IsNullOrWhiteSpace(targetClientId))
                    {
                        return Json(new { success = false, message = "Access restricted: Selected client is not currently assigned to you." }, JsonRequestBehavior.AllowGet);
                    }
                    return Json(new
                    {
                        success = true,
                        state = new ChatStateDto
                        {
                            IsConfigured = false,
                            NoticeMessage = "You have no assigned clients on your roster yet."
                        }
                    }, JsonRequestBehavior.AllowGet);
                }

                int conversationId = MessagingHelper.GetOrCreateConversation(db, sessionUser.UserId, activeClient.ClientId);
                MessagingHelper.MarkConversationAsRead(db, conversationId, "Trainer");

                var messages = MessagingHelper.GetMessages(db, conversationId, sessionUser.UserId);
                int unread = MessagingHelper.GetUnreadCount(db, sessionUser.UserId, "Trainer");

                var partnerList = assignedClients.Select(c => new ChatPartnerDto
                {
                    Id = c.ClientId,
                    Name = c.FullName,
                    Role = "Client",
                    Subtitle = c.Email
                }).ToList();

                var state = new ChatStateDto
                {
                    IsConfigured = true,
                    ConversationId = conversationId,
                    ActivePartner = new ChatPartnerDto
                    {
                        Id = activeClient.ClientId,
                        Name = activeClient.FullName,
                        Role = "Client",
                        Subtitle = activeClient.Email
                    },
                    AvailablePartners = partnerList,
                    Messages = messages,
                    UnreadCount = unread
                };

                return Json(new { success = true, state = state }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { success = false, message = "Role not supported for chat." }, JsonRequestBehavior.AllowGet);
        }

        // POST: /Chat/SendMessage (FR-S-11)
        // Saves a new chat message and returns the sent record
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendMessage(string targetClientId, string messageText)
        {
            var sessionUser = GetCurrentSessionUser();
            if (sessionUser == null)
            {
                return Json(new { success = false, message = "User is not authenticated." });
            }

            if (string.IsNullOrWhiteSpace(messageText))
            {
                return Json(new { success = false, message = "Message text cannot be empty." });
            }

            if (sessionUser.Role == "Client")
            {
                var client = db.Clients.FirstOrDefault(c => c.ClientId == sessionUser.UserId && c.IsActive);
                if (client == null || string.IsNullOrEmpty(client.AssignedTrainerId))
                {
                    return Json(new { success = false, message = "No assigned trainer found." });
                }

                int conversationId = MessagingHelper.GetOrCreateConversation(db, client.AssignedTrainerId, client.ClientId);
                var sentMsg = MessagingHelper.SendMessage(db, conversationId, client.AssignedTrainerId, client.ClientId, "Client", client.ClientId, messageText);

                return Json(new { success = true, message = sentMsg });
            }

            if (sessionUser.Role == "Trainer")
            {
                if (string.IsNullOrEmpty(targetClientId))
                {
                    return Json(new { success = false, message = "Target client must be specified." });
                }

                // Verify client belongs to this trainer and is actively assigned
                bool isAssigned = db.Clients.Any(c => c.ClientId == targetClientId && c.AssignedTrainerId == sessionUser.UserId && c.IsActive);
                if (!isAssigned)
                {
                    return Json(new { success = false, message = "This client is not currently assigned to you." });
                }

                int conversationId = MessagingHelper.GetOrCreateConversation(db, sessionUser.UserId, targetClientId);
                var sentMsg = MessagingHelper.SendMessage(db, conversationId, sessionUser.UserId, targetClientId, "Trainer", sessionUser.UserId, messageText);

                return Json(new { success = true, message = sentMsg });
            }

            return Json(new { success = false, message = "Invalid role." });
        }

        // GET: /Chat/GetUnreadCount
        // Returns unread badge count for the floating chat button
        [HttpGet]
        public ActionResult GetUnreadCount()
        {
            var sessionUser = GetCurrentSessionUser();
            if (sessionUser == null)
            {
                return Json(new { unreadCount = 0 }, JsonRequestBehavior.AllowGet);
            }

            int unread = MessagingHelper.GetUnreadCount(db, sessionUser.UserId, sessionUser.Role);
            return Json(new { unreadCount = unread }, JsonRequestBehavior.AllowGet);
        }

        // POST: /Chat/MarkAsRead (FR-S-11)
        // Marks incoming messages in a conversation as read
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAsRead(int conversationId)
        {
            var sessionUser = GetCurrentSessionUser();
            if (sessionUser != null && conversationId > 0)
            {
                const string verifySql = "SELECT COUNT(*) FROM [dbo].[MessageConversations] WHERE ConversationId = @cid AND (TrainerId = @uid OR ClientId = @uid)";
                int count = db.Database.SqlQuery<int>(verifySql, new SqlParameter("@cid", conversationId), new SqlParameter("@uid", sessionUser.UserId)).FirstOrDefault();
                if (count > 0)
                {
                    MessagingHelper.MarkConversationAsRead(db, conversationId, sessionUser.Role);
                }
            }
            return Json(new { success = true });
        }

        /// Helper to determine the current session user ID and role
        private SessionUserInfo GetCurrentSessionUser()
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);
            if (!string.IsNullOrEmpty(clientId))
            {
                return new SessionUserInfo { UserId = clientId, Role = "Client" };
            }

            string trainerId = SessionHelper.GetCurrentTrainerId(Session);
            if (!string.IsNullOrEmpty(trainerId))
            {
                return new SessionUserInfo { UserId = trainerId, Role = "Trainer" };
            }

            return null;
        }

        private class SessionUserInfo
        {
            public string UserId { get; set; }
            public string Role { get; set; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
