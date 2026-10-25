using System;
using System.Collections.Generic;

namespace HealthTrack.Areas.Admin.ViewModels
{
    public class AdminClientIndexViewModel
    {
        public List<AdminClientListItemViewModel> Clients { get; set; } = new List<AdminClientListItemViewModel>();
        public string SearchQuery { get; set; }
        public string StatusFilter { get; set; } // "All", "Active", "Inactive", "Unassigned"
        
        public int TotalClientsCount { get; set; }
        public int ActiveClientsCount { get; set; }
        public int InactiveClientsCount { get; set; }
        public int UnassignedClientsCount { get; set; }
    }

    public class AdminClientListItemViewModel
    {
        public string ClientId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string AssignedTrainerId { get; set; }
        public string AssignedTrainerName { get; set; }
        public bool IsActive { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutEndUtc { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ProfilePicturePath { get; set; }

        public bool IsLockedOut => LockoutEndUtc.HasValue && LockoutEndUtc.Value > DateTime.UtcNow;
    }
}
