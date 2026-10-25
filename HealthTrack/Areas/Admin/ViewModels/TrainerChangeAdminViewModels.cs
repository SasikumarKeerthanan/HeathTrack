using System;
using System.Collections.Generic;
using HealthTrack.Models;

namespace HealthTrack.Areas.Admin.ViewModels
{
    public class AdminTrainerChangeIndexViewModel
    {
        public int TotalRequests { get; set; }
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public string ActiveFilter { get; set; } = "all";

        public List<TrainerChangeItemViewModel> Requests { get; set; } = new List<TrainerChangeItemViewModel>();
        public List<TrainerOptionViewModel> ActiveTrainers { get; set; } = new List<TrainerOptionViewModel>();
    }

    public class TrainerChangeItemViewModel
    {
        public int RequestId { get; set; }
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string ClientEmail { get; set; }
        public string ClientGender { get; set; }
        public string ClientGoal { get; set; }

        public string CurrentTrainerId { get; set; }
        public string CurrentTrainerName { get; set; }
        public string CurrentTrainerSpecialization { get; set; }

        public string NewTrainerId { get; set; }
        public string NewTrainerName { get; set; }

        public string ReasonCategory { get; set; }
        public string ReasonDetails { get; set; }
        public string PreferredGender { get; set; }

        public string Status { get; set; } // Pending, Approved, Rejected
        public string AdminNotes { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string ReviewedBy { get; set; }
    }

    public class TrainerOptionViewModel
    {
        public string TrainerId { get; set; }
        public string FullName { get; set; }
        public string Specialization { get; set; }
        public string Gender { get; set; }
        public int CurrentClientsCount { get; set; }
        public int MaxCapacity { get; set; }
        public bool IsFull => CurrentClientsCount >= MaxCapacity;
    }
}
