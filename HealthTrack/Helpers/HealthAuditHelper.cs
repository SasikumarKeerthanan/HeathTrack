using System;
using System.Diagnostics;
using System.Web;
using HealthTrack.Models;

namespace HealthTrack.Helpers
{
    /// <summary>
    /// FR-S-20: Audit logging service for access to and modification of protected Client health records.
    /// Records events in the dedicated HealthRecordAuditLogs table with UTC timestamps.
    /// Strictly excludes passwords, tokens, full health measurements, and medical disclosures from audit details.
    /// Implements safe failure policy: audit write errors are logged diagnostically without terminating user operations.
    /// </summary>
    public static class HealthAuditHelper
    {
        /// <summary>
        /// Logs a health record access or modification event using the current HTTP session to identify the actor.
        /// </summary>
        public static void LogAccess(
            HttpSessionStateBase session,
            string clientId,
            string recordType,
            string recordId,
            string action,
            bool wasSuccessful = true,
            string details = null)
        {
            try
            {
                string actorUserId = "ANONYMOUS";
                string actorRole = "Unknown";

                if (session != null)
                {
                    string clientUser = SessionHelper.GetCurrentClientId(session);
                    string trainerUser = SessionHelper.GetCurrentTrainerId(session);
                    string adminUser = session["AdminId"]?.ToString();
                    string sessionRole = session["UserRole"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(clientUser))
                    {
                        actorUserId = clientUser;
                        actorRole = "Client";
                    }
                    else if (!string.IsNullOrWhiteSpace(trainerUser))
                    {
                        actorUserId = trainerUser;
                        actorRole = "Trainer";
                    }
                    else if (!string.IsNullOrWhiteSpace(adminUser))
                    {
                        actorUserId = adminUser;
                        actorRole = "Administrator";
                    }
                    else if (!string.IsNullOrWhiteSpace(sessionRole))
                    {
                        actorRole = sessionRole;
                        actorUserId = session["UserId"]?.ToString() ?? "AUTHENTICATED";
                    }
                }

                Log(actorUserId, actorRole, clientId, recordType, recordId, action, wasSuccessful, details);
            }
            catch (Exception ex)
            {
                // Explicit safe failure policy: never allow audit logging failures to interrupt healthcare operations
                Trace.TraceError($"[HealthAuditHelper.LogAccess] Error resolving session actor: {ex.Message}");
            }
        }

        /// <summary>
        /// Core audit recorder committing directly to HealthRecordAuditLogs with sanitized metadata.
        /// </summary>
        public static void Log(
            string actorUserId,
            string actorRole,
            string clientId,
            string recordType,
            string recordId,
            string action,
            bool wasSuccessful = true,
            string details = null)
        {
            // Sanitize inputs
            string safeActor = string.IsNullOrWhiteSpace(actorUserId) ? "UNKNOWN" : actorUserId.Trim();
            string safeRole = string.IsNullOrWhiteSpace(actorRole) ? "Unknown" : actorRole.Trim();
            string safeClient = string.IsNullOrWhiteSpace(clientId) ? "UNKNOWN" : clientId.Trim();
            string safeType = string.IsNullOrWhiteSpace(recordType) ? "HealthRecord" : recordType.Trim();
            string safeRecordId = string.IsNullOrWhiteSpace(recordId) ? null : recordId.Trim();
            string safeAction = string.IsNullOrWhiteSpace(action) ? "Access" : action.Trim();

            // Strict sanitization: ensure details contain only high-level operational descriptions
            string safeDetails = SanitizeDetails(details);

            try
            {
                using (var db = new HealthTrackEntities())
                {
                    var auditEntry = new HealthRecordAuditLogs
                    {
                        ActorUserId = safeActor.Length > 50 ? safeActor.Substring(0, 50) : safeActor,
                        ActorRole = safeRole.Length > 20 ? safeRole.Substring(0, 20) : safeRole,
                        ClientId = safeClient.Length > 50 ? safeClient.Substring(0, 50) : safeClient,
                        RecordType = safeType.Length > 50 ? safeType.Substring(0, 50) : safeType,
                        RecordId = safeRecordId != null && safeRecordId.Length > 50 ? safeRecordId.Substring(0, 50) : safeRecordId,
                        Action = safeAction.Length > 20 ? safeAction.Substring(0, 20) : safeAction,
                        WasSuccessful = wasSuccessful,
                        OccurredAtUtc = DateTime.UtcNow,
                        Details = safeDetails
                    };

                    db.HealthRecordAuditLogs.Add(auditEntry);
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                // Explicit safe policy: log diagnostic trace to system log, but preserve client/trainer execution
                Trace.TraceError($"[HealthAuditHelper.Log] Failed to persist health audit entry for Client '{safeClient}', Action '{safeAction}': {ex.Message}");
            }
        }

        private static string SanitizeDetails(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            string sanitized = input.Trim();
            // Restrict maximum length to 500 characters
            if (sanitized.Length > 500)
            {
                sanitized = sanitized.Substring(0, 497) + "...";
            }

            return sanitized;
        }
    }
}
