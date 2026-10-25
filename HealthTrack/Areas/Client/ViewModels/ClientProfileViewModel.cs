using System;
using System.ComponentModel.DataAnnotations;
using System.Web;
using HealthTrack.Models;

namespace HealthTrack.Areas.Client.ViewModels
{
    /// ViewModel for Client Profile view and edit operations.
    public class ClientProfileViewModel
    {
        // Display information
        public Clients Client { get; set; }
        public Trainers AssignedTrainer { get; set; }

        public decimal? CurrentWeightKg { get; set; }
        public decimal? Bmi { get; set; }
        public string BmiCategory { get; set; }
        public string PrimaryGoal { get; set; }

        // Edit Form Fields
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

        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [Range(50, 250, ErrorMessage = "Height must be between 50 and 250 cm.")]
        [Display(Name = "Height (cm)")]
        public decimal? HeightCm { get; set; }

        [Display(Name = "Profile Picture")]
        public HttpPostedFileBase ProfileImage { get; set; }
    }
}
