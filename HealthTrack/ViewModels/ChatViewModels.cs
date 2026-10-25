using System;
using System.Collections.Generic;

namespace HealthTrack.ViewModels
{
    /// Data transfer object for a single chat message.
    public class ChatMessageDto
    {
        public int MessageId { get; set; }
        public int? ConversationId { get; set; }
        public string TrainerId { get; set; }
        public string ClientId { get; set; }
        public string SenderRole { get; set; } // "Trainer" or "Client"
        public string SenderId { get; set; }
        public string MessageText { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsMine { get; set; }

        /// Relative timestamp for message bubble such as "just now", "10s ago", "2h ago".
        public string TimeAgo
        {
            get
            {
                var utc = SentAt.Kind == DateTimeKind.Utc 
                    ? SentAt 
                    : DateTime.SpecifyKind(SentAt, DateTimeKind.Utc);
                var span = DateTime.UtcNow - utc;
                if (span.TotalSeconds < 0) span = TimeSpan.Zero;
                if (span.TotalSeconds < 30) return "just now";
                if (span.TotalMinutes < 1) return $"{(int)span.TotalSeconds}s ago";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
                return utc.ToLocalTime().ToString("MMM dd, hh:mm tt");
            }
        }
    }

    /// Information about the person on the other end of the conversation.
    public class ChatPartnerDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; } // "Trainer" or "Client"
        public string AvatarUrl { get; set; }
        public string Subtitle { get; set; }
    }

    /// Full state of the chat widget when opened.
    public class ChatStateDto
    {
        public bool IsConfigured { get; set; } // false if client has no assigned trainer
        public string NoticeMessage { get; set; }
        public int ConversationId { get; set; }
        public ChatPartnerDto ActivePartner { get; set; }
        public List<ChatPartnerDto> AvailablePartners { get; set; } = new List<ChatPartnerDto>(); // for trainer: list of assigned clients
        public List<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();
        public int UnreadCount { get; set; }
    }

    /// Request payload when sending a new message.
    public class SendMessageRequest
    {
        public string ClientId { get; set; }
        public string TrainerId { get; set; }
        public string MessageText { get; set; }
    }
}
