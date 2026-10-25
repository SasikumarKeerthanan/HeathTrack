using System;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace HealthTrack.Areas.Client.ViewModels
{
    /// ViewModel for Client Registration.
    public class ClientRegisterViewModel
    {
        // =========================================================================
        // STEP 1: Account Credentials
        // =========================================================================

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email Address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please confirm your password.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; }

        // =========================================================================
        // STEP 2: Personal Profile Details
        // =========================================================================

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Please select your gender.")]
        [RegularExpression("^(Male|Female)$", ErrorMessage = "Gender must be either Male or Female.")]
        [Display(Name = "Gender")]
        public string Gender { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [Range(50, 250, ErrorMessage = "Height must be between 50 and 250 cm.")]
        [Display(Name = "Height (cm)")]
        public decimal? HeightCm { get; set; }

        [Required(ErrorMessage = "Please enter your current weight.")]
        [Range(30, 300, ErrorMessage = "Current weight must be between 30 and 300 kg.")]
        [Display(Name = "Current Weight (kg)")]
        public decimal? WeightKg { get; set; }

        [Range(30, 300, ErrorMessage = "Target weight must be between 30 and 300 kg.")]
        [Display(Name = "Target Goal Weight (kg)")]
        public decimal? TargetWeightKg { get; set; }

        [Display(Name = "Profile Picture (Optional)")]
        public HttpPostedFileBase ProfileImage { get; set; }

        // =========================================================================
        // STEP 3: Fitness & Diet Preferences
        // =========================================================================

        [Display(Name = "Primary Fitness Goal")]
        public string PrimaryGoal { get; set; }

        [Display(Name = "Current Fitness Level")]
        public string FitnessLevel { get; set; }

        [Display(Name = "Preferred Workout Type")]
        public string PreferredWorkoutType { get; set; }

        [Range(1, 7, ErrorMessage = "Target days must be between 1 and 7.")]
        [Display(Name = "Workout Days per Week")]
        public int? TargetDaysPerWeek { get; set; } = 3;

        [Display(Name = "Preferred Session Duration")]
        public short? PreferredWorkoutDurationMinutes { get; set; } = 45;

        [Display(Name = "Preferred Workout Time")]
        public string PreferredWorkoutTime { get; set; }

        [Display(Name = "Workout Location")]
        public string PreferredWorkoutLocation { get; set; }

        [Display(Name = "Preferred Diet / Nutrition Type")]
        public string PreferredDietType { get; set; }

        [Display(Name = "Target Timeframe")]
        public byte? TargetTimeframeMonths { get; set; } = 3;

        [Required(ErrorMessage = "Please select preferred coach gender.")]
        [RegularExpression("^(Male|Female)$", ErrorMessage = "Preferred Coach Gender must be either Male or Female.")]
        [Display(Name = "Preferred Trainer Gender")]
        public string PreferredTrainerGender { get; set; } = "Male";

        [StringLength(500, ErrorMessage = "Additional notes cannot exceed 500 characters.")]
        [Display(Name = "Health Notes or Preferences")]
        public string AdditionalPreferences { get; set; }
    }

    /// ViewModel for Client Login.
    public class ClientLoginViewModel
    {
        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please enter your password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }
    }

    /// ViewModel for resetting password via emailed link.
    public class ResetPasswordViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        public string Token { get; set; }

        [Required(ErrorMessage = "Please enter a new password.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please confirm your new password.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; }
    }
}
