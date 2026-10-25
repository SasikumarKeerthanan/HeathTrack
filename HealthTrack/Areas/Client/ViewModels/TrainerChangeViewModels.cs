using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HealthTrack.Models;

namespace HealthTrack.Areas.Client.ViewModels
{
    /// ViewModel for the single, all-in-one Client Trainer Change page.
    public class ClientTrainerChangePageViewModel
    {
        public Clients Client { get; set; }
        public Trainers CurrentTrainer { get; set; }
        public bool HasPendingRequest { get; set; }
        public TrainerChangeRequests PendingRequest { get; set; }
        public List<TrainerChangeRequests> PastRequests { get; set; } = new List<TrainerChangeRequests>();
        public Dictionary<string, string> TrainerNames { get; set; } = new Dictionary<string, string>();

        public SubmitTrainerChangeViewModel Form { get; set; } = new SubmitTrainerChangeViewModel();
    }

    /// Captures the client's reason and preferences for changing coach.
    public class SubmitTrainerChangeViewModel
    {
        [Required(ErrorMessage = "Please select a reason category.")]
        [Display(Name = "Reason Category")]
        public string ReasonCategory { get; set; }

        [Required(ErrorMessage = "Please provide details explaining why you wish to change coach.")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Reason details must be between 10 and 1000 characters.")]
        [Display(Name = "Detailed Reason & Expectations")]
        public string ReasonDetails { get; set; }

        [Required(ErrorMessage = "Please select preferred coach gender.")]
        [Display(Name = "Preferred Coach Gender")]
        public string PreferredGender { get; set; } = "No Preference";
    }
}
