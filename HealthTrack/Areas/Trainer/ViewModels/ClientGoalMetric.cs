namespace HealthTrack.Areas.Trainer.ViewModels
{
    public class ClientGoalMetric
    {
        public string GoalName { get; set; } = string.Empty;
        public string Category { get; set; } = "Weight"; // Weight, Workout, Nutrition, Health
        public double BaselineValue { get; set; }
        public double CurrentValue { get; set; }
        public double TargetValue { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string StatusMessage { get; set; }
        public int ProgressPercentage { get; set; }
        public bool IsAchieved => ProgressPercentage >= 100;
    }
}
