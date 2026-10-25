using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Areas.Client.ViewModels
{
    public class ClientSupportIndexViewModel
    {
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string Email { get; set; }

        public List<ClientSupportTicketItemDto> Tickets { get; set; } = new List<ClientSupportTicketItemDto>();

        public int TotalTicketsCount { get; set; }
        public int OpenTicketsCount { get; set; }
        public int InProgressTicketsCount { get; set; }
        public int ResolvedTicketsCount { get; set; }

        public string SelectedStatus { get; set; } = "All";
        public string SearchTerm { get; set; }

        public CreateTicketViewModel NewTicket { get; set; } = new CreateTicketViewModel();
    }

    public class ClientSupportTicketItemDto
    {
        public int TicketId { get; set; }
        public string ClientId { get; set; }
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

    public class CreateTicketViewModel
    {
        [Required(ErrorMessage = "Please enter a subject.")]
        [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
        [Display(Name = "Subject")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public string Category { get; set; }

        [Required(ErrorMessage = "Please select a priority.")]
        [Display(Name = "Priority")]
        public string Priority { get; set; } = "Medium";

        [Required(ErrorMessage = "Please describe your question or issue.")]
        [Display(Name = "Description")]
        public string Description { get; set; }
    }
}
