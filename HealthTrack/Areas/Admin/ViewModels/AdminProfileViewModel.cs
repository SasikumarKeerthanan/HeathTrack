using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Areas.Admin.ViewModels
{
    public class AdminProfileViewModel
    {
        public string AdminId { get; set; }

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        public string Role { get; set; } = "Admin";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }

        // Real-time system statistics from database
        public int TotalClientsCount { get; set; }
        public int TotalTrainersCount { get; set; }
        public int TotalTicketsCount { get; set; }

        // Change Password form
        public AdminChangePasswordViewModel PasswordForm { get; set; } = new AdminChangePasswordViewModel();

        // System Administrator accounts roster (FR-A-09)
        public List<AdminListItemViewModel> Administrators { get; set; } = new List<AdminListItemViewModel>();
    }

    public class AdminChangePasswordViewModel
    {
        [Required(ErrorMessage = "Current password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "New password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Please confirm your new password.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        [Display(Name = "Confirm New Password")]
        public string ConfirmPassword { get; set; }
    }

    /// <summary>
    /// Dedicated ViewModel for creating an additional Administrator account (FR-A-09).
    /// Binds only permitted account creation fields to prevent over-posting.
    /// </summary>
    public class CreateAdminViewModel
    {
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Administrator role is required.")]
        [RegularExpression("^(Admin)$", ErrorMessage = "Invalid or unsupported Administrator role. Allowed role is 'Admin'.")]
        [StringLength(20)]
        [Display(Name = "Administrator Role")]
        public string Role { get; set; } = "Admin";

        [Required(ErrorMessage = "Initial password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        [Display(Name = "Initial Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please confirm the initial password.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "The initial password and confirmation password do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; }
    }

    /// <summary>
    /// Read-only projection representing an Administrator account row in the roster (FR-A-09).
    /// Sensitive credentials and password hashes are never exposed.
    /// </summary>
    public class AdminListItemViewModel
    {
        public string AdminId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsCurrentAdmin { get; set; }
    }
}
