using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Client.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Client.Controllers
{
    [ClientAuthorize]
    public class SupportController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        private Clients GetCurrentClient()
        {
            var sessionClientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrWhiteSpace(sessionClientId))
            {
                return null;
            }

            return db.Clients.FirstOrDefault(c => c.ClientId == sessionClientId);
        }

        // ====================================================================
        // GET: /Client/Support
        // Member support ticket dashboard & submission center
        // ====================================================================
        [HttpGet]
        public ActionResult Index(string status = "All", string search = null)
        {
            var client = GetCurrentClient();
            if (client == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            ViewBag.ClientName = client.FullName;
            ViewBag.ClientEmail = client.Email;

            // Fetch all tickets submitted by this client
            var allClientTickets = db.SupportTickets
                .Where(t => t.ClientId == client.ClientId)
                .OrderByDescending(t => t.CreatedAt)
                .ToList();

            // Calculate KPI counters
            int totalCount = allClientTickets.Count;
            int openCount = allClientTickets.Count(t => t.Status == "Open");
            int inProgressCount = allClientTickets.Count(t => t.Status == "In Progress");
            int resolvedCount = allClientTickets.Count(t => t.Status == "Resolved" || t.Status == "Closed");

            // Filter tickets
            var filtered = allClientTickets.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (status.Equals("Resolved", StringComparison.OrdinalIgnoreCase))
                {
                    filtered = filtered.Where(t => t.Status == "Resolved" || t.Status == "Closed");
                }
                else
                {
                    filtered = filtered.Where(t => string.Equals(t.Status, status, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim().ToLower();
                filtered = filtered.Where(t =>
                    (t.Subject != null && t.Subject.ToLower().Contains(term)) ||
                    (t.Description != null && t.Description.ToLower().Contains(term)) ||
                    (t.Category != null && t.Category.ToLower().Contains(term)) ||
                    t.TicketId.ToString().Contains(term)
                );
            }

            var ticketDtos = filtered.Select(t => new ClientSupportTicketItemDto
            {
                TicketId = t.TicketId,
                ClientId = t.ClientId,
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

            var viewModel = new ClientSupportIndexViewModel
            {
                ClientId = client.ClientId,
                ClientName = client.FullName,
                Email = client.Email,
                Tickets = ticketDtos,
                TotalTicketsCount = totalCount,
                OpenTicketsCount = openCount,
                InProgressTicketsCount = inProgressCount,
                ResolvedTicketsCount = resolvedCount,
                SelectedStatus = string.IsNullOrWhiteSpace(status) ? "All" : status,
                SearchTerm = search,
                NewTicket = new CreateTicketViewModel()
            };

            return View(viewModel);
        }

        // ====================================================================
        // POST: /Client/Support/Create
        // Submit a new support ticket
        // ====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateTicketViewModel model)
        {
            var client = GetCurrentClient();
            if (client == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please fill in all required fields to submit your ticket.";
                return RedirectToAction("Index");
            }

            var ticket = new SupportTickets
            {
                ClientId = client.ClientId,
                Subject = model.Subject.Trim(),
                Category = model.Category,
                Priority = string.IsNullOrWhiteSpace(model.Priority) ? "Medium" : model.Priority,
                Description = model.Description.Trim(),
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            };

            db.SupportTickets.Add(ticket);

            // Log event in audit history
            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = client.ClientId,
                UserRole = "Client",
                EventType = "TICKET_CREATED",
                Details = $"Client created support ticket #{ticket.TicketId}: {ticket.Subject} ({ticket.Category})",
                TimestampUtc = DateTime.UtcNow
            });

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Your support ticket #{ticket.TicketId} has been submitted successfully! Our support staff will respond promptly.";
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
