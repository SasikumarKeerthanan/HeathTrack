using System;
using System.Data.Entity;
using System.Linq;
using System.Web;
using HealthTrack.Models;

namespace HealthTrack.Helpers
{
    /// <summary>
    /// Reusable server-side record-level and active-assignment authorization helper (FR-S-04 and FR-S-17).
    /// </summary>
    public static class AuthorizationHelper
    {
        /// <summary>
        /// Confirms whether the authenticated Client is the owner of the requested ClientId (FR-S-04).
        /// Rejects attempts by a client to access, view, modify, or export another client's records.
        /// </summary>
        public static bool IsClientOwner(HttpSessionStateBase session, string requestedClientId)
        {
            if (session == null || string.IsNullOrWhiteSpace(requestedClientId))
            {
                return false;
            }

            string currentClientId = SessionHelper.GetCurrentClientId(session);
            if (string.IsNullOrWhiteSpace(currentClientId))
            {
                return false;
            }

            return string.Equals(currentClientId.Trim(), requestedClientId.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Confirms whether a Trainer has an active, currently valid assignment to the specified Client (FR-S-17).
        /// Returns false for unassigned clients, inactive accounts, or assignments that were ended/removed under FR-A-18.
        /// </summary>
        public static bool IsActiveTrainerClientAssignment(HealthTrackEntities db, string trainerId, string clientId)
        {
            if (db == null || string.IsNullOrWhiteSpace(trainerId) || string.IsNullOrWhiteSpace(clientId))
            {
                return false;
            }

            string cleanTrainerId = trainerId.Trim();
            string cleanClientId = clientId.Trim();

            return db.Clients.Any(c => c.ClientId == cleanClientId &&
                                       c.AssignedTrainerId == cleanTrainerId &&
                                       c.IsActive);
        }

        /// <summary>
        /// Confirms whether a Workout Plan is authored/managed by the trainer and belongs to a currently assigned client (FR-S-04 & FR-S-17).
        /// </summary>
        public static bool CanTrainerManageWorkoutPlan(HealthTrackEntities db, string trainerId, int workoutPlanId)
        {
            if (db == null || string.IsNullOrWhiteSpace(trainerId) || workoutPlanId <= 0)
            {
                return false;
            }

            string cleanTrainerId = trainerId.Trim();
            var plan = db.WorkoutPlans
                .Include(p => p.Clients)
                .FirstOrDefault(p => p.WorkoutPlanId == workoutPlanId);

            if (plan == null)
            {
                return false;
            }

            // Trainer must be the author/trainer of the plan, and the client must be currently assigned and active
            if (!string.Equals(plan.TrainerId, cleanTrainerId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (plan.Clients == null ||
                !string.Equals(plan.Clients.AssignedTrainerId, cleanTrainerId, StringComparison.OrdinalIgnoreCase) ||
                !plan.Clients.IsActive)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Confirms whether a Diet Plan is authored/managed by the trainer and belongs to a currently assigned client (FR-S-04 & FR-S-17).
        /// </summary>
        public static bool CanTrainerManageDietPlan(HealthTrackEntities db, string trainerId, int dietPlanId)
        {
            if (db == null || string.IsNullOrWhiteSpace(trainerId) || dietPlanId <= 0)
            {
                return false;
            }

            string cleanTrainerId = trainerId.Trim();
            var plan = db.DietPlans
                .Include(p => p.Clients)
                .FirstOrDefault(p => p.DietPlanId == dietPlanId);

            if (plan == null)
            {
                return false;
            }

            if (!string.Equals(plan.TrainerId, cleanTrainerId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (plan.Clients == null ||
                !string.Equals(plan.Clients.AssignedTrainerId, cleanTrainerId, StringComparison.OrdinalIgnoreCase) ||
                !plan.Clients.IsActive)
            {
                return false;
            }

            return true;
        }
    }
}
