using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Areas.Admin.ViewModels
{
    public class AdminSupportIndexViewModel
    {
        public List<AdminSupportTicketItemDto> Tickets { get; set; } = new List<AdminSupportTicketItemDto>();

        public int TotalTicketsCount { get; set; }
        public int OpenTicketsCount { get; set; }
        public int InProgressTicketsCount { get; set; }
        public int ResolvedTicketsCount { get; set; }

        public string SelectedStatus { get; set; } = "All";
        public string SearchQuery { get; set; }
    }

    public class AdminSupportTicketItemDto
    {
        public int TicketId { get; set; }
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string ClientEmail { get; set; }
        public string Subject { get; set; }
        public string Category { get; set; }
        public string Priority { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string AdminResponse { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    public class AdminRespondTicketViewModel
    {
        [Required]
        public int TicketId { get; set; }

        [Required(ErrorMessage = "Please enter a response or resolution.")]
        [Display(Name = "Admin Response")]
        public string AdminResponse { get; set; }

        [Required(ErrorMessage = "Please select a status.")]
        [Display(Name = "Ticket Status")]
        public string Status { get; set; } = "Resolved";
    }
}
