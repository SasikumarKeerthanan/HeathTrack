using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Admin.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class SupportController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // ====================================================================
        // GET: /Admin/Support
        // Central Helpdesk Dashboard for Gym Support & Inquiries
        // ====================================================================
        [HttpGet]
        public ActionResult Index(string status = "All", string search = null)
        {
            var model = new AdminSupportIndexViewModel
            {
                SelectedStatus = string.IsNullOrWhiteSpace(status) ? "All" : status.Trim(),
                SearchQuery = search?.Trim()
            };

            // Calculate KPI counts across all tickets
            model.TotalTicketsCount = db.SupportTickets.Count();
            model.OpenTicketsCount = db.SupportTickets.Count(t => t.Status == "Open");
            model.InProgressTicketsCount = db.SupportTickets.Count(t => t.Status == "In Progress");
            model.ResolvedTicketsCount = db.SupportTickets.Count(t => t.Status == "Resolved" || t.Status == "Closed");

            // Base query with client navigation
            IQueryable<SupportTickets> query = db.SupportTickets.Include(t => t.Clients);

            // Filter by Status
            if (!model.SelectedStatus.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (model.SelectedStatus.Equals("Resolved", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(t => t.Status == "Resolved" || t.Status == "Closed");
                }
                else
                {
                    query = query.Where(t => t.Status == model.SelectedStatus);
                }
            }

            // Filter by Search Query
            if (!string.IsNullOrWhiteSpace(model.SearchQuery))
            {
                string term = model.SearchQuery.ToLower();
                query = query.Where(t =>
                    t.TicketId.ToString().Contains(term) ||
                    t.ClientId.ToLower().Contains(term) ||
                    (t.Clients != null && t.Clients.FullName.ToLower().Contains(term)) ||
                    (t.Clients != null && t.Clients.Email.ToLower().Contains(term)) ||
                    t.Subject.ToLower().Contains(term) ||
                    t.Category.ToLower().Contains(term) ||
                    t.Description.ToLower().Contains(term)
                );
            }

            var ticketList = query
                .OrderByDescending(t => t.CreatedAt)
                .ToList();

            model.Tickets = ticketList.Select(t => new AdminSupportTicketItemDto
            {
                TicketId = t.TicketId,
                ClientId = t.ClientId,
                ClientName = t.Clients != null ? t.Clients.FullName : "Unknown Client",
                ClientEmail = t.Clients != null ? t.Clients.Email : "",
                Subject = t.Subject,
                Category = t.Category,
                Priority = t.Priority,
                Description = t.Description,
                Status = t.Status,
                AdminResponse = t.AdminResponse,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                ResolvedAt = t.ResolvedAt
            }).ToList();

            return View(model);
        }

        // ====================================================================
        // POST: /Admin/Support/Respond
        // Admin responds to and/or resolves a support ticket
        // ====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Respond(AdminRespondTicketViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please provide response notes and select a valid status.";
                return RedirectToAction("Index");
            }

            var ticket = db.SupportTickets.FirstOrDefault(t => t.TicketId == model.TicketId);
            if (ticket == null)
            {
                TempData["ErrorMessage"] = "Support ticket not found.";
                return RedirectToAction("Index");
            }

            string previousStatus = ticket.Status;
            ticket.AdminResponse = model.AdminResponse.Trim();
            ticket.Status = model.Status;
            ticket.UpdatedAt = DateTime.UtcNow;

            if (model.Status == "Resolved" || model.Status == "Closed")
            {
                ticket.ResolvedAt = DateTime.UtcNow;
            }

            // Record security audit log
            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = "Admin",
                UserRole = "Admin",
                EventType = model.Status == "Resolved" ? "TICKET_RESOLVED" : "TICKET_RESPONDED",
                Details = $"Admin responded to ticket #{ticket.TicketId} (Status changed from '{previousStatus}' to '{ticket.Status}') for client {ticket.ClientId}",
                TimestampUtc = DateTime.UtcNow
            });

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Support ticket #{ticket.TicketId} has been successfully updated with status '{ticket.Status}'.";
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
