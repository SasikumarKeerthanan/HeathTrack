using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Admin.ViewModels;
using HealthTrack.Models;
using HealthTrack.Helpers;

namespace HealthTrack.Areas.Admin.Controllers
{
    /// Admin Controller for Intelligent Client-Trainer Assignment.
    [AdminAuthorize]
    public class AssignmentsController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // =====================================================================
        // 1. ASSIGNMENTS DASHBOARD (GET: /Admin/Assignments)
        // =====================================================================
        [HttpGet]
        public ActionResult Index(string clientId = null, string filter = "unassigned", string search = null)
        {
            // 1. Fetch all clients and their preferences
            var allClientsQuery = db.Clients
                .Include(c => c.ClientPreferences)
                .Include(c => c.Trainers)
                .OrderBy(c => c.FullName)
                .AsQueryable();

            var totalClients = allClientsQuery.Count();
            var unassignedCount = allClientsQuery.Count(c => string.IsNullOrEmpty(c.AssignedTrainerId));
            var assignedCount = allClientsQuery.Count(c => !string.IsNullOrEmpty(c.AssignedTrainerId));
            var totalTrainers = db.Trainers.Count(t => t.IsActive);

            // Apply filter ("unassigned", "assigned", "all")
            if (string.Equals(filter, "unassigned", StringComparison.OrdinalIgnoreCase))
            {
                allClientsQuery = allClientsQuery.Where(c => string.IsNullOrEmpty(c.AssignedTrainerId));
            }
            else if (string.Equals(filter, "assigned", StringComparison.OrdinalIgnoreCase))
            {
                allClientsQuery = allClientsQuery.Where(c => !string.IsNullOrEmpty(c.AssignedTrainerId));
            }

            // Apply search filter (name or email)
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLower();
                allClientsQuery = allClientsQuery.Where(c => c.FullName.ToLower().Contains(s) || c.Email.ToLower().Contains(s));
            }

            var clientEntities = allClientsQuery.ToList();

            // Transform into view models
            var clientItems = clientEntities.Select(c =>
            {
                var pref = c.ClientPreferences.FirstOrDefault();
                return new ClientAssignmentItemViewModel
                {
                    ClientId = c.ClientId,
                    FullName = c.FullName,
                    Email = c.Email,
                    Gender = c.Gender,
                    AssignedTrainerId = c.AssignedTrainerId,
                    AssignedTrainerName = c.Trainers != null ? c.Trainers.FullName : null,
                    IsActive = c.IsActive,
                    PrimaryGoal = pref != null ? pref.PrimaryGoal : "General Fitness",
                    PreferredWorkoutType = pref != null ? pref.PreferredWorkoutType : "Strength",
                    FitnessLevel = pref != null ? pref.FitnessLevel : "Beginner",
                    PreferredTrainerGender = pref != null ? pref.PreferredTrainerGender : null,
                    PreferredDietType = pref != null ? pref.PreferredDietType : "Balanced",
                    TargetDaysPerWeek = pref != null ? pref.TargetDaysPerWeek : 3
                };
            }).ToList();

            // 2. Select active client for match scoring
            ClientAssignmentItemViewModel selectedClient = null;
            if (!string.IsNullOrEmpty(clientId))
            {
                selectedClient = clientItems.FirstOrDefault(c => c.ClientId == clientId);
                if (selectedClient == null)
                {
                    // Look in whole database if filtered out
                    var singleClient = db.Clients.Include(c => c.ClientPreferences).Include(c => c.Trainers).FirstOrDefault(c => c.ClientId == clientId);
                    if (singleClient != null)
                    {
                        var pref = singleClient.ClientPreferences.FirstOrDefault();
                        selectedClient = new ClientAssignmentItemViewModel
                        {
                            ClientId = singleClient.ClientId,
                            FullName = singleClient.FullName,
                            Email = singleClient.Email,
                            Gender = singleClient.Gender,
                            AssignedTrainerId = singleClient.AssignedTrainerId,
                            AssignedTrainerName = singleClient.Trainers != null ? singleClient.Trainers.FullName : null,
                            IsActive = singleClient.IsActive,
                            PrimaryGoal = pref != null ? pref.PrimaryGoal : "General Fitness",
                            PreferredWorkoutType = pref != null ? pref.PreferredWorkoutType : "Strength",
                            FitnessLevel = pref != null ? pref.FitnessLevel : "Beginner",
                            PreferredTrainerGender = pref != null ? pref.PreferredTrainerGender : null,
                            PreferredDietType = pref != null ? pref.PreferredDietType : "Balanced",
                            TargetDaysPerWeek = pref != null ? pref.TargetDaysPerWeek : 3
                        };
                    }
                }
            }

            if (selectedClient == null && clientItems.Any())
            {
                selectedClient = clientItems.FirstOrDefault();
            }

            // 3. Compute Compatibility Match Scores for all active trainers
            var trainerMatches = new List<TrainerMatchCardViewModel>();

            if (selectedClient != null)
            {
                var activeTrainers = db.Trainers.Where(t => t.IsActive).ToList();

                foreach (var trainer in activeTrainers)
                {
                    int activeLoad = db.Clients.Count(c => c.AssignedTrainerId == trainer.TrainerId && c.IsActive);
                    var matchCard = CalculateMatch(selectedClient, trainer, activeLoad);
                    trainerMatches.Add(matchCard);
                }

                // Rank trainers: highest match score first, then most available capacity
                trainerMatches = trainerMatches
                    .OrderByDescending(t => t.MatchPercentage)
                    .ThenByDescending(t => t.CapacityRemaining)
                    .ToList();
            }

            var model = new ClientAssignmentIndexViewModel
            {
                Clients = clientItems,
                SelectedClient = selectedClient,
                TrainerMatches = trainerMatches,
                Filter = filter,
                SearchTerm = search,
                TotalClientsCount = totalClients,
                UnassignedClientsCount = unassignedCount,
                AssignedClientsCount = assignedCount,
                TotalTrainersCount = totalTrainers
            };

            return View(model);
        }

        // =====================================================================
        // 2. ASSIGN TRAINER (POST: /Admin/Assignments/Assign)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Assign(string clientId, string trainerId, string filter = "unassigned")
        {
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(trainerId))
            {
                TempData["ErrorMessage"] = "Please select both a valid client and trainer.";
                return RedirectToAction("Index", new { clientId, filter });
            }

            var client = db.Clients.Find(clientId);
            if (client == null)
            {
                TempData["ErrorMessage"] = "Client could not be found.";
                return RedirectToAction("Index", new { filter });
            }

            var trainer = db.Trainers.Find(trainerId);
            if (trainer == null)
            {
                TempData["ErrorMessage"] = "Selected trainer could not be found.";
                return RedirectToAction("Index", new { clientId, filter });
            }

            // Update assignment
            client.AssignedTrainerId = trainerId;

            // Audit Log: Track assignment creation
            string currentAdminId = Session["AdminId"] as string ?? "Admin";
            string currentAdminEmail = Session["AdminEmail"] as string ?? "admin@healthtrack.com";
            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = currentAdminId,
                UserRole = "Admin",
                EventType = "TRAINER_ASSIGNED",
                Details = $"Administrator '{currentAdminEmail}' (ID: {currentAdminId}) assigned Coach '{trainer.FullName}' (ID: {trainerId}) to Client '{client.FullName}' (ID: {client.ClientId}).",
                TimestampUtc = DateTime.UtcNow
            });

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Coach {trainer.FullName} was successfully assigned to {client.FullName}!";
            return RedirectToAction("Index", new { clientId = clientId, filter = "assigned" });
        }

        // =====================================================================
        // 3. REMOVE ASSIGNMENT (POST: /Admin/Assignments/RemoveAssignment)
        // FR-A-18: Remove Trainer Assignments While Preserving History
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveAssignment(string clientId, string filter = "assigned")
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                TempData["ErrorMessage"] = "Client ID is required to remove an assignment.";
                return RedirectToAction("Index", new { filter });
            }

            var client = db.Clients
                .Include(c => c.Trainers)
                .FirstOrDefault(c => c.ClientId == clientId);

            if (client == null)
            {
                TempData["ErrorMessage"] = "Client record not found.";
                return RedirectToAction("Index", new { filter });
            }

            // Confirm that the assignment is currently active (prevents repeated removals)
            if (string.IsNullOrEmpty(client.AssignedTrainerId))
            {
                TempData["WarningMessage"] = $"Client '{client.FullName}' does not have an active trainer assignment to remove.";
                return RedirectToAction("Index", new { clientId = client.ClientId, filter = "unassigned" });
            }

            string previousTrainerId = client.AssignedTrainerId;
            string previousTrainerName = client.Trainers != null ? client.Trainers.FullName : previousTrainerId;
            string clientName = client.FullName;
            string adminId = Session["AdminId"] as string ?? "Admin";
            string adminEmail = Session["AdminEmail"] as string ?? "admin@healthtrack.com";

            // Mark the active assignment as ended/removed.
            // Historical entities (WorkoutPlans, DietPlans, Appointments, TrainerFeedback, ClientProgressRecords)
            // retain their ClientId and TrainerId foreign keys and remain 100% intact.
            client.AssignedTrainerId = null;

            // Audit Trail: Preserves historical record of which Administrator removed the assignment,
            // the Trainer, the Client, and the exact timestamp.
            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = adminId,
                UserRole = "Admin",
                EventType = "TRAINER_ASSIGNMENT_REMOVED",
                Details = $"Administrator '{adminEmail}' (ID: {adminId}) removed active assignment between Coach '{previousTrainerName}' (ID: {previousTrainerId}) and Client '{clientName}' (ID: {client.ClientId}). Historical records preserved.",
                TimestampUtc = DateTime.UtcNow
            });

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Active assignment between Coach {previousTrainerName} and {clientName} was successfully removed. All historical workout, diet, feedback, and appointment records have been preserved.";
            return RedirectToAction("Index", new { clientId = client.ClientId, filter = "unassigned" });
        }

        // Backward compatibility alias for Unassign
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Unassign(string clientId, string filter = "assigned")
        {
            return RemoveAssignment(clientId, filter);
        }

        // =====================================================================
        // 4. ASSIGNMENT HISTORY (GET: /Admin/Assignments/History)
        // FR-A-19: View Trainer–Client Assignment History
        // =====================================================================
        [HttpGet]
        public ActionResult History(string status = "all", string search = null)
        {
            var model = new AssignmentHistoryIndexViewModel
            {
                StatusFilter = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLower(),
                SearchTerm = search?.Trim()
            };

            // Pre-load trainers for fast ID-to-Name resolution
            var trainersLookup = db.Trainers
                .ToDictionary(t => t.TrainerId, t => new { t.FullName, Specialization = t.PrimarySpecialization ?? t.Specialization ?? "General Fitness" });

            // Pre-load clients for fast ID-to-Details resolution
            var clientsLookup = db.Clients
                .ToDictionary(c => c.ClientId, c => new { c.FullName, c.Email, c.CreatedAt });

            var historyItems = new List<AssignmentHistoryItemViewModel>();

            // 1. Current Active Assignments (from db.Clients where AssignedTrainerId != null)
            var activeAssignments = db.Clients
                .Include(c => c.Trainers)
                .Where(c => !string.IsNullOrEmpty(c.AssignedTrainerId))
                .ToList();

            foreach (var client in activeAssignments)
            {
                var assignAudit = db.AuthAuditLogs
                    .Where(l => l.EventType == "TRAINER_ASSIGNED" && l.Details.Contains(client.ClientId))
                    .OrderByDescending(l => l.TimestampUtc)
                    .FirstOrDefault();

                DateTime assignDate = assignAudit != null ? assignAudit.TimestampUtc : client.CreatedAt;

                historyItems.Add(new AssignmentHistoryItemViewModel
                {
                    ClientId = client.ClientId,
                    ClientName = client.FullName,
                    ClientEmail = client.Email,
                    TrainerId = client.AssignedTrainerId,
                    TrainerName = client.Trainers != null ? client.Trainers.FullName : client.AssignedTrainerId,
                    TrainerSpecialization = client.Trainers != null ? (client.Trainers.PrimarySpecialization ?? client.Trainers.Specialization ?? "General Fitness") : "General Fitness",
                    AssignmentDate = assignDate,
                    EndDate = null,
                    Status = "Active",
                    AdminActionBy = assignAudit != null ? assignAudit.UserId : "Administrator",
                    Details = "Current active trainer-client relationship."
                });
            }

            // 2. Ended Assignments (from db.AuthAuditLogs where EventType == 'TRAINER_ASSIGNMENT_REMOVED')
            var removalLogs = db.AuthAuditLogs
                .Where(l => l.EventType == "TRAINER_ASSIGNMENT_REMOVED")
                .OrderByDescending(l => l.TimestampUtc)
                .ToList();

            foreach (var log in removalLogs)
            {
                string cId = null;
                string tId = null;
                string cName = null;
                string tName = null;

                int cIdx = log.Details.IndexOf("(ID: C", StringComparison.OrdinalIgnoreCase);
                if (cIdx >= 0)
                {
                    int cEnd = log.Details.IndexOf(")", cIdx);
                    if (cEnd > cIdx) cId = log.Details.Substring(cIdx + 5, cEnd - (cIdx + 5)).Trim();
                }

                int tIdx = log.Details.IndexOf("(ID: T", StringComparison.OrdinalIgnoreCase);
                if (tIdx >= 0)
                {
                    int tEnd = log.Details.IndexOf(")", tIdx);
                    if (tEnd > tIdx) tId = log.Details.Substring(tIdx + 5, tEnd - (tIdx + 5)).Trim();
                }

                if (string.IsNullOrEmpty(cId) && clientsLookup.ContainsKey(log.UserId))
                {
                    cId = log.UserId;
                }

                if (!string.IsNullOrEmpty(cId) && clientsLookup.ContainsKey(cId))
                {
                    cName = clientsLookup[cId].FullName;
                }

                if (!string.IsNullOrEmpty(tId) && trainersLookup.ContainsKey(tId))
                {
                    tName = trainersLookup[tId].FullName;
                }

                historyItems.Add(new AssignmentHistoryItemViewModel
                {
                    ClientId = cId ?? "Historical Client",
                    ClientName = cName ?? "Client Member",
                    ClientEmail = cId != null && clientsLookup.ContainsKey(cId) ? clientsLookup[cId].Email : "",
                    TrainerId = tId ?? "Historical Trainer",
                    TrainerName = tName ?? (tId ?? "Trainer Coach"),
                    TrainerSpecialization = tId != null && trainersLookup.ContainsKey(tId) ? trainersLookup[tId].Specialization : "General Fitness",
                    AssignmentDate = cId != null && clientsLookup.ContainsKey(cId) ? clientsLookup[cId].CreatedAt : log.TimestampUtc.AddDays(-30),
                    EndDate = log.TimestampUtc,
                    Status = "Ended",
                    AdminActionBy = log.UserId ?? "Administrator",
                    Details = log.Details
                });
            }

            // 3. Historical Reassignments from Approved TrainerChangeRequests
            var approvedChanges = db.TrainerChangeRequests
                .Include(r => r.Clients)
                .Where(r => r.Status == "Approved")
                .OrderByDescending(r => r.ReviewedAt)
                .ToList();

            foreach (var req in approvedChanges)
            {
                if (!string.IsNullOrEmpty(req.CurrentTrainerId))
                {
                    string oldTrainerName = trainersLookup.ContainsKey(req.CurrentTrainerId) ? trainersLookup[req.CurrentTrainerId].FullName : req.CurrentTrainerId;
                    string spec = trainersLookup.ContainsKey(req.CurrentTrainerId) ? trainersLookup[req.CurrentTrainerId].Specialization : "General Fitness";

                    historyItems.Add(new AssignmentHistoryItemViewModel
                    {
                        ClientId = req.ClientId,
                        ClientName = req.Clients != null ? req.Clients.FullName : req.ClientId,
                        ClientEmail = req.Clients != null ? req.Clients.Email : "",
                        TrainerId = req.CurrentTrainerId,
                        TrainerName = oldTrainerName,
                        TrainerSpecialization = spec,
                        AssignmentDate = req.RequestedAt,
                        EndDate = req.ReviewedAt ?? req.RequestedAt,
                        Status = "Ended",
                        AdminActionBy = req.ReviewedBy ?? "Administrator",
                        Details = $"Reassigned to Coach {req.NewTrainerId}. Admin notes: {req.AdminNotes}"
                    });
                }
            }

            // Summary metrics
            model.TotalAssignmentsCount = historyItems.Count;
            model.ActiveAssignmentsCount = historyItems.Count(h => h.IsActive);
            model.EndedAssignmentsCount = historyItems.Count(h => !h.IsActive);

            // Apply Status Filter
            var filtered = historyItems.AsEnumerable();
            if (model.StatusFilter == "active")
            {
                filtered = filtered.Where(h => h.IsActive);
            }
            else if (model.StatusFilter == "ended")
            {
                filtered = filtered.Where(h => !h.IsActive);
            }

            // Apply Search Term
            if (!string.IsNullOrWhiteSpace(model.SearchTerm))
            {
                string term = model.SearchTerm.ToLower();
                filtered = filtered.Where(h =>
                    (h.ClientName != null && h.ClientName.ToLower().Contains(term)) ||
                    (h.ClientEmail != null && h.ClientEmail.ToLower().Contains(term)) ||
                    (h.ClientId != null && h.ClientId.ToLower().Contains(term)) ||
                    (h.TrainerName != null && h.TrainerName.ToLower().Contains(term)) ||
                    (h.TrainerId != null && h.TrainerId.ToLower().Contains(term))
                );
            }

            model.HistoryRecords = filtered
                .OrderByDescending(h => h.IsActive)
                .ThenByDescending(h => h.EndDate ?? h.AssignmentDate)
                .ToList();

            return View(model);
        }

        // =====================================================================
        // 4. MATCH SCORE ALGORITHM (Simple, Beginner-Friendly, Weighted 100%)
        // =====================================================================
        private static TrainerMatchCardViewModel CalculateMatch(ClientAssignmentItemViewModel client, Trainers trainer, int activeClientsCount)
        {
            var card = new TrainerMatchCardViewModel
            {
                TrainerId = trainer.TrainerId,
                FullName = trainer.FullName,
                Email = trainer.Email,
                Gender = trainer.Gender,
                PrimarySpecialization = trainer.PrimarySpecialization ?? trainer.Specialization ?? "General Fitness",
                SupportedWorkoutTypes = trainer.SupportedWorkoutTypes ?? "Strength, Cardio",
                SupportedFitnessLevels = trainer.SupportedFitnessLevels ?? "Beginner, Intermediate, Advanced",
                ExperienceYears = trainer.ExperienceYears,
                MaxClientCapacity = trainer.MaxClientCapacity > 0 ? trainer.MaxClientCapacity : 15,
                ActiveClientsCount = activeClientsCount,
                IsCurrentlyAssigned = (client.AssignedTrainerId == trainer.TrainerId)
            };

            var match = TrainerMatchingHelper.CalculateMatch(
                trainer,
                client.PrimaryGoal,
                client.PreferredWorkoutType,
                client.FitnessLevel,
                client.PreferredTrainerGender,
                activeClientsCount
            );

            card.MatchPercentage = match.Score;
            card.GoalMatch = match.GoalMatch;
            card.WorkoutTypeMatch = match.WorkoutTypeMatch;
            card.FitnessLevelMatch = match.FitnessLevelMatch;
            card.GenderMatch = match.GenderMatch;
            card.CapacityMatch = match.CapacityMatch;

            // Assign presentation tier
            if (card.MatchPercentage >= 85)
            {
                card.MatchTier = "Excellent Match";
                card.MatchBadgeClass = "success";
            }
            else if (card.MatchPercentage >= 70)
            {
                card.MatchTier = "Great Match";
                card.MatchBadgeClass = "primary";
            }
            else if (card.MatchPercentage >= 50)
            {
                card.MatchTier = "Moderate Match";
                card.MatchBadgeClass = "warning";
            }
            else
            {
                card.MatchTier = "Basic Match";
                card.MatchBadgeClass = "secondary";
            }

            return card;
        }

        // =====================================================================
        // 4. TRAINER CHANGE REQUESTS (GET: /Admin/Assignments/Requests)
        // =====================================================================
        [HttpGet]
        public ActionResult Requests(string filter = "all")
        {
            var query = db.TrainerChangeRequests
                .Include(r => r.Clients)
                .OrderByDescending(r => r.RequestedAt)
                .AsQueryable();

            int total = query.Count();
            int pending = query.Count(r => r.Status == "Pending");
            int approved = query.Count(r => r.Status == "Approved");
            int rejected = query.Count(r => r.Status == "Rejected");

            if (filter == "pending")
            {
                query = query.Where(r => r.Status == "Pending");
            }
            else if (filter == "approved")
            {
                query = query.Where(r => r.Status == "Approved");
            }
            else if (filter == "rejected")
            {
                query = query.Where(r => r.Status == "Rejected");
            }

            var requests = query.ToList();

            // Load trainer dict for quick lookups
            var allTrainers = db.Trainers.Where(t => t.IsActive).ToList();
            var trainerDict = db.Trainers.ToDictionary(t => t.TrainerId, t => t);

            // Calculate current workload of active trainers
            var clientCounts = db.Clients
                .Where(c => !string.IsNullOrEmpty(c.AssignedTrainerId))
                .GroupBy(c => c.AssignedTrainerId)
                .ToDictionary(g => g.Key, g => g.Count());

            var activeTrainerOptions = allTrainers.Select(t => new TrainerOptionViewModel
            {
                TrainerId = t.TrainerId,
                FullName = t.FullName,
                Specialization = t.PrimarySpecialization ?? t.Specialization ?? "General Fitness",
                Gender = t.Gender,
                CurrentClientsCount = clientCounts.ContainsKey(t.TrainerId) ? clientCounts[t.TrainerId] : 0,
                MaxCapacity = t.MaxClientCapacity > 0 ? t.MaxClientCapacity : 5
            }).OrderBy(t => t.IsFull).ThenBy(t => t.FullName).ToList();

            var items = requests.Select(r =>
            {
                var curT = (!string.IsNullOrEmpty(r.CurrentTrainerId) && trainerDict.ContainsKey(r.CurrentTrainerId))
                    ? trainerDict[r.CurrentTrainerId]
                    : null;

                var newT = (!string.IsNullOrEmpty(r.NewTrainerId) && trainerDict.ContainsKey(r.NewTrainerId))
                    ? trainerDict[r.NewTrainerId]
                    : null;

                var clientPref = r.Clients?.ClientPreferences?.FirstOrDefault();

                return new TrainerChangeItemViewModel
                {
                    RequestId = r.RequestId,
                    ClientId = r.ClientId,
                    ClientName = r.Clients?.FullName ?? "Unknown Client",
                    ClientEmail = r.Clients?.Email ?? "",
                    ClientGender = r.Clients?.Gender ?? "",
                    ClientGoal = clientPref?.PrimaryGoal ?? "General Fitness",
                    CurrentTrainerId = r.CurrentTrainerId,
                    CurrentTrainerName = curT?.FullName ?? r.CurrentTrainerId,
                    CurrentTrainerSpecialization = curT?.PrimarySpecialization ?? curT?.Specialization ?? "Coach",
                    NewTrainerId = r.NewTrainerId,
                    NewTrainerName = newT?.FullName,
                    ReasonCategory = r.ReasonCategory,
                    ReasonDetails = r.ReasonDetails,
                    PreferredGender = r.PreferredGender,
                    Status = r.Status,
                    AdminNotes = r.AdminNotes,
                    RequestedAt = r.RequestedAt,
                    ReviewedAt = r.ReviewedAt,
                    ReviewedBy = r.ReviewedBy
                };
            }).ToList();

            var model = new AdminTrainerChangeIndexViewModel
            {
                TotalRequests = total,
                PendingCount = pending,
                ApprovedCount = approved,
                RejectedCount = rejected,
                ActiveFilter = filter.ToLower(),
                Requests = items,
                ActiveTrainers = activeTrainerOptions
            };

            return View(model);
        }

        // =====================================================================
        // 5. APPROVE REQUEST & REASSIGN (POST: /Admin/Assignments/ApproveRequest)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveRequest(int requestId, string newTrainerId, string adminNotes)
        {
            if (string.IsNullOrWhiteSpace(newTrainerId))
            {
                TempData["ErrorMessage"] = "Please select an active trainer to reassign the client.";
                return RedirectToAction("Requests");
            }

            try
            {
                var request = db.TrainerChangeRequests
                    .Include(r => r.Clients)
                    .FirstOrDefault(r => r.RequestId == requestId);

                if (request == null)
                {
                    TempData["ErrorMessage"] = "Request not found.";
                    return RedirectToAction("Requests");
                }

                if (request.Status != "Pending")
                {
                    TempData["WarningMessage"] = $"This request has already been marked as {request.Status}.";
                    return RedirectToAction("Requests");
                }

                var newTrainer = db.Trainers.FirstOrDefault(t => t.TrainerId == newTrainerId && t.IsActive);
                if (newTrainer == null)
                {
                    TempData["ErrorMessage"] = "The selected trainer is invalid or inactive.";
                    return RedirectToAction("Requests");
                }

                // Update Request status & details
                request.Status = "Approved";
                request.NewTrainerId = newTrainer.TrainerId;
                request.AdminNotes = string.IsNullOrWhiteSpace(adminNotes) ? "Approved and reassigned by administrator." : adminNotes.Trim();
                request.ReviewedAt = DateTime.UtcNow;
                request.ReviewedBy = "Administrator";

                // Reassign the client to new trainer
                var client = request.Clients;
                string prevTrainerId = client.AssignedTrainerId;
                client.AssignedTrainerId = newTrainer.TrainerId;

                // Audit Log
                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = client.ClientId,
                    UserRole = "Client",
                    EventType = "TRAINER_CHANGE_APPROVED",
                    Details = $"Admin approved trainer change for {client.Email}. Reassigned from {prevTrainerId} to {newTrainer.FullName} ({newTrainer.TrainerId}).",
                    TimestampUtc = DateTime.UtcNow
                });

                db.SaveChanges();
                TempData["SuccessMessage"] = $"Request #{requestId} approved! {client.FullName} has been reassigned to Coach {newTrainer.FullName}.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to approve request: " + ex.Message;
            }

            return RedirectToAction("Requests");
        }

        // =====================================================================
        // 6. REJECT REQUEST (POST: /Admin/Assignments/RejectRequest)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectRequest(int requestId, string rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["ErrorMessage"] = "Please provide a reason explaining why the request is rejected.";
                return RedirectToAction("Requests");
            }

            try
            {
                var request = db.TrainerChangeRequests
                    .Include(r => r.Clients)
                    .FirstOrDefault(r => r.RequestId == requestId);

                if (request == null)
                {
                    TempData["ErrorMessage"] = "Request not found.";
                    return RedirectToAction("Requests");
                }

                if (request.Status != "Pending")
                {
                    TempData["WarningMessage"] = $"This request has already been marked as {request.Status}.";
                    return RedirectToAction("Requests");
                }

                // Update Request
                request.Status = "Rejected";
                request.AdminNotes = rejectionReason.Trim();
                request.ReviewedAt = DateTime.UtcNow;
                request.ReviewedBy = "Administrator";

                // Audit Log
                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = request.ClientId,
                    UserRole = "Client",
                    EventType = "TRAINER_CHANGE_REJECTED",
                    Details = $"Admin rejected trainer change for client {request.ClientId}. Reason: {request.AdminNotes}",
                    TimestampUtc = DateTime.UtcNow
                });

                db.SaveChanges();
                TempData["SuccessMessage"] = $"Request #{requestId} rejected and reason recorded successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to reject request: " + ex.Message;
            }

            return RedirectToAction("Requests");
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
