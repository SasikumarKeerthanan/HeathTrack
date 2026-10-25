using System;
using System.ComponentModel.DataAnnotations;
using System.Web;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.ViewModels
{
    /// ViewModel for Trainer Profile view and update operations.
    public class TrainerProfileViewModel
    {
        public Trainers Trainer { get; set; }
        public int ActiveClientsCount { get; set; }
        public int MaxClientCapacity { get; set; }

        // Form Edit Fields
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Gender")]
        public string Gender { get; set; }

        [Display(Name = "Primary Coaching Specialization")]
        [StringLength(100, ErrorMessage = "Specialization cannot exceed 100 characters.")]
        public string PrimarySpecialization { get; set; }

        [Range(0, 50, ErrorMessage = "Years of experience must be between 0 and 50.")]
        [Display(Name = "Coaching Experience (Years)")]
        public int? ExperienceYears { get; set; }

        [StringLength(1000, ErrorMessage = "Bio cannot exceed 1000 characters.")]
        [Display(Name = "Professional Biography")]
        public string Bio { get; set; }

        [Display(Name = "Profile Photo")]
        public HttpPostedFileBase ProfileImage { get; set; }
    }
}
