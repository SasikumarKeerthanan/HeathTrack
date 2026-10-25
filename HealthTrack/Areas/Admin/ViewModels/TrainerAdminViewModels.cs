using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Areas.Admin.ViewModels
{
    /// Model for adding a new trainer.
    public class AddTrainerViewModel
    {
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Please select a gender.")]
        [RegularExpression("^(Male|Female)$", ErrorMessage = "Gender must be either Male or Female.")]
        [Display(Name = "Gender")]
        public string Gender { get; set; } = "Male";

        [Required(ErrorMessage = "Please select a primary specialization.")]
        [StringLength(50)]
        [Display(Name = "Primary Specialization")]
        public string PrimarySpecialization { get; set; }

        [Display(Name = "Supported Workout Types")]
        public List<string> SelectedWorkoutTypes { get; set; } = new List<string>();

        [Display(Name = "Supported Fitness Levels")]
        public List<string> SelectedFitnessLevels { get; set; } = new List<string>();

        [Display(Name = "Supported Diet Types")]
        public List<string> SelectedDietTypes { get; set; } = new List<string>();

        [Range(0, 50, ErrorMessage = "Experience years must be between 0 and 50.")]
        [Display(Name = "Years of Experience")]
        public int? ExperienceYears { get; set; }

        [Required(ErrorMessage = "Max Client Capacity is required.")]
        [Range(1, 100, ErrorMessage = "Capacity must be between 1 and 100.")]
        [Display(Name = "Maximum Client Capacity")]
        public int MaxClientCapacity { get; set; } = 15;

        [StringLength(500, ErrorMessage = "Bio cannot exceed 500 characters.")]
        [Display(Name = "Bio & Certifications")]
        public string Bio { get; set; }
    }

    /// Model representing a trainer row in the Admin list.
    public class TrainerListItemViewModel
    {
        public string TrainerId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Gender { get; set; }
        public string PrimarySpecialization { get; set; }
        public int MaxClientCapacity { get; set; }
        public int ActiveClientsCount { get; set; }
        public bool IsActive { get; set; }
        public bool IsInvitationAccepted { get; set; }
        public DateTime? InvitationExpiresUtc { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsInvitationExpired => !IsInvitationAccepted && InvitationExpiresUtc.HasValue && InvitationExpiresUtc.Value < DateTime.UtcNow;
    }
}
