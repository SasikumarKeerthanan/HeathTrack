using System;
using System.Collections.Generic;

namespace HealthTrack.Areas.Admin.ViewModels
{
    public class AdminAuditLogsIndexViewModel
    {
        /// <summary>
        /// Log view mode: "health" (FR-S-20 Health Record Audit Logs) or "security" (FR-S-05/11 Auth Audit Logs)
        /// </summary>
        public string LogType { get; set; } = "health";

        public List<AuditLogItemViewModel> Logs { get; set; } = new List<AuditLogItemViewModel>();
        public List<HealthRecordAuditLogItemViewModel> HealthLogs { get; set; } = new List<HealthRecordAuditLogItemViewModel>();
        
        // Filter options
        public string SelectedEventType { get; set; }
        public string SelectedRole { get; set; }
        public string SelectedRecordType { get; set; }
        public string DateRange { get; set; } // "all", "today", "7days", "30days"
        public string SearchQuery { get; set; }

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / (PageSize > 0 ? PageSize : 25));

        // Available Event Types, Roles, and Record Types for dropdown filters
        public List<string> AvailableEventTypes { get; set; } = new List<string>();
        public List<string> AvailableRoles { get; set; } = new List<string>();
        public List<string> AvailableRecordTypes { get; set; } = new List<string>();
    }

    public class AuditLogItemViewModel
    {
        public long LogId { get; set; }
        public string UserId { get; set; }
        public string UserRole { get; set; }
        public string EventType { get; set; }
        public string Details { get; set; }
        public DateTime TimestampUtc { get; set; }
    }

    public class HealthRecordAuditLogItemViewModel
    {
        public long AuditLogId { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string ActorId { get; set; }
        public string ActorRole { get; set; }
        public string ClientId { get; set; }
        public string RecordType { get; set; }
        public string RecordId { get; set; }
        public string Operation { get; set; }
        public bool Success { get; set; }
        public string Details { get; set; }
    }
}
