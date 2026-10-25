using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using HealthTrack.Models;
using HealthTrack.ViewModels;

namespace HealthTrack.Helpers
{
    /// Simple helper for managing trainer-client messaging and conversation state.
    public static class MessagingHelper
    {
        /// Finds an existing conversation between a trainer and client, or creates one if it does not exist yet.
        public static int GetOrCreateConversation(HealthTrackEntities db, string trainerId, string clientId)
        {
            if (string.IsNullOrEmpty(trainerId) || string.IsNullOrEmpty(clientId)) return 0;

            // Check if conversation already exists
            const string findSql = "SELECT ConversationId FROM [dbo].[MessageConversations] WHERE TrainerId = @t AND ClientId = @c";
            var existingId = db.Database.SqlQuery<int?>(findSql, 
                new SqlParameter("@t", trainerId), 
                new SqlParameter("@c", clientId)
            ).FirstOrDefault();

            if (existingId.HasValue && existingId.Value > 0)
            {
                return existingId.Value;
            }

            // Create new conversation
            const string insertSql = @"
                INSERT INTO [dbo].[MessageConversations] (TrainerId, ClientId, CreatedAt)
                VALUES (@t, @c, SYSUTCDATETIME());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var newId = db.Database.SqlQuery<int>(insertSql,
                new SqlParameter("@t", trainerId),
                new SqlParameter("@c", clientId)
            ).FirstOrDefault();

            return newId;
        }

        /// Retrieves all messages in a conversation ordered chronologically.
        public static List<ChatMessageDto> GetMessages(HealthTrackEntities db, int conversationId, string currentUserId)
        {
            var list = new List<ChatMessageDto>();
            if (conversationId <= 0) return list;

            const string sql = @"
                SELECT MessageId, ConversationId, TrainerId, ClientId, SenderRole, SenderId, MessageText, SentAt, IsRead, ReadAt
                FROM [dbo].[Messages]
                WHERE ConversationId = @cid
                ORDER BY SentAt ASC";

            var rows = db.Database.SqlQuery<ChatMessageDbRow>(sql, new SqlParameter("@cid", conversationId)).ToList();

            foreach (var r in rows)
            {
                list.Add(new ChatMessageDto
                {
                    MessageId = r.MessageId,
                    ConversationId = r.ConversationId,
                    TrainerId = r.TrainerId,
                    ClientId = r.ClientId,
                    SenderRole = r.SenderRole,
                    SenderId = r.SenderId,
                    MessageText = r.MessageText,
                    SentAt = r.SentAt,
                    IsRead = r.IsRead,
                    ReadAt = r.ReadAt,
                    IsMine = string.Equals(r.SenderId, currentUserId, StringComparison.OrdinalIgnoreCase)
                });
            }

            return list;
        }

        /// Saves a new message to the database and updates the conversation timestamp.
        public static ChatMessageDto SendMessage(HealthTrackEntities db, int conversationId, string trainerId, string clientId, string senderRole, string senderId, string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText)) return null;

            const string insertSql = @"
                INSERT INTO [dbo].[Messages] (ConversationId, TrainerId, ClientId, SenderRole, SenderId, MessageText, SentAt, IsRead)
                VALUES (@cid, @t, @c, @role, @sid, @text, SYSUTCDATETIME(), 0);
                
                UPDATE [dbo].[MessageConversations]
                SET LastMessageAt = SYSUTCDATETIME()
                WHERE ConversationId = @cid;

                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var newMsgId = db.Database.SqlQuery<int>(insertSql,
                new SqlParameter("@cid", conversationId),
                new SqlParameter("@t", trainerId),
                new SqlParameter("@c", clientId),
                new SqlParameter("@role", senderRole),
                new SqlParameter("@sid", senderId),
                new SqlParameter("@text", messageText.Trim())
            ).FirstOrDefault();

            return new ChatMessageDto
            {
                MessageId = newMsgId,
                ConversationId = conversationId,
                TrainerId = trainerId,
                ClientId = clientId,
                SenderRole = senderRole,
                SenderId = senderId,
                MessageText = messageText.Trim(),
                SentAt = DateTime.UtcNow,
                IsRead = false,
                IsMine = true
            };
        }

        /// Automatically marks all unread incoming messages in the conversation as read.
        public static void MarkConversationAsRead(HealthTrackEntities db, int conversationId, string currentRole)
        {
            if (conversationId <= 0) return;

            // Only mark messages sent by the other party as read
            const string updateSql = @"
                UPDATE [dbo].[Messages]
                SET IsRead = 1, ReadAt = SYSUTCDATETIME()
                WHERE ConversationId = @cid AND SenderRole != @currentRole AND IsRead = 0";

            db.Database.ExecuteSqlCommand(updateSql,
                new SqlParameter("@cid", conversationId),
                new SqlParameter("@currentRole", currentRole)
            );
        }

        /// Counts total unread messages directed to the current user.
        public static int GetUnreadCount(HealthTrackEntities db, string userId, string role)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role)) return 0;

            if (role == "Trainer")
            {
                const string sql = "SELECT COUNT(*) FROM [dbo].[Messages] WHERE TrainerId = @uid AND SenderRole = 'Client' AND IsRead = 0";
                return db.Database.SqlQuery<int>(sql, new SqlParameter("@uid", userId)).FirstOrDefault();
            }
            else if (role == "Client")
            {
                const string sql = "SELECT COUNT(*) FROM [dbo].[Messages] WHERE ClientId = @uid AND SenderRole = 'Trainer' AND IsRead = 0";
                return db.Database.SqlQuery<int>(sql, new SqlParameter("@uid", userId)).FirstOrDefault();
            }

            return 0;
        }

        /// Internal DTO to read SQL records directly.
        private class ChatMessageDbRow
        {
            public int MessageId { get; set; }
            public int? ConversationId { get; set; }
            public string TrainerId { get; set; }
            public string ClientId { get; set; }
            public string SenderRole { get; set; }
            public string SenderId { get; set; }
            public string MessageText { get; set; }
            public DateTime SentAt { get; set; }
            public bool IsRead { get; set; }
            public DateTime? ReadAt { get; set; }
        }
    }
}
