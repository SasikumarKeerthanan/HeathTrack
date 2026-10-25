using System;
using System.Globalization;
using HealthTrack.Helpers;

namespace HealthTrack.ReminderWorker
{
    class Program
    {
        static int Main(string[] args)
        {
            DateTime? referenceNow = null;

            // Optional command line parameter for manual testing/verification simulation:
            // e.g. HealthTrack.ReminderWorker.exe --now "2026-10-24 07:35"
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--now", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (DateTime.TryParse(args[i + 1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ||
                        DateTime.TryParse(args[i + 1], CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
                    {
                        referenceNow = dt;
                    }
                }
            }

            Console.WriteLine("===============================================================");
            Console.WriteLine("   HealthTrack Automated Reminder Worker (FR-S-26, 27, 28)    ");
            Console.WriteLine("===============================================================");
            Console.WriteLine($"Worker started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss} (Local)");
            if (referenceNow.HasValue)
            {
                Console.WriteLine($"[Simulation Mode] Reference time: {referenceNow.Value:yyyy-MM-dd HH:mm:ss}");
            }

            try
            {
                var result = ReminderService.ProcessAllDueReminders(referenceNow);

                Console.WriteLine();
                Console.WriteLine("--- Processing Summary ---");
                Console.WriteLine($"Workout Reminders Generated (FR-S-26):     {result.WorkoutRemindersGenerated}");
                Console.WriteLine($"Meal Reminders Generated (FR-S-27):        {result.MealRemindersGenerated}");
                Console.WriteLine($"Appointment Reminders Generated (FR-S-28): {result.AppointmentRemindersGenerated}");
                Console.WriteLine($"Total Notifications Created:               {result.TotalRemindersGenerated}");

                if (result.Logs != null && result.Logs.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("--- Detailed Activity Logs ---");
                    foreach (var log in result.Logs)
                    {
                        Console.WriteLine($"  {log}");
                    }
                }

                if (result.Errors != null && result.Errors.Count > 0)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("--- Errors Encountered ---");
                    foreach (var err in result.Errors)
                    {
                        Console.WriteLine($"  [ERROR] {err}");
                    }
                    Console.ResetColor();
                    return 1;
                }

                Console.WriteLine();
                Console.WriteLine("Worker cycle completed successfully.");
                return 0; // Return code 0 (Success) for Windows Task Scheduler
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FATAL ERROR] An unexpected exception occurred during worker run: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                return 1; // Return code 1 (Failure)
            }
        }
    }
}
