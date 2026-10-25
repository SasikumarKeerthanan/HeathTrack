using System;
using System.Collections.Generic;
using HealthTrack.Models;

namespace HealthTrack.Areas.Client.ViewModels
{
    public class ClientSummaryDto
    {
        public string ClientId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Gender { get; set; }
        public string AssignedTrainerId { get; set; }
        public string TrainerName { get; set; }
    }

    public class ExerciseInPlanDto
    {
        public int WorkoutPlanExerciseId { get; set; }
        public int ExerciseId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string DifficultyLevel { get; set; }
        public string TargetMuscle { get; set; }
        public string SecondaryMuscles { get; set; }
        public string Equipment { get; set; }
        public int Sets { get; set; }
        public int? Reps { get; set; }
        public int? DurationMinutes { get; set; }
        public int RestSeconds { get; set; }
        public string Instructions { get; set; }
        public string ExerciseRole { get; set; }
        public string ImagePath { get; set; }
        public string StartImagePath { get; set; }
        public string MovementImagePath { get; set; }
        public string MovementPattern { get; set; }
        public string KeyGuidance { get; set; }
        public string TipsAndPrecautions { get; set; }
        public int DefaultCaloriesBurnEstimate { get; set; }
    }

    public class WorkoutDayScheduleDto
    {
        public string DayOfWeek { get; set; }
        public int OrderIndex { get; set; }
        public string FocusTitle { get; set; }
        public int ExerciseCount { get; set; }
        public int TotalSets { get; set; }
        public int EstimatedDurationMinutes { get; set; }
        public int EstimatedCaloriesBurn { get; set; }
        public bool IsToday { get; set; }
        public bool IsCompletedToday { get; set; }
        public List<ExerciseInPlanDto> Exercises { get; set; } = new List<ExerciseInPlanDto>();
    }

    public class ClientWorkoutDashboardViewModel
    {
        public ClientSummaryDto Client { get; set; }
        public List<ClientSummaryDto> AvailableClients { get; set; } = new List<ClientSummaryDto>();
        public WorkoutPlans ActivePlan { get; set; }
        public List<WorkoutPlans> AllPlans { get; set; } = new List<WorkoutPlans>();
        public List<WorkoutDayScheduleDto> WeeklySchedules { get; set; } = new List<WorkoutDayScheduleDto>();
        public WorkoutDayScheduleDto TodaySchedule { get; set; }
        public ClientWorkoutLogs TodayLog { get; set; }
        public List<ClientWorkoutLogs> RecentLogs { get; set; } = new List<ClientWorkoutLogs>();

        // Analytics KPIs
        public int TotalWorkoutsCompleted { get; set; }
        public int CurrentStreakDays { get; set; }
        public int TotalMinutesTrained { get; set; }
        public int TotalCaloriesBurned { get; set; }
        public int CompletionRatePercent { get; set; }
    }

    public class WorkoutPlanDetailsViewModel
    {
        public ClientSummaryDto Client { get; set; }
        public List<ClientSummaryDto> AvailableClients { get; set; } = new List<ClientSummaryDto>();
        public WorkoutPlans Plan { get; set; }
        public List<WorkoutDayScheduleDto> GroupedDays { get; set; } = new List<WorkoutDayScheduleDto>();
        public string SelectedDay { get; set; }
        public bool HasActivePlan { get; set; }
    }

    public class CalendarDayDto
    {
        public DateTime Date { get; set; }
        public int DayNumber { get; set; }
        public string DayName { get; set; }
        public bool IsCurrentMonth { get; set; }
        public bool IsToday { get; set; }
        public bool IsPast { get; set; }
        public bool IsScheduledWorkout { get; set; }
        public string ScheduledDayOfWeek { get; set; }
        public string ScheduledSessionName { get; set; }
        public int ScheduledExercisesCount { get; set; }
        public bool IsCompleted { get; set; }
        public ClientWorkoutLogs CompletedLog { get; set; }
    }

    public class WorkoutCalendarViewModel
    {
        public ClientSummaryDto Client { get; set; }
        public List<ClientSummaryDto> AvailableClients { get; set; } = new List<ClientSummaryDto>();
        public WorkoutPlans ActivePlan { get; set; }
        public int CurrentMonth { get; set; }
        public int CurrentYear { get; set; }
        public string MonthName { get; set; }
        public List<CalendarDayDto> DaysInMonth { get; set; } = new List<CalendarDayDto>();
        public int ScheduledThisMonth { get; set; }
        public int CompletedThisMonth { get; set; }
        public int TotalMinutesThisMonth { get; set; }
        public int AdherenceRatePercent { get; set; }
    }

    public class WorkoutHistoryViewModel
    {
        public ClientSummaryDto Client { get; set; }
        public List<ClientSummaryDto> AvailableClients { get; set; } = new List<ClientSummaryDto>();
        public List<ClientWorkoutLogs> Logs { get; set; } = new List<ClientWorkoutLogs>();
        public int TotalLogsCount { get; set; }
        public int TotalDurationHours { get; set; }
        public int TotalCalories { get; set; }
        public double AverageRpe { get; set; }
        public string SearchTerm { get; set; }
        public string FilterStatus { get; set; }
        public int? FilterPlanId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<WorkoutPlans> AvailablePlans { get; set; } = new List<WorkoutPlans>();

        public bool HasActiveFilter =>
            !string.IsNullOrWhiteSpace(SearchTerm) ||
            (!string.IsNullOrWhiteSpace(FilterStatus) && !FilterStatus.Equals("all", StringComparison.OrdinalIgnoreCase)) ||
            FilterPlanId.HasValue ||
            FromDate.HasValue ||
            ToDate.HasValue;
    }

    public class LogWorkoutSubmissionModel
    {
        public string ClientId { get; set; }
        public int? WorkoutPlanId { get; set; }
        public DateTime? LogDate { get; set; }
        public string SessionName { get; set; }
        public int DurationMinutes { get; set; }
        public int CaloriesBurned { get; set; }
        public bool IsCompleted { get; set; }
        public int PerceivedExertion { get; set; } // 1-10
        public string ClientNotes { get; set; }
    }
}
