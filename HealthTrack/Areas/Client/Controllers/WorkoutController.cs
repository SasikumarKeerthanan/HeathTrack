using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Client.ViewModels;
using HealthTrack.Models;
using HealthTrack.Helpers;

namespace HealthTrack.Areas.Client.Controllers
{
    [ClientAuthorize]
    public class WorkoutController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        private static readonly string[] OrderedDaysOfWeek = new[]
        {
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
        };

        // Helper: retrieve active logged-in client strictly from Session
        private Clients GetCurrentClient(string requestedClientId = null)
        {
            var sessionClientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrWhiteSpace(sessionClientId))
            {
                return null;
            }

            // FR-S-04: If requestedClientId is supplied, strictly verify ownership against authenticated session
            if (!string.IsNullOrWhiteSpace(requestedClientId) &&
                !string.Equals(requestedClientId.Trim(), sessionClientId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var sessionClient = db.Clients
                .Include(c => c.Trainers)
                .FirstOrDefault(c => c.ClientId == sessionClientId);

            return sessionClient;
        }

        private List<ClientSummaryDto> GetAvailableClients(Clients client = null)
        {
            if (client == null)
            {
                var sessionClientId = SessionHelper.GetCurrentClientId(Session);
                if (!string.IsNullOrWhiteSpace(sessionClientId))
                {
                    client = db.Clients.Include(c => c.Trainers).FirstOrDefault(c => c.ClientId == sessionClientId);
                }
            }

            if (client == null) return new List<ClientSummaryDto>();

            // FR-S-04: Client only sees their own summary profile, not other clients' profiles
            return new List<ClientSummaryDto>
            {
                new ClientSummaryDto
                {
                    ClientId = client.ClientId,
                    FullName = client.FullName,
                    Email = client.Email,
                    Gender = client.Gender,
                    AssignedTrainerId = client.AssignedTrainerId,
                    TrainerName = client.Trainers != null ? client.Trainers.FullName : "Coach"
                }
            };
        }

        private List<WorkoutDayScheduleDto> BuildDaySchedules(WorkoutPlans plan, DateTime? referenceDate = null)
        {
            var date = referenceDate ?? DateTime.Today;
            var todayName = date.DayOfWeek.ToString();

            var schedules = new List<WorkoutDayScheduleDto>();

            if (plan == null || plan.WorkoutPlanExercises == null)
                return schedules;

            var grouped = plan.WorkoutPlanExercises
                .GroupBy(e => e.DayOfWeek)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.OrderIndex).ToList(), StringComparer.OrdinalIgnoreCase);

            int order = 0;
            foreach (var dayName in OrderedDaysOfWeek)
            {
                if (grouped.ContainsKey(dayName))
                {
                    var exercises = grouped[dayName];
                    var mappedExercises = exercises.Select(e => new ExerciseInPlanDto
                    {
                        WorkoutPlanExerciseId = e.WorkoutPlanExerciseId,
                        ExerciseId = e.ExerciseId,
                        Name = e.Exercises != null ? e.Exercises.Name : "Exercise",
                        Category = e.Exercises != null ? e.Exercises.Category : "General",
                        DifficultyLevel = e.Exercises != null ? e.Exercises.DifficultyLevel : "Intermediate",
                        TargetMuscle = e.Exercises != null ? e.Exercises.TargetMuscle : "Full Body",
                        SecondaryMuscles = e.Exercises != null ? e.Exercises.SecondaryMuscles : "",
                        Equipment = e.Exercises != null ? e.Exercises.Equipment : "Standard",
                        Sets = e.Sets,
                        Reps = e.Reps,
                        DurationMinutes = e.DurationMinutes,
                        RestSeconds = e.RestSeconds > 0 ? e.RestSeconds : 60,
                        Instructions = !string.IsNullOrWhiteSpace(e.Instructions) ? e.Instructions : (e.Exercises != null ? e.Exercises.Instructions : ""),
                        ExerciseRole = !string.IsNullOrWhiteSpace(e.ExerciseRole) ? e.ExerciseRole : "Compound Movement",
                        ImagePath = e.Exercises != null ? e.Exercises.ImagePath : null,
                        StartImagePath = e.Exercises != null ? e.Exercises.StartImagePath : null,
                        MovementImagePath = e.Exercises != null ? e.Exercises.MovementImagePath : null,
                        MovementPattern = e.Exercises != null ? e.Exercises.MovementPattern : "Controlled Motion",
                        KeyGuidance = e.Exercises != null ? e.Exercises.KeyGuidance : "",
                        TipsAndPrecautions = e.Exercises != null ? e.Exercises.TipsAndPrecautions : "",
                        DefaultCaloriesBurnEstimate = e.Exercises != null ? e.Exercises.DefaultCaloriesBurnEstimate : 60
                    }).ToList();

                    var distinctMuscles = mappedExercises
                        .Select(me => me.TargetMuscle)
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .Distinct()
                        .Take(2);

                    var focusTitle = distinctMuscles.Any() ? string.Join(" & ", distinctMuscles) : "Conditioning";

                    var totalEstMinutes = mappedExercises.Sum(me => (me.DurationMinutes ?? 0) > 0 ? me.DurationMinutes.Value : (me.Sets * 2));
                    if (totalEstMinutes == 0 && mappedExercises.Any()) totalEstMinutes = mappedExercises.Count * 8;

                    var totalEstCalories = mappedExercises.Sum(me => me.DefaultCaloriesBurnEstimate > 0 ? me.DefaultCaloriesBurnEstimate : 0);
                    if (totalEstCalories == 0 && totalEstMinutes > 0) totalEstCalories = totalEstMinutes * 5;

                    schedules.Add(new WorkoutDayScheduleDto
                    {
                        DayOfWeek = dayName,
                        OrderIndex = ++order,
                        FocusTitle = focusTitle,
                        ExerciseCount = mappedExercises.Count,
                        TotalSets = mappedExercises.Sum(me => me.Sets),
                        EstimatedDurationMinutes = totalEstMinutes,
                        EstimatedCaloriesBurn = totalEstCalories,
                        IsToday = dayName.Equals(todayName, StringComparison.OrdinalIgnoreCase),
                        Exercises = mappedExercises
                    });
                }
            }

            return schedules;
        }

        // Action: Switch active client (FR-S-04: Enforce that clients cannot switch to other accounts)
        [HttpGet]
        public ActionResult SwitchClient(string clientId, string returnUrl = null)
        {
            var sessionClientId = SessionHelper.GetCurrentClientId(Session);
            if (string.IsNullOrWhiteSpace(sessionClientId) ||
                (!string.IsNullOrWhiteSpace(clientId) && !string.Equals(clientId.Trim(), sessionClientId.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return new HttpStatusCodeResult(403, "Access Denied: You cannot switch to another client's account.");
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index");
        }

        // 1. GET: /Client/Workout
        // Main Client Workout Hub with Today's Session, Live Workout Mode, and KPI Metrics
        [HttpGet]
        public ActionResult Index(string clientId = null)
        {
            if (!string.IsNullOrWhiteSpace(clientId) && !AuthorizationHelper.IsClientOwner(Session, clientId))
            {
                return new HttpStatusCodeResult(403, "Access Denied: You are not authorized to view another client's workout dashboard.");
            }

            var client = GetCurrentClient(clientId);
            if (client == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Client" });
            }

            var availableClients = GetAvailableClients(client);

            // Fetch Active or Most Recent Workout Plan
            var activePlan = db.WorkoutPlans
                .Include(p => p.Trainers)
                .Include(p => p.WorkoutPlanExercises.Select(wpe => wpe.Exercises))
                .Where(p => p.ClientId == client.ClientId && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (activePlan == null)
            {
                // Fallback to any latest plan
                activePlan = db.WorkoutPlans
                    .Include(p => p.Trainers)
                    .Include(p => p.WorkoutPlanExercises.Select(wpe => wpe.Exercises))
                    .Where(p => p.ClientId == client.ClientId)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();
            }

            var allPlans = db.WorkoutPlans
                .Where(p => p.ClientId == client.ClientId)
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            var weeklySchedules = BuildDaySchedules(activePlan);

            var todayName = DateTime.Today.DayOfWeek.ToString();
            var todaySchedule = weeklySchedules.FirstOrDefault(s => s.DayOfWeek.Equals(todayName, StringComparison.OrdinalIgnoreCase));

            // Check today's completed log
            var todayStart = DateTime.Today;
            var todayEnd = todayStart.AddDays(1);
            var todayLog = db.ClientWorkoutLogs
                .Where(l => l.ClientId == client.ClientId && l.LogDate >= todayStart && l.LogDate < todayEnd)
                .OrderByDescending(l => l.LogId)
                .FirstOrDefault();

            if (todaySchedule != null && todayLog != null && todayLog.IsCompleted)
            {
                todaySchedule.IsCompletedToday = true;
            }

            // Recent Logs
            var recentLogs = db.ClientWorkoutLogs
                .Include(l => l.WorkoutPlans)
                .Where(l => l.ClientId == client.ClientId)
                .OrderByDescending(l => l.LogDate)
                .Take(5)
                .ToList();

            // Total Completed
            var totalCompleted = db.ClientWorkoutLogs
                .Count(l => l.ClientId == client.ClientId && l.IsCompleted);

            var totalMinutes = db.ClientWorkoutLogs
                .Where(l => l.ClientId == client.ClientId && l.IsCompleted)
                .Select(l => (int?)l.DurationMinutes)
                .Sum() ?? 0;

            var totalCalories = db.ClientWorkoutLogs
                .Where(l => l.ClientId == client.ClientId && l.IsCompleted)
                .Select(l => (int?)l.CaloriesBurned)
                .Sum() ?? 0;

            // Calculate streak
            int streakDays = 0;
            var logDates = db.ClientWorkoutLogs
                .Where(l => l.ClientId == client.ClientId && l.IsCompleted)
                .Select(l => DbFunctions.TruncateTime(l.LogDate))
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            DateTime checkDate = DateTime.Today;
            if (logDates.Any())
            {
                // if logged today or yesterday, streak continues
                if (logDates.Any(d => d.HasValue && d.Value.Date == checkDate.Date))
                {
                    while (logDates.Any(d => d.HasValue && d.Value.Date == checkDate.Date))
                    {
                        streakDays++;
                        checkDate = checkDate.AddDays(-1);
                    }
                }
                else if (logDates.Any(d => d.HasValue && d.Value.Date == checkDate.AddDays(-1).Date))
                {
                    checkDate = checkDate.AddDays(-1);
                    while (logDates.Any(d => d.HasValue && d.Value.Date == checkDate.Date))
                    {
                        streakDays++;
                        checkDate = checkDate.AddDays(-1);
                    }
                }
            }

            int completionRate = 0;
            if (activePlan != null && activePlan.DaysPerWeek > 0)
            {
                var daysSinceStart = (DateTime.Today - activePlan.StartDate.Date).Days + 1;
                if (daysSinceStart > 0)
                {
                    int expectedWorkouts = (int)Math.Ceiling((daysSinceStart / 7.0) * activePlan.DaysPerWeek);
                    if (expectedWorkouts > 0)
                    {
                        completionRate = (int)Math.Min(100, Math.Round((double)totalCompleted / expectedWorkouts * 100));
                    }
                }
            }
            if (completionRate == 0 && totalCompleted > 0)
            {
                completionRate = Math.Min(100, totalCompleted * 10);
            }

            var clientDto = new ClientSummaryDto
            {
                ClientId = client.ClientId,
                FullName = client.FullName,
                Email = client.Email,
                Gender = client.Gender,
                AssignedTrainerId = client.AssignedTrainerId,
                TrainerName = client.Trainers != null ? client.Trainers.FullName : "Assigned Coach"
            };

            var viewModel = new ClientWorkoutDashboardViewModel
            {
                Client = clientDto,
                AvailableClients = availableClients,
                ActivePlan = activePlan,
                AllPlans = allPlans,
                WeeklySchedules = weeklySchedules,
                TodaySchedule = todaySchedule,
                TodayLog = todayLog,
                RecentLogs = recentLogs,
                TotalWorkoutsCompleted = totalCompleted,
                CurrentStreakDays = streakDays,
                TotalMinutesTrained = totalMinutes,
                TotalCaloriesBurned = totalCalories,
                CompletionRatePercent = completionRate
            };

            return View(viewModel);
        }

        // 2. GET: /Client/Workout/Plan
        // In-depth view of assigned workout routine, day tabs & dual-photo exercise instructions
        [HttpGet]
        public ActionResult Plan(int? id, string clientId = null, string day = null)
        {
            if (!string.IsNullOrWhiteSpace(clientId) && !AuthorizationHelper.IsClientOwner(Session, clientId))
            {
                return new HttpStatusCodeResult(403, "Access Denied: You are not authorized to view another client's workout plan.");
            }

            var client = GetCurrentClient(clientId);
            if (client == null) return RedirectToAction("Index");

            WorkoutPlans plan = null;
            if (id.HasValue)
            {
                plan = db.WorkoutPlans
                    .Include(p => p.Trainers)
                    .Include(p => p.WorkoutPlanExercises.Select(wpe => wpe.Exercises))
                    .FirstOrDefault(p => p.WorkoutPlanId == id.Value && p.ClientId == client.ClientId);

                if (plan == null)
                {
                    return new HttpStatusCodeResult(403, "Access Denied: The requested workout plan does not belong to you or does not exist.");
                }
            }

            if (plan == null)
            {
                plan = db.WorkoutPlans
                    .Include(p => p.Trainers)
                    .Include(p => p.WorkoutPlanExercises.Select(wpe => wpe.Exercises))
                    .Where(p => p.ClientId == client.ClientId && p.IsActive)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();
            }

            if (plan == null)
            {
                plan = db.WorkoutPlans
                    .Include(p => p.Trainers)
                    .Include(p => p.WorkoutPlanExercises.Select(wpe => wpe.Exercises))
                    .Where(p => p.ClientId == client.ClientId)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();
            }

            var clientDto = new ClientSummaryDto
            {
                ClientId = client.ClientId,
                FullName = client.FullName,
                Email = client.Email,
                Gender = client.Gender,
                AssignedTrainerId = client.AssignedTrainerId,
                TrainerName = client.Trainers != null ? client.Trainers.FullName : "Assigned Coach"
            };

            var groupedDays = BuildDaySchedules(plan);

            string selectedDay = day;
            if (string.IsNullOrWhiteSpace(selectedDay))
            {
                var todayName = DateTime.Today.DayOfWeek.ToString();
                var matched = groupedDays.FirstOrDefault(d => d.DayOfWeek.Equals(todayName, StringComparison.OrdinalIgnoreCase));
                selectedDay = matched != null ? matched.DayOfWeek : (groupedDays.FirstOrDefault()?.DayOfWeek ?? "Monday");
            }

            var viewModel = new WorkoutPlanDetailsViewModel
            {
                Client = clientDto,
                AvailableClients = GetAvailableClients(client),
                Plan = plan,
                GroupedDays = groupedDays,
                SelectedDay = selectedDay,
                HasActivePlan = plan != null
            };

            return View(viewModel);
        }

        // 3. GET: /Client/Workout/Calendar
        // Interactive Workout Calendar showing scheduled vs completed workouts
        [HttpGet]
        public ActionResult Calendar(int? month, int? year, string clientId = null)
        {
            if (!string.IsNullOrWhiteSpace(clientId) && !AuthorizationHelper.IsClientOwner(Session, clientId))
            {
                return new HttpStatusCodeResult(403, "Access Denied: You are not authorized to view another client's workout calendar.");
            }

            var client = GetCurrentClient(clientId);
            if (client == null) return RedirectToAction("Index");

            var today = DateTime.Today;
            int targetMonth = month ?? today.Month;
            int targetYear = year ?? today.Year;

            if (targetMonth < 1) { targetMonth = 12; targetYear--; }
            if (targetMonth > 12) { targetMonth = 1; targetYear++; }

            var firstDayOfMonth = new DateTime(targetYear, targetMonth, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            var activePlan = db.WorkoutPlans
                .Include(p => p.WorkoutPlanExercises.Select(wpe => wpe.Exercises))
                .Where(p => p.ClientId == client.ClientId && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            // Fetch logs for this month
            var logsInMonth = db.ClientWorkoutLogs
                .Where(l => l.ClientId == client.ClientId && l.LogDate >= firstDayOfMonth && l.LogDate <= lastDayOfMonth)
                .ToList();

            var scheduledDaysOfWeek = activePlan != null && activePlan.WorkoutPlanExercises != null
                ? activePlan.WorkoutPlanExercises.Select(e => e.DayOfWeek).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();

            var calendarDays = new List<CalendarDayDto>();

            // Calculate start padding for week start (Monday)
            int startDayOffset = ((int)firstDayOfMonth.DayOfWeek + 6) % 7; // 0 = Monday, 6 = Sunday
            var calendarGridStart = firstDayOfMonth.AddDays(-startDayOffset);

            // Total 35 or 42 cells (5 or 6 weeks)
            int totalCells = (startDayOffset + lastDayOfMonth.Day > 35) ? 42 : 35;

            int scheduledCount = 0;
            int completedCount = 0;
            int totalMinutes = 0;

            for (int i = 0; i < totalCells; i++)
            {
                var date = calendarGridStart.AddDays(i);
                bool isCurrentMonth = date.Month == targetMonth;
                bool isToday = date.Date == today;
                bool isPast = date.Date < today;
                string dayOfWeekName = date.DayOfWeek.ToString();

                bool isScheduled = false;
                string scheduledSession = null;
                int scheduledExercisesCount = 0;

                if (activePlan != null && scheduledDaysOfWeek.Contains(dayOfWeekName, StringComparer.OrdinalIgnoreCase))
                {
                    // Check if date is within plan range
                    if (date >= activePlan.StartDate.Date && date <= activePlan.EndDate.Date)
                    {
                        isScheduled = true;
                        var dayExercises = activePlan.WorkoutPlanExercises
                            .Where(e => e.DayOfWeek.Equals(dayOfWeekName, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        scheduledExercisesCount = dayExercises.Count;
                        scheduledSession = dayOfWeekName + " Routine (" + activePlan.Goal + ")";
                    }
                }

                var log = logsInMonth.FirstOrDefault(l => l.LogDate.Date == date.Date);
                bool isCompleted = log != null && log.IsCompleted;

                if (isCurrentMonth)
                {
                    if (isScheduled) scheduledCount++;
                    if (isCompleted)
                    {
                        completedCount++;
                        totalMinutes += log.DurationMinutes;
                    }
                }

                calendarDays.Add(new CalendarDayDto
                {
                    Date = date,
                    DayNumber = date.Day,
                    DayName = dayOfWeekName,
                    IsCurrentMonth = isCurrentMonth,
                    IsToday = isToday,
                    IsPast = isPast,
                    IsScheduledWorkout = isScheduled,
                    ScheduledDayOfWeek = dayOfWeekName,
                    ScheduledSessionName = scheduledSession,
                    ScheduledExercisesCount = scheduledExercisesCount,
                    IsCompleted = isCompleted,
                    CompletedLog = log
                });
            }

            int adherence = scheduledCount > 0 ? (int)Math.Min(100, Math.Round((double)completedCount / scheduledCount * 100)) : (completedCount > 0 ? 100 : 0);

            var clientDto = new ClientSummaryDto
            {
                ClientId = client.ClientId,
                FullName = client.FullName,
                Email = client.Email,
                Gender = client.Gender,
                AssignedTrainerId = client.AssignedTrainerId,
                TrainerName = client.Trainers != null ? client.Trainers.FullName : "Assigned Coach"
            };

            var viewModel = new WorkoutCalendarViewModel
            {
                Client = clientDto,
                AvailableClients = GetAvailableClients(client),
                ActivePlan = activePlan,
                CurrentMonth = targetMonth,
                CurrentYear = targetYear,
                MonthName = firstDayOfMonth.ToString("MMMM yyyy"),
                DaysInMonth = calendarDays,
                ScheduledThisMonth = scheduledCount,
                CompletedThisMonth = completedCount,
                TotalMinutesThisMonth = totalMinutes,
                AdherenceRatePercent = adherence
            };

            return View(viewModel);
        }

        // 4. GET: /Client/Workout/History (FR-C-62)
        // Comprehensive Workout History & Log Records with Date-Range and Status Filtering
        [HttpGet]
        public ActionResult History(string search, string status, int? planId, DateTime? fromDate = null, DateTime? toDate = null, string clientId = null)
        {
            if (!string.IsNullOrWhiteSpace(clientId) && !AuthorizationHelper.IsClientOwner(Session, clientId))
            {
                return new HttpStatusCodeResult(403, "Access Denied: You are not authorized to view another client's workout history.");
            }

            var client = GetCurrentClient(clientId);
            if (client == null) return RedirectToAction("Index");

            if (planId.HasValue && planId.Value > 0)
            {
                if (!db.WorkoutPlans.Any(p => p.WorkoutPlanId == planId.Value && p.ClientId == client.ClientId))
                {
                    return new HttpStatusCodeResult(403, "Access Denied: The specified workout plan does not belong to you.");
                }
            }

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            {
                TempData["ErrorMessage"] = "From Date cannot be later than To Date.";
                var temp = fromDate;
                fromDate = toDate;
                toDate = temp;
            }

            var query = db.ClientWorkoutLogs
                .Include(l => l.WorkoutPlans)
                .Where(l => l.ClientId == client.ClientId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(l =>
                    l.SessionName.Contains(term)
                    || (l.ClientNotes != null && l.ClientNotes.Contains(term))
                    || (l.CoachFeedback != null && l.CoachFeedback.Contains(term))
                    || (l.WorkoutPlans != null && l.WorkoutPlans.Title.Contains(term))
                );
            }

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(l => l.IsCompleted);
                }
                else if (status.Equals("partial", StringComparison.OrdinalIgnoreCase) || status.Equals("incomplete", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(l => !l.IsCompleted);
                }
            }

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(l => DbFunctions.TruncateTime(l.LogDate) >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date;
                query = query.Where(l => DbFunctions.TruncateTime(l.LogDate) <= to);
            }

            if (planId.HasValue && planId.Value > 0)
            {
                query = query.Where(l => l.WorkoutPlanId == planId.Value);
            }

            var logs = query.OrderByDescending(l => l.LogDate).ToList();

            var totalCompleted = logs.Count(l => l.IsCompleted);
            var totalMins = logs.Sum(l => l.DurationMinutes);
            var totalCal = logs.Sum(l => l.CaloriesBurned);
            var avgRpe = logs.Any(l => l.PerceivedExertion > 0)
                ? logs.Where(l => l.PerceivedExertion > 0).Average(l => l.PerceivedExertion)
                : 0.0;

            var availablePlans = db.WorkoutPlans
                .Where(p => p.ClientId == client.ClientId)
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            var clientDto = new ClientSummaryDto
            {
                ClientId = client.ClientId,
                FullName = client.FullName,
                Email = client.Email,
                Gender = client.Gender,
                AssignedTrainerId = client.AssignedTrainerId,
                TrainerName = client.Trainers != null ? client.Trainers.FullName : "Assigned Coach"
            };

            var viewModel = new WorkoutHistoryViewModel
            {
                Client = clientDto,
                AvailableClients = GetAvailableClients(client),
                Logs = logs,
                TotalLogsCount = logs.Count,
                TotalDurationHours = (int)Math.Round(totalMins / 60.0),
                TotalCalories = totalCal,
                AverageRpe = Math.Round(avgRpe, 1),
                SearchTerm = search,
                FilterStatus = status,
                FilterPlanId = planId,
                FromDate = fromDate,
                ToDate = toDate,
                AvailablePlans = availablePlans
            };

            return View(viewModel);
        }

        // 5. POST: /Client/Workout/CompleteWorkout
        // Mark workout completed and record duration, calories, exertion & notes
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CompleteWorkout(LogWorkoutSubmissionModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.ClientId) && !AuthorizationHelper.IsClientOwner(Session, model.ClientId))
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Access Denied: You are not authorized to record workouts for another client." });
                return new HttpStatusCodeResult(403, "Access Denied: You are not authorized to record workouts for another client.");
            }

            var client = GetCurrentClient(model.ClientId);
            if (client == null)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Client session not found." });
                return RedirectToAction("Index");
            }

            if (model.WorkoutPlanId.HasValue && !db.WorkoutPlans.Any(p => p.WorkoutPlanId == model.WorkoutPlanId.Value && p.ClientId == client.ClientId))
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Access Denied: The specified workout plan does not belong to you." });
                return new HttpStatusCodeResult(403, "Access Denied: The specified workout plan does not belong to you.");
            }

            try
            {
                var logDate = model.LogDate ?? DateTime.Now;

                // Check if a log already exists for this client and day
                var dateStart = logDate.Date;
                var dateEnd = dateStart.AddDays(1);
                var existingLog = db.ClientWorkoutLogs
                    .FirstOrDefault(l => l.ClientId == client.ClientId && l.LogDate >= dateStart && l.LogDate < dateEnd);

                if (existingLog != null)
                {
                    existingLog.WorkoutPlanId = model.WorkoutPlanId ?? existingLog.WorkoutPlanId;
                    existingLog.SessionName = !string.IsNullOrWhiteSpace(model.SessionName) ? model.SessionName : existingLog.SessionName;
                    existingLog.DurationMinutes = model.DurationMinutes > 0 ? model.DurationMinutes : existingLog.DurationMinutes;
                    existingLog.CaloriesBurned = model.CaloriesBurned > 0 ? model.CaloriesBurned : existingLog.CaloriesBurned;
                    existingLog.IsCompleted = true;
                    existingLog.PerceivedExertion = model.PerceivedExertion > 0 ? model.PerceivedExertion : existingLog.PerceivedExertion;
                    if (!string.IsNullOrWhiteSpace(model.ClientNotes))
                    {
                        existingLog.ClientNotes = model.ClientNotes;
                    }
                }
                else
                {
                    var newLog = new ClientWorkoutLogs
                    {
                        ClientId = client.ClientId,
                        WorkoutPlanId = model.WorkoutPlanId,
                        LogDate = logDate,
                        SessionName = !string.IsNullOrWhiteSpace(model.SessionName) ? model.SessionName : (DateTime.Today.DayOfWeek + " Workout Session"),
                        DurationMinutes = model.DurationMinutes > 0 ? model.DurationMinutes : 45,
                        CaloriesBurned = model.CaloriesBurned > 0 ? model.CaloriesBurned : 320,
                        IsCompleted = true,
                        PerceivedExertion = model.PerceivedExertion > 0 ? model.PerceivedExertion : 7,
                        ClientNotes = model.ClientNotes,
                        CoachFeedback = null
                    };
                    db.ClientWorkoutLogs.Add(newLog);
                }

                db.SaveChanges();

                if (Request.IsAjaxRequest())
                {
                    return Json(new
                    {
                        success = true,
                        message = "🎉 Workout successfully recorded and marked completed! Excellent dedication!",
                        duration = model.DurationMinutes,
                        calories = model.CaloriesBurned
                    });
                }

                TempData["SuccessMessage"] = "🎉 Workout marked completed! Great job pushing your limits today!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = "Error saving workout log: " + ex.Message });
                }
                TempData["ErrorMessage"] = "Could not record workout: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // 6. GET: /Client/Workout/GetExerciseDetails
        // JSON endpoint for modal preview
        [HttpGet]
        public ActionResult GetExerciseDetails(int id)
        {
            var exercise = db.Exercises.FirstOrDefault(e => e.ExerciseId == id);
            if (exercise == null)
            {
                return Json(new { success = false, message = "Exercise not found." }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                success = true,
                exerciseId = exercise.ExerciseId,
                name = exercise.Name,
                category = exercise.Category,
                difficulty = exercise.DifficultyLevel,
                targetMuscle = exercise.TargetMuscle,
                secondaryMuscles = exercise.SecondaryMuscles,
                equipment = exercise.Equipment,
                description = exercise.Description,
                instructions = exercise.Instructions,
                tips = exercise.TipsAndPrecautions,
                startImage = exercise.StartImagePath,
                movementImage = exercise.MovementImagePath,
                imagePath = exercise.ImagePath,
                movementPattern = exercise.MovementPattern,
                keyGuidance = exercise.KeyGuidance,
                burn = exercise.DefaultCaloriesBurnEstimate
            }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
