using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using HealthTrack.Areas.Admin.ViewModels;
using HealthTrack.Areas.Client.ViewModels;
using HealthTrack.Areas.Trainer.ViewModels;
using HealthTrack.Models;

namespace HealthTrack.Helpers
{
    public static class NotificationHelper
    {
        private const string SessionReadKey = "ClientReadNotificationIds";
        private const string AdminSessionReadKey = "AdminReadNotificationIds";
        private const string TrainerSessionReadKey = "TrainerReadNotificationIds";

        #region Session Read Cache (In-Memory Fallback)

        public static HashSet<string> GetReadNotificationIds(HttpSessionStateBase session)
        {
            if (session == null) return new HashSet<string>();
            var set = session[SessionReadKey] as HashSet<string>;
            if (set == null)
            {
                set = new HashSet<string>();
                session[SessionReadKey] = set;
            }
            return set;
        }

        public static HashSet<string> GetAdminReadNotificationIds(HttpSessionStateBase session)
        {
            if (session == null) return new HashSet<string>();
            var set = session[AdminSessionReadKey] as HashSet<string>;
            if (set == null)
            {
                set = new HashSet<string>();
                session[AdminSessionReadKey] = set;
            }
            return set;
        }

        public static HashSet<string> GetTrainerReadNotificationIds(HttpSessionStateBase session)
        {
            if (session == null) return new HashSet<string>();
            var set = session[TrainerSessionReadKey] as HashSet<string>;
            if (set == null)
            {
                set = new HashSet<string>();
                session[TrainerSessionReadKey] = set;
            }
            return set;
        }

        #endregion

        #region Persistent Read State Operations (FR-S-31)

        /// <summary>
        /// Marks a specific notification as read in the database for the scoped role and recipient ID.
        /// Preserves read status permanently across login sessions.
        /// </summary>
        public static void MarkAsRead(HealthTrackEntities db, string role, string recipientUserId, string notificationId, HttpSessionStateBase session = null)
        {
            if (db == null || string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(recipientUserId) || string.IsNullOrWhiteSpace(notificationId))
            {
                return;
            }

            try
            {
                var existing = db.UserNotifications
                    .FirstOrDefault(n => n.RecipientRole == role && n.RecipientUserId == recipientUserId && n.NotificationKey == notificationId);

                if (existing != null)
                {
                    if (!existing.IsRead)
                    {
                        existing.IsRead = true;
                        existing.ReadAt = DateTime.UtcNow;
                        db.SaveChanges();
                    }
                }
                else
                {
                    // Synthesized domain notification being marked as read for the first time
                    var record = new UserNotifications
                    {
                        RecipientUserId = recipientUserId,
                        RecipientRole = role,
                        NotificationKey = notificationId,
                        Title = "Notification",
                        Message = "",
                        Category = "System",
                        NotificationType = "System",
                        ActionUrl = null,
                        Icon = "bi-bell-fill",
                        Color = "primary",
                        IsRead = true,
                        ReadAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.UserNotifications.Add(record);
                    db.SaveChanges();
                }

                // Update session in-memory cache
                if (session != null)
                {
                    if (role == "Client") GetReadNotificationIds(session).Add(notificationId);
                    else if (role == "Trainer") GetTrainerReadNotificationIds(session).Add(notificationId);
                    else if (role == "Admin") GetAdminReadNotificationIds(session).Add(notificationId);
                }
            }
            catch
            {
                // Fallback to session if database write encounters an issue
                if (session != null)
                {
                    if (role == "Client") GetReadNotificationIds(session).Add(notificationId);
                    else if (role == "Trainer") GetTrainerReadNotificationIds(session).Add(notificationId);
                    else if (role == "Admin") GetAdminReadNotificationIds(session).Add(notificationId);
                }
            }
        }

        /// <summary>
        /// Marks all specified notification IDs as read in the database for the scoped role and recipient ID.
        /// </summary>
        public static void MarkAllAsRead(HealthTrackEntities db, string role, string recipientUserId, IEnumerable<string> notificationIds, HttpSessionStateBase session = null)
        {
            if (db == null || string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(recipientUserId) || notificationIds == null)
            {
                return;
            }

            var idList = notificationIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            if (!idList.Any()) return;

            try
            {
                var existingRecords = db.UserNotifications
                    .Where(n => n.RecipientRole == role && n.RecipientUserId == recipientUserId)
                    .ToList();

                var existingKeys = new HashSet<string>(existingRecords.Select(r => r.NotificationKey));

                // Update existing
                foreach (var rec in existingRecords.Where(r => idList.Contains(r.NotificationKey)))
                {
                    if (!rec.IsRead)
                    {
                        rec.IsRead = true;
                        rec.ReadAt = DateTime.UtcNow;
                    }
                }

                // Insert missing
                foreach (var id in idList.Where(id => !existingKeys.Contains(id)))
                {
                    db.UserNotifications.Add(new UserNotifications
                    {
                        RecipientUserId = recipientUserId,
                        RecipientRole = role,
                        NotificationKey = id,
                        Title = "Notification",
                        Message = "",
                        Category = "System",
                        NotificationType = "System",
                        ActionUrl = null,
                        Icon = "bi-bell-fill",
                        Color = "primary",
                        IsRead = true,
                        ReadAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                db.SaveChanges();

                if (session != null)
                {
                    HashSet<string> set = null;
                    if (role == "Client") set = GetReadNotificationIds(session);
                    else if (role == "Trainer") set = GetTrainerReadNotificationIds(session);
                    else if (role == "Admin") set = GetAdminReadNotificationIds(session);

                    if (set != null)
                    {
                        foreach (var id in idList) set.Add(id);
                    }
                }
            }
            catch
            {
                if (session != null)
                {
                    HashSet<string> set = null;
                    if (role == "Client") set = GetReadNotificationIds(session);
                    else if (role == "Trainer") set = GetTrainerReadNotificationIds(session);
                    else if (role == "Admin") set = GetAdminReadNotificationIds(session);

                    if (set != null)
                    {
                        foreach (var id in idList) set.Add(id);
                    }
                }
            }
        }

        // Backward-compatible overloads
        public static void MarkAsRead(HttpSessionStateBase session, string notificationId)
        {
            if (session == null || string.IsNullOrEmpty(notificationId)) return;
            string clientId = SessionHelper.GetCurrentClientId(session);
            if (!string.IsNullOrEmpty(clientId))
            {
                using (var db = new HealthTrackEntities())
                {
                    MarkAsRead(db, "Client", clientId, notificationId, session);
                }
            }
            else
            {
                GetReadNotificationIds(session).Add(notificationId);
            }
        }

        public static void MarkAllAsRead(HttpSessionStateBase session, IEnumerable<string> notificationIds)
        {
            if (session == null || notificationIds == null) return;
            string clientId = SessionHelper.GetCurrentClientId(session);
            if (!string.IsNullOrEmpty(clientId))
            {
                using (var db = new HealthTrackEntities())
                {
                    MarkAllAsRead(db, "Client", clientId, notificationIds, session);
                }
            }
            else
            {
                var set = GetReadNotificationIds(session);
                foreach (var id in notificationIds) set.Add(id);
            }
        }

        public static void MarkAdminNotificationAsRead(HttpSessionStateBase session, string notificationId)
        {
            if (session == null || string.IsNullOrEmpty(notificationId)) return;
            string adminId = SessionHelper.GetCurrentAdminId(session) ?? "ADM-DEFAULT";
            using (var db = new HealthTrackEntities())
            {
                MarkAsRead(db, "Admin", adminId, notificationId, session);
            }
        }

        public static void MarkAllAdminNotificationsAsRead(HttpSessionStateBase session, IEnumerable<string> notificationIds)
        {
            if (session == null || notificationIds == null) return;
            string adminId = SessionHelper.GetCurrentAdminId(session) ?? "ADM-DEFAULT";
            using (var db = new HealthTrackEntities())
            {
                MarkAllAsRead(db, "Admin", adminId, notificationIds, session);
            }
        }

        public static void MarkTrainerNotificationAsRead(HttpSessionStateBase session, string notificationId)
        {
            if (session == null || string.IsNullOrEmpty(notificationId)) return;
            string trainerId = SessionHelper.GetCurrentTrainerId(session);
            if (!string.IsNullOrEmpty(trainerId))
            {
                using (var db = new HealthTrackEntities())
                {
                    MarkAsRead(db, "Trainer", trainerId, notificationId, session);
                }
            }
            else
            {
                GetTrainerReadNotificationIds(session).Add(notificationId);
            }
        }

        public static void MarkAllTrainerNotificationsAsRead(HttpSessionStateBase session, IEnumerable<string> notificationIds)
        {
            if (session == null || notificationIds == null) return;
            string trainerId = SessionHelper.GetCurrentTrainerId(session);
            if (!string.IsNullOrEmpty(trainerId))
            {
                using (var db = new HealthTrackEntities())
                {
                    MarkAllAsRead(db, "Trainer", trainerId, notificationIds, session);
                }
            }
            else
            {
                var set = GetTrainerReadNotificationIds(session);
                foreach (var id in notificationIds) set.Add(id);
            }
        }

        #endregion

        #region Client Notification Loading (FR-S-31 & FR-A-37)

        public static List<ClientNotificationItem> GetClientNotifications(HealthTrackEntities db, string clientId, HttpSessionStateBase session)
        {
            var list = new List<ClientNotificationItem>();
            if (string.IsNullOrEmpty(clientId) || db == null) return list;

            // 1. Load persistent records from UserNotifications for this client
            List<UserNotifications> persistedUserNotifs = new List<UserNotifications>();
            try
            {
                persistedUserNotifs = db.UserNotifications
                    .Where(n => n.RecipientRole == "Client" && n.RecipientUserId == clientId)
                    .ToList();
            }
            catch { }

            var persistedReadKeys = new HashSet<string>(persistedUserNotifs.Where(n => n.IsRead).Select(n => n.NotificationKey));
            var sessionReadKeys = GetReadNotificationIds(session);

            // Sync session with persisted read keys
            foreach (var k in persistedReadKeys) sessionReadKeys.Add(k);

            // 2. Deliver In-App Administrator Announcements (FR-A-37)
            var announcements = persistedUserNotifs.Where(n => n.NotificationType == "Announcement").ToList();
            foreach (var ann in announcements)
            {
                bool isRead = ann.IsRead || sessionReadKeys.Contains(ann.NotificationKey);
                list.Add(new ClientNotificationItem
                {
                    Id = ann.NotificationKey,
                    Title = ann.Title,
                    Message = ann.Message,
                    Category = "Announcement",
                    Timestamp = ann.CreatedAt,
                    Url = string.IsNullOrWhiteSpace(ann.ActionUrl) ? "/Client/Notifications" : ann.ActionUrl,
                    Icon = string.IsNullOrWhiteSpace(ann.Icon) ? "bi-megaphone-fill" : ann.Icon,
                    Color = string.IsNullOrWhiteSpace(ann.Color) ? "primary" : ann.Color,
                    IsRead = isRead
                });
            }

            // 2b. Deliver Automated Reminder Notifications (FR-S-26, FR-S-27, FR-S-28)
            var reminders = persistedUserNotifs.Where(n => n.NotificationType == "Reminder").ToList();
            foreach (var rem in reminders)
            {
                bool isRead = rem.IsRead || sessionReadKeys.Contains(rem.NotificationKey);
                list.Add(new ClientNotificationItem
                {
                    Id = rem.NotificationKey,
                    Title = rem.Title,
                    Message = rem.Message,
                    Category = !string.IsNullOrWhiteSpace(rem.Category) ? rem.Category : "Reminders",
                    Timestamp = rem.CreatedAt,
                    Url = string.IsNullOrWhiteSpace(rem.ActionUrl) ? "/Client/Notifications" : rem.ActionUrl,
                    Icon = string.IsNullOrWhiteSpace(rem.Icon) ? "bi-bell-fill" : rem.Icon,
                    Color = string.IsNullOrWhiteSpace(rem.Color) ? "warning" : rem.Color,
                    IsRead = isRead
                });
            }

            // 3. Trainer Feedback
            try
            {
                var feedbacks = db.TrainerFeedback
                    .Where(f => f.ClientId == clientId && !f.IsDraft)
                    .OrderByDescending(f => f.SentAt)
                    .Take(5)
                    .ToList();

                foreach (var f in feedbacks)
                {
                    string id = $"fb_{f.FeedbackId}";
                    list.Add(new ClientNotificationItem
                    {
                        Id = id,
                        Title = "Coach Feedback Received",
                        Message = string.IsNullOrWhiteSpace(f.Title) ? "Your trainer sent new guidance for your training routine." : f.Title,
                        Category = "Coach",
                        Timestamp = f.SentAt,
                        Url = "/Client/Feedback",
                        Icon = "bi-chat-heart-fill",
                        Color = "info",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            // 4. Trainer Change Requests decisions
            try
            {
                var changeRequests = db.TrainerChangeRequests
                    .Where(r => r.ClientId == clientId)
                    .OrderByDescending(r => r.RequestedAt)
                    .Take(5)
                    .ToList();

                foreach (var r in changeRequests)
                {
                    string id = $"cr_{r.RequestId}_{r.Status}";
                    bool isRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id);

                    if (r.Status == "Approved")
                    {
                        list.Add(new ClientNotificationItem
                        {
                            Id = id,
                            Title = "Coach Change Approved! 🎉",
                            Message = "Your trainer-change request has been approved and your new coach has been assigned.",
                            Category = "Requests",
                            Timestamp = r.ReviewedAt ?? r.RequestedAt,
                            Url = "/Client/TrainerChange",
                            Icon = "bi-check-circle-fill",
                            Color = "success",
                            IsRead = isRead
                        });
                    }
                    else if (r.Status == "Rejected")
                    {
                        list.Add(new ClientNotificationItem
                        {
                            Id = id,
                            Title = "Coach Change Request Update",
                            Message = string.IsNullOrWhiteSpace(r.AdminNotes) ? "Your coach-change request was reviewed and rejected by the Administrator." : $"Request rejected: \"{r.AdminNotes}\"",
                            Category = "Requests",
                            Timestamp = r.ReviewedAt ?? r.RequestedAt,
                            Url = "/Client/TrainerChange",
                            Icon = "bi-x-circle-fill",
                            Color = "danger",
                            IsRead = isRead
                        });
                    }
                    else
                    {
                        list.Add(new ClientNotificationItem
                        {
                            Id = id,
                            Title = "Coach Change Under Review",
                            Message = "Your request to change coach is currently under administrator evaluation.",
                            Category = "Requests",
                            Timestamp = r.RequestedAt,
                            Url = "/Client/TrainerChange",
                            Icon = "bi-hourglass-split",
                            Color = "warning",
                            IsRead = isRead
                        });
                    }
                }
            }
            catch { }

            // 5. Active Workout Plan
            try
            {
                var activeWorkout = db.WorkoutPlans
                    .Where(w => w.ClientId == clientId && w.IsActive)
                    .OrderByDescending(w => w.CreatedAt)
                    .FirstOrDefault();

                if (activeWorkout != null)
                {
                    string id = $"wp_{activeWorkout.WorkoutPlanId}";
                    list.Add(new ClientNotificationItem
                    {
                        Id = id,
                        Title = "Workout Routine Active",
                        Message = $"Prescribed routine: {activeWorkout.Title} ({activeWorkout.Goal}).",
                        Category = "Workouts",
                        Timestamp = activeWorkout.CreatedAt,
                        Url = "/Client/Workout",
                        Icon = "bi-lightning-charge-fill",
                        Color = "warning",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            // 6. Active Diet Plan
            try
            {
                var activeDiet = db.DietPlans
                    .Where(d => d.ClientId == clientId && d.IsActive)
                    .OrderByDescending(d => d.CreatedAt)
                    .FirstOrDefault();

                if (activeDiet != null)
                {
                    string id = $"dp_{activeDiet.DietPlanId}";
                    list.Add(new ClientNotificationItem
                    {
                        Id = id,
                        Title = "Nutrition Plan Active",
                        Message = $"Assigned diet plan: {activeDiet.Title} ({activeDiet.DailyCalorieTarget} kcal/day target).",
                        Category = "Diet",
                        Timestamp = activeDiet.CreatedAt,
                        Url = "/Client/Diet",
                        Icon = "bi-egg-fried",
                        Color = "success",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            // 7. Goals Milestones
            try
            {
                var goals = db.ClientGoals
                    .Where(g => g.ClientId == clientId && g.IsAchieved)
                    .OrderByDescending(g => g.CreatedAt)
                    .Take(3)
                    .ToList();

                foreach (var g in goals)
                {
                    string id = $"gl_{g.GoalId}";
                    list.Add(new ClientNotificationItem
                    {
                        Id = id,
                        Title = "Milestone Achieved! 🏆",
                        Message = $"Congratulations! You reached your {g.GoalName} milestone of {g.TargetValue:F1} {g.Unit}.",
                        Category = "Milestones",
                        Timestamp = g.CreatedAt,
                        Url = "/Client/Dashboard",
                        Icon = "bi-trophy-fill",
                        Color = "primary",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            // 8. Recent Progress Weigh-ins
            try
            {
                var latestWeighIn = db.ClientProgressRecords
                    .Where(p => p.ClientId == clientId)
                    .OrderByDescending(p => p.RecordedDate)
                    .FirstOrDefault();

                if (latestWeighIn != null)
                {
                    string id = $"pr_{latestWeighIn.RecordId}";
                    list.Add(new ClientNotificationItem
                    {
                        Id = id,
                        Title = "Weight Recorded",
                        Message = $"Latest weigh-in of {latestWeighIn.WeightKg:F1} kg recorded for {latestWeighIn.RecordedDate:MMM dd}.",
                        Category = "Biometrics",
                        Timestamp = latestWeighIn.CreatedAt,
                        Url = "/Client/Dashboard",
                        Icon = "bi-speedometer2",
                        Color = "primary",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            // 9. Appointments & Reminders
            try
            {
                var appointments = db.Appointments
                    .Where(a => a.ClientId == clientId)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(5)
                    .ToList();

                foreach (var a in appointments)
                {
                    string id = $"apt_{a.AppointmentId}_{a.Status}";
                    string title = a.Status == "Approved"
                        ? "Appointment Confirmed! 📅"
                        : (a.Status == "Rejected" ? "Appointment Declined" : (a.Status == "Cancelled" ? "Appointment Cancelled" : "Appointment Requested"));
                    string color = a.Status == "Approved" ? "success" : (a.Status == "Rejected" || a.Status == "Cancelled" ? "danger" : "warning");
                    string msg = a.Status == "Approved"
                        ? $"Your coach approved session '{a.Title}' for {a.AppointmentDate:MMM dd} ({DateTime.Today.Add(a.StartTime):hh:mm tt}). Location: {a.MeetingLocation}"
                        : (a.Status == "Rejected" ? $"Your coach declined session '{a.Title}': {a.CancellationReason}" : $"Consultation session '{a.Title}' requested for {a.AppointmentDate:MMM dd}.");

                    list.Add(new ClientNotificationItem
                    {
                        Id = id,
                        Title = title,
                        Message = msg,
                        Category = "Appointments",
                        Timestamp = a.UpdatedAt ?? a.CreatedAt,
                        Url = "/Client/Appointments",
                        Icon = "bi-calendar2-check-fill",
                        Color = color,
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            // Sort all by most recent first
            return list.OrderByDescending(n => n.Timestamp).ToList();
        }

        #endregion

        #region Trainer Notification Loading (FR-S-31 & FR-A-37)

        public static List<TrainerNotificationItem> GetTrainerNotifications(HealthTrackEntities db, string trainerId, HttpSessionStateBase session)
        {
            var list = new List<TrainerNotificationItem>();
            if (string.IsNullOrEmpty(trainerId) || db == null) return list;

            List<UserNotifications> persistedUserNotifs = new List<UserNotifications>();
            try
            {
                persistedUserNotifs = db.UserNotifications
                    .Where(n => n.RecipientRole == "Trainer" && n.RecipientUserId == trainerId)
                    .ToList();
            }
            catch { }

            var persistedReadKeys = new HashSet<string>(persistedUserNotifs.Where(n => n.IsRead).Select(n => n.NotificationKey));
            var sessionReadKeys = GetTrainerReadNotificationIds(session);

            foreach (var k in persistedReadKeys) sessionReadKeys.Add(k);

            // In-App Announcements for Trainers (FR-A-37)
            var announcements = persistedUserNotifs.Where(n => n.NotificationType == "Announcement").ToList();
            foreach (var ann in announcements)
            {
                bool isRead = ann.IsRead || sessionReadKeys.Contains(ann.NotificationKey);
                list.Add(new TrainerNotificationItem
                {
                    Id = ann.NotificationKey,
                    Title = ann.Title,
                    Message = ann.Message,
                    Category = "Announcement",
                    Icon = string.IsNullOrWhiteSpace(ann.Icon) ? "bi-megaphone-fill" : ann.Icon,
                    Color = string.IsNullOrWhiteSpace(ann.Color) ? "primary" : ann.Color,
                    Timestamp = ann.CreatedAt,
                    ActionUrl = string.IsNullOrWhiteSpace(ann.ActionUrl) ? "/Trainer/Dashboard" : ann.ActionUrl,
                    IsRead = isRead
                });
            }

            // 1. Pending appointment requests
            try
            {
                var pendingAppointments = db.Appointments
                    .Where(a => a.TrainerId == trainerId && a.Status == "Pending")
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(10)
                    .ToList();

                foreach (var a in pendingAppointments)
                {
                    string id = $"appt_pending_{a.AppointmentId}";
                    string clientName = a.Clients != null ? a.Clients.FullName : "Client";
                    string timeStr = a.StartTime.ToString(@"hh\:mm");
                    list.Add(new TrainerNotificationItem
                    {
                        Id = id,
                        Title = "New Booking Request",
                        Message = $"{clientName} requested an appointment on {a.AppointmentDate:MMM dd, yyyy} at {timeStr}.",
                        Category = "Appointments",
                        Icon = "bi-calendar-plus-fill",
                        Color = "warning",
                        Timestamp = a.CreatedAt,
                        ActionUrl = "/Trainer/Appointments",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            // 2. Upcoming approved sessions in next 2 days
            try
            {
                var today = DateTime.Today;
                var upcomingEnd = today.AddDays(2);
                var upcomingAppointments = db.Appointments
                    .Where(a => a.TrainerId == trainerId && a.Status == "Approved" && a.AppointmentDate >= today && a.AppointmentDate <= upcomingEnd)
                    .OrderBy(a => a.AppointmentDate)
                    .ThenBy(a => a.StartTime)
                    .Take(10)
                    .ToList();

                foreach (var a in upcomingAppointments)
                {
                    string id = $"appt_upcoming_{a.AppointmentId}";
                    string clientName = a.Clients != null ? a.Clients.FullName : "Client";
                    string timeStr = a.StartTime.ToString(@"hh\:mm");
                    list.Add(new TrainerNotificationItem
                    {
                        Id = id,
                        Title = "Upcoming Training Session",
                        Message = $"Confirmed session with {clientName} on {a.AppointmentDate:MMM dd} at {timeStr}.",
                        Category = "Appointments",
                        Icon = "bi-alarm-fill",
                        Color = "info",
                        Timestamp = a.UpdatedAt ?? a.CreatedAt,
                        ActionUrl = "/Trainer/Appointments",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            // 3. New clients assigned
            try
            {
                var assignedClients = db.Clients
                    .Where(c => c.AssignedTrainerId == trainerId && c.IsActive)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(8)
                    .ToList();

                foreach (var c in assignedClients)
                {
                    string id = $"client_assigned_{c.ClientId}";
                    list.Add(new TrainerNotificationItem
                    {
                        Id = id,
                        Title = "New Member Assigned",
                        Message = $"{c.FullName} ({c.Email}) is assigned to your client roster.",
                        Category = "Clients",
                        Icon = "bi-person-check-fill",
                        Color = "success",
                        Timestamp = c.CreatedAt,
                        ActionUrl = "/Trainer/Clients",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            // 4. Client workout logs
            try
            {
                var recentLogs = db.ClientWorkoutLogs
                    .Where(w => w.Clients != null && w.Clients.AssignedTrainerId == trainerId)
                    .OrderByDescending(w => w.LogDate)
                    .Take(10)
                    .ToList();

                foreach (var w in recentLogs)
                {
                    string id = $"workout_log_{w.LogId}";
                    string clientName = w.Clients != null ? w.Clients.FullName : "Client";
                    string sessionTitle = string.IsNullOrWhiteSpace(w.SessionName) ? "Workout Session" : w.SessionName;
                    list.Add(new TrainerNotificationItem
                    {
                        Id = id,
                        Title = "Client Finished Workout",
                        Message = $"{clientName} logged '{sessionTitle}' ({w.DurationMinutes} min, {w.CaloriesBurned} kcal).",
                        Category = "Workouts",
                        Icon = "bi-activity",
                        Color = "primary",
                        Timestamp = w.LogDate,
                        ActionUrl = "/Trainer/Clients",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            // 5. Transfer requests
            try
            {
                var changeRequests = db.TrainerChangeRequests
                    .Where(r => r.CurrentTrainerId == trainerId && r.Status == "Pending")
                    .OrderByDescending(r => r.RequestedAt)
                    .Take(5)
                    .ToList();

                foreach (var r in changeRequests)
                {
                    string id = $"trainer_change_{r.RequestId}";
                    string clientName = r.Clients != null ? r.Clients.FullName : "Client";
                    list.Add(new TrainerNotificationItem
                    {
                        Id = id,
                        Title = "Client Transfer Notice",
                        Message = $"{clientName} requested coach reassignment: {r.ReasonCategory ?? "Coach change requested"}.",
                        Category = "Clients",
                        Icon = "bi-arrow-left-right",
                        Color = "secondary",
                        Timestamp = r.RequestedAt,
                        ActionUrl = "/Trainer/Clients",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            return list.OrderByDescending(n => n.Timestamp).ToList();
        }

        #endregion

        #region Admin Notification Loading (FR-S-31 & FR-A-37)

        public static List<AdminNotificationItemViewModel> GetAdminNotifications(HealthTrackEntities db, HttpSessionStateBase session)
        {
            var list = new List<AdminNotificationItemViewModel>();
            if (db == null) return list;

            string adminId = SessionHelper.GetCurrentAdminId(session) ?? "ADM-DEFAULT";

            List<UserNotifications> persistedUserNotifs = new List<UserNotifications>();
            try
            {
                persistedUserNotifs = db.UserNotifications
                    .Where(n => n.RecipientRole == "Admin" && n.RecipientUserId == adminId)
                    .ToList();
            }
            catch { }

            var persistedReadKeys = new HashSet<string>(persistedUserNotifs.Where(n => n.IsRead).Select(n => n.NotificationKey));
            var sessionReadKeys = GetAdminReadNotificationIds(session);

            foreach (var k in persistedReadKeys) sessionReadKeys.Add(k);

            // In-App Announcements for Admins (FR-A-37)
            var announcements = persistedUserNotifs.Where(n => n.NotificationType == "Announcement").ToList();
            foreach (var ann in announcements)
            {
                bool isRead = ann.IsRead || sessionReadKeys.Contains(ann.NotificationKey);
                list.Add(new AdminNotificationItemViewModel
                {
                    Id = ann.NotificationKey,
                    Title = ann.Title,
                    Message = ann.Message,
                    Category = "Announcement",
                    Icon = string.IsNullOrWhiteSpace(ann.Icon) ? "bi-megaphone-fill" : ann.Icon,
                    Color = string.IsNullOrWhiteSpace(ann.Color) ? "primary" : ann.Color,
                    Timestamp = ann.CreatedAt,
                    ActionUrl = "/Admin/Notifications?filter=announcements",
                    IsRead = isRead
                });
            }

            // 1. Support Tickets
            try
            {
                var openTickets = db.SupportTickets
                    .Where(t => t.Status == "Open")
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(10)
                    .ToList();

                foreach (var t in openTickets)
                {
                    string id = $"ticket_{t.TicketId}";
                    string clientName = t.Clients != null ? t.Clients.FullName : "Client";
                    list.Add(new AdminNotificationItemViewModel
                    {
                        Id = id,
                        Title = $"Open Support Ticket #{t.TicketId}",
                        Message = $"{clientName}: {t.Subject} ({t.Priority} Priority)",
                        Category = "Support",
                        Icon = "bi-headset",
                        Color = t.Priority == "High" || t.Priority == "Urgent" ? "danger" : "warning",
                        Timestamp = t.CreatedAt,
                        ActionUrl = "/Admin/Support",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            // 2. Pending Coach Change Requests
            try
            {
                var pendingRequests = db.TrainerChangeRequests
                    .Where(r => r.Status == "Pending")
                    .OrderByDescending(r => r.RequestedAt)
                    .Take(10)
                    .ToList();

                foreach (var r in pendingRequests)
                {
                    string id = $"cr_{r.RequestId}";
                    string clientName = r.Clients != null ? r.Clients.FullName : "Client";
                    list.Add(new AdminNotificationItemViewModel
                    {
                        Id = id,
                        Title = "Coach Change Request",
                        Message = $"{clientName} requested reassignment: {r.ReasonCategory ?? r.ReasonDetails ?? "New trainer requested"}",
                        Category = "Requests",
                        Icon = "bi-arrow-left-right",
                        Color = "info",
                        Timestamp = r.RequestedAt,
                        ActionUrl = "/Admin/Assignments/Requests",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            // 3. Unassigned Active Clients
            try
            {
                var unassigned = db.Clients
                    .Where(c => c.IsActive && string.IsNullOrEmpty(c.AssignedTrainerId))
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(5)
                    .ToList();

                foreach (var c in unassigned)
                {
                    string id = $"unassigned_{c.ClientId}";
                    list.Add(new AdminNotificationItemViewModel
                    {
                        Id = id,
                        Title = "Unassigned Member",
                        Message = $"{c.FullName} ({c.Email}) is waiting for trainer assignment.",
                        Category = "Assignments",
                        Icon = "bi-person-plus-fill",
                        Color = "primary",
                        Timestamp = c.CreatedAt,
                        ActionUrl = "/Admin/Assignments",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            // 4. Security Events / Account Alerts
            try
            {
                var securityLogs = db.AuthAuditLogs
                    .Where(l => l.EventType == "ACCOUNT_LOCKED" || l.EventType == "CLIENT_DEACTIVATED" || l.EventType == "LOGIN_FAILED")
                    .OrderByDescending(l => l.TimestampUtc)
                    .Take(5)
                    .ToList();

                foreach (var l in securityLogs)
                {
                    string id = $"sec_{l.LogId}";
                    string title = l.EventType == "ACCOUNT_LOCKED" ? "Account Locked Alert" :
                                   (l.EventType == "CLIENT_DEACTIVATED" ? "Member Deactivated" : "Failed Login Attempt");
                    list.Add(new AdminNotificationItemViewModel
                    {
                        Id = id,
                        Title = title,
                        Message = $"{l.Details} (User: {l.UserId})",
                        Category = "Security",
                        Icon = "bi-shield-exclamation",
                        Color = "danger",
                        Timestamp = l.TimestampUtc.ToLocalTime(),
                        ActionUrl = "/Admin/AuditLogs",
                        IsRead = persistedReadKeys.Contains(id) || sessionReadKeys.Contains(id)
                    });
                }
            }
            catch { }

            return list.OrderByDescending(n => n.Timestamp).ToList();
        }

        #endregion

        #region Administrator Announcement Publishing (FR-A-37)

        /// <summary>
        /// Publishes an in-app announcement to the selected user roles and creates independent recipient notifications.
        /// Returns the total number of notifications delivered.
        /// </summary>
        public static int PublishAnnouncement(HealthTrackEntities db, string title, string message, List<string> targetRoles, string adminId)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title cannot be empty.", nameof(title));
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message cannot be empty.", nameof(message));
            if (targetRoles == null || !targetRoles.Any()) throw new ArgumentException("At least one target role must be selected.", nameof(targetRoles));

            string sanitizedTitle = title.Trim();
            string sanitizedMessage = message.Trim();
            if (sanitizedTitle.Length > 200) sanitizedTitle = sanitizedTitle.Substring(0, 200);
            if (sanitizedMessage.Length > 4000) sanitizedMessage = sanitizedMessage.Substring(0, 4000);

            string validAdminId = adminId;
            if (string.IsNullOrWhiteSpace(validAdminId))
            {
                var defaultAdmin = db.Admins.FirstOrDefault(a => a.IsActive);
                validAdminId = defaultAdmin != null ? defaultAdmin.AdminId : "ADM-DEFAULT";
            }

            // 1. Create master announcement record
            var announcement = new AdminAnnouncements
            {
                Title = sanitizedTitle,
                Message = sanitizedMessage,
                TargetRoles = string.Join(",", targetRoles.Distinct()),
                CreatedByAdminId = validAdminId,
                CreatedAt = DateTime.UtcNow
            };

            db.AdminAnnouncements.Add(announcement);
            db.SaveChanges(); // Persist master record to obtain AnnouncementId

            int deliveredCount = 0;
            var newNotifications = new List<UserNotifications>();
            string notifKey = $"ann_{announcement.AnnouncementId}";

            // 2. Deliver to active Clients
            if (targetRoles.Contains("Client"))
            {
                var activeClients = db.Clients.Where(c => c.IsActive).Select(c => c.ClientId).ToList();
                foreach (var cId in activeClients)
                {
                    newNotifications.Add(new UserNotifications
                    {
                        RecipientUserId = cId,
                        RecipientRole = "Client",
                        NotificationKey = notifKey,
                        Title = $"Announcement: {sanitizedTitle}",
                        Message = sanitizedMessage,
                        Category = "Announcement",
                        NotificationType = "Announcement",
                        ActionUrl = "/Client/Notifications?filter=announcements",
                        Icon = "bi-megaphone-fill",
                        Color = "primary",
                        IsRead = false,
                        ReadAt = null,
                        CreatedAt = DateTime.UtcNow,
                        AnnouncementId = announcement.AnnouncementId
                    });
                    deliveredCount++;
                }
            }

            // 3. Deliver to active Trainers
            if (targetRoles.Contains("Trainer"))
            {
                var activeTrainers = db.Trainers.Where(t => t.IsActive).Select(t => t.TrainerId).ToList();
                foreach (var tId in activeTrainers)
                {
                    newNotifications.Add(new UserNotifications
                    {
                        RecipientUserId = tId,
                        RecipientRole = "Trainer",
                        NotificationKey = notifKey,
                        Title = $"Announcement: {sanitizedTitle}",
                        Message = sanitizedMessage,
                        Category = "Announcement",
                        NotificationType = "Announcement",
                        ActionUrl = "/Trainer/Dashboard",
                        Icon = "bi-megaphone-fill",
                        Color = "primary",
                        IsRead = false,
                        ReadAt = null,
                        CreatedAt = DateTime.UtcNow,
                        AnnouncementId = announcement.AnnouncementId
                    });
                    deliveredCount++;
                }
            }

            // 4. Deliver to active Administrators
            if (targetRoles.Contains("Admin"))
            {
                var activeAdmins = db.Admins.Where(a => a.IsActive).Select(a => a.AdminId).ToList();
                foreach (var aId in activeAdmins)
                {
                    newNotifications.Add(new UserNotifications
                    {
                        RecipientUserId = aId,
                        RecipientRole = "Admin",
                        NotificationKey = notifKey,
                        Title = $"System Broadcast: {sanitizedTitle}",
                        Message = sanitizedMessage,
                        Category = "Announcement",
                        NotificationType = "Announcement",
                        ActionUrl = "/Admin/Notifications?filter=announcements",
                        Icon = "bi-megaphone-fill",
                        Color = "primary",
                        IsRead = false,
                        ReadAt = null,
                        CreatedAt = DateTime.UtcNow,
                        AnnouncementId = announcement.AnnouncementId
                    });
                    deliveredCount++;
                }
            }

            if (newNotifications.Any())
            {
                db.UserNotifications.AddRange(newNotifications);
                db.SaveChanges();
            }

            return deliveredCount;
        }

        /// <summary>
        /// Retrieves published announcements with author information and delivery statistics for Administrator auditing.
        /// </summary>
        public static List<AnnouncementListItemViewModel> GetPublishedAnnouncements(HealthTrackEntities db)
        {
            var list = new List<AnnouncementListItemViewModel>();
            if (db == null) return list;

            try
            {
                var records = db.AdminAnnouncements
                    .Include(a => a.Admins)
                    .Include(a => a.UserNotifications)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(30)
                    .ToList();

                foreach (var item in records)
                {
                    string authorName = item.Admins != null ? item.Admins.FullName : "System Administrator";
                    int delivered = item.UserNotifications != null ? item.UserNotifications.Count : 0;
                    int read = item.UserNotifications != null ? item.UserNotifications.Count(n => n.IsRead) : 0;

                    list.Add(new AnnouncementListItemViewModel
                    {
                        AnnouncementId = item.AnnouncementId,
                        Title = item.Title,
                        Message = item.Message,
                        TargetRoles = item.TargetRoles,
                        CreatedByAdminId = item.CreatedByAdminId,
                        CreatedByAdminName = authorName,
                        CreatedAt = item.CreatedAt,
                        TotalDelivered = delivered,
                        ReadCount = read
                    });
                }
            }
            catch { }

            return list;
        }

        #endregion
    }
}
