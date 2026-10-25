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
    public class AuditLogsController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        [HttpGet]
        public ActionResult Index(string logType = "health", string eventType = null, string role = null, string recordType = null, string dateRange = "all", string search = null, int page = 1)
        {
            const int pageSize = 25;
            if (page < 1) page = 1;

            string selectedLogType = string.Equals(logType, "security", StringComparison.OrdinalIgnoreCase) ? "security" : "health";

            var model = new AdminAuditLogsIndexViewModel
            {
                LogType = selectedLogType,
                SelectedEventType = eventType?.Trim(),
                SelectedRole = role?.Trim(),
                SelectedRecordType = recordType?.Trim(),
                DateRange = string.IsNullOrWhiteSpace(dateRange) ? "all" : dateRange.Trim().ToLower(),
                SearchQuery = search?.Trim(),
                CurrentPage = page,
                PageSize = pageSize
            };

            DateTime now = DateTime.UtcNow;

            if (selectedLogType == "security")
            {
                // FR-S-05 / FR-S-11: Authentication & Security Audit Telemetry
                model.AvailableEventTypes = db.AuthAuditLogs
                    .Select(l => l.EventType)
                    .Distinct()
                    .OrderBy(e => e)
                    .ToList();

                model.AvailableRoles = db.AuthAuditLogs
                    .Select(l => l.UserRole)
                    .Where(r => r != null && r != "")
                    .Distinct()
                    .OrderBy(r => r)
                    .ToList();

                IQueryable<AuthAuditLogs> query = db.AuthAuditLogs;

                if (!string.IsNullOrWhiteSpace(model.SelectedEventType))
                {
                    query = query.Where(l => l.EventType == model.SelectedEventType);
                }

                if (!string.IsNullOrWhiteSpace(model.SelectedRole))
                {
                    query = query.Where(l => l.UserRole == model.SelectedRole);
                }

                if (model.DateRange == "today")
                {
                    DateTime todayStart = now.Date;
                    query = query.Where(l => l.TimestampUtc >= todayStart);
                }
                else if (model.DateRange == "7days")
                {
                    DateTime sevenDaysAgo = now.AddDays(-7);
                    query = query.Where(l => l.TimestampUtc >= sevenDaysAgo);
                }
                else if (model.DateRange == "30days")
                {
                    DateTime thirtyDaysAgo = now.AddDays(-30);
                    query = query.Where(l => l.TimestampUtc >= thirtyDaysAgo);
                }

                if (!string.IsNullOrWhiteSpace(model.SearchQuery))
                {
                    string term = model.SearchQuery.ToLower();
                    query = query.Where(l => l.UserId.ToLower().Contains(term)
                                          || (l.Details != null && l.Details.ToLower().Contains(term))
                                          || l.EventType.ToLower().Contains(term));
                }

                model.TotalRecords = query.Count();

                var logs = query
                    .OrderByDescending(l => l.TimestampUtc)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                model.Logs = logs.Select(l => new AuditLogItemViewModel
                {
                    LogId = l.LogId,
                    UserId = l.UserId,
                    UserRole = l.UserRole,
                    EventType = l.EventType,
                    Details = l.Details,
                    TimestampUtc = l.TimestampUtc
                }).ToList();
            }
            else
            {
                // FR-S-20: Health Record Audit Logs (Clinical Access & Modification Ledger)
                model.AvailableEventTypes = db.HealthRecordAuditLogs
                    .Select(l => l.Action)
                    .Distinct()
                    .OrderBy(e => e)
                    .ToList();

                model.AvailableRoles = db.HealthRecordAuditLogs
                    .Select(l => l.ActorRole)
                    .Where(r => r != null && r != "")
                    .Distinct()
                    .OrderBy(r => r)
                    .ToList();

                model.AvailableRecordTypes = db.HealthRecordAuditLogs
                    .Select(l => l.RecordType)
                    .Where(r => r != null && r != "")
                    .Distinct()
                    .OrderBy(r => r)
                    .ToList();

                IQueryable<HealthRecordAuditLogs> query = db.HealthRecordAuditLogs;

                if (!string.IsNullOrWhiteSpace(model.SelectedEventType))
                {
                    query = query.Where(l => l.Action == model.SelectedEventType);
                }

                if (!string.IsNullOrWhiteSpace(model.SelectedRole))
                {
                    query = query.Where(l => l.ActorRole == model.SelectedRole);
                }

                if (!string.IsNullOrWhiteSpace(model.SelectedRecordType))
                {
                    query = query.Where(l => l.RecordType == model.SelectedRecordType);
                }

                if (model.DateRange == "today")
                {
                    DateTime todayStart = now.Date;
                    query = query.Where(l => l.OccurredAtUtc >= todayStart);
                }
                else if (model.DateRange == "7days")
                {
                    DateTime sevenDaysAgo = now.AddDays(-7);
                    query = query.Where(l => l.OccurredAtUtc >= sevenDaysAgo);
                }
                else if (model.DateRange == "30days")
                {
                    DateTime thirtyDaysAgo = now.AddDays(-30);
                    query = query.Where(l => l.OccurredAtUtc >= thirtyDaysAgo);
                }

                if (!string.IsNullOrWhiteSpace(model.SearchQuery))
                {
                    string term = model.SearchQuery.ToLower();
                    query = query.Where(l => l.ActorUserId.ToLower().Contains(term)
                                          || (l.ClientId != null && l.ClientId.ToLower().Contains(term))
                                          || (l.Details != null && l.Details.ToLower().Contains(term))
                                          || l.RecordType.ToLower().Contains(term)
                                          || l.Action.ToLower().Contains(term));
                }

                model.TotalRecords = query.Count();

                var healthLogs = query
                    .OrderByDescending(l => l.OccurredAtUtc)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                model.HealthLogs = healthLogs.Select(l => new HealthRecordAuditLogItemViewModel
                {
                    AuditLogId = l.AuditId,
                    TimestampUtc = l.OccurredAtUtc,
                    ActorId = l.ActorUserId,
                    ActorRole = l.ActorRole,
                    ClientId = l.ClientId,
                    RecordType = l.RecordType,
                    RecordId = l.RecordId,
                    Operation = l.Action,
                    Success = l.WasSuccessful,
                    Details = l.Details
                }).ToList();

                // FR-S-20: Audit administrator access to health record audit logs
                HealthAuditHelper.Log(
                    actorUserId: SessionHelper.GetCurrentAdminId(Session) ?? "Admin",
                    actorRole: "Administrator",
                    clientId: null,
                    recordType: "HealthRecordAuditLogs",
                    recordId: null,
                    action: "View",
                    wasSuccessful: true,
                    details: $"Viewed health record audit logs ledger (Page {page}, Filter: {model.DateRange})");
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
