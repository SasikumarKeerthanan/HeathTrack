using System.Web;

namespace HealthTrack.Helpers
{
    public static class SessionHelper
    {
        /// Retrieves the authenticated Client ID from the current session.
        public static string GetCurrentClientId(HttpSessionStateBase session)
        {
            if (session != null && session["CurrentClientId"] != null)
            {
                return session["CurrentClientId"].ToString();
            }
            return null;
        }

        /// Retrieves the authenticated Trainer ID from the current session.
        public static string GetCurrentTrainerId(HttpSessionStateBase session)
        {
            if (session != null && session["CurrentTrainerId"] != null)
            {
                return session["CurrentTrainerId"].ToString();
            }
            return null;
        }

        /// Retrieves the authenticated Administrator ID from the current session.
        public static string GetCurrentAdminId(HttpSessionStateBase session)
        {
            if (session == null) return null;
            if (session["AdminId"] != null && !string.IsNullOrWhiteSpace(session["AdminId"].ToString()))
            {
                return session["AdminId"].ToString();
            }
            if (session["UserId"] != null && IsAdmin(session))
            {
                return session["UserId"].ToString();
            }
            return null;
        }

        public static void SetClientSession(HttpSessionStateBase session, string clientId, string clientName, string email = "", string avatar = null)
        {
            if (session == null) return;
            session["UserId"] = clientId;
            session["CurrentClientId"] = clientId;
            session["CurrentClientName"] = clientName;
            session["CurrentClientEmail"] = email;
            session["CurrentClientAvatar"] = avatar;
            session["Role"] = "Client";
        }

        public static void SetTrainerSession(HttpSessionStateBase session, string trainerId, string trainerName, string email = "")
        {
            if (session == null) return;
            session["UserId"] = trainerId;
            session["CurrentTrainerId"] = trainerId;
            session["CurrentTrainerName"] = trainerName;
            session["CurrentTrainerEmail"] = email;
            session["Role"] = "Trainer";
        }

        public static void SetAdminSession(HttpSessionStateBase session, string adminId, string adminName, string email = "")
        {
            if (session == null) return;
            session["UserId"] = adminId;
            session["AdminId"] = adminId;
            session["AdminName"] = adminName;
            session["AdminEmail"] = email;
            session["Role"] = "Admin";
            session["IsAdmin"] = true;
        }

        public static bool IsClient(HttpSessionStateBase session)
        {
            return session != null && 
                   "Client".Equals(session["Role"] as string, System.StringComparison.OrdinalIgnoreCase) &&
                   session["CurrentClientId"] != null &&
                   !string.IsNullOrWhiteSpace(session["CurrentClientId"].ToString());
        }

        public static bool IsTrainer(HttpSessionStateBase session)
        {
            return session != null && 
                   "Trainer".Equals(session["Role"] as string, System.StringComparison.OrdinalIgnoreCase) &&
                   session["CurrentTrainerId"] != null &&
                   !string.IsNullOrWhiteSpace(session["CurrentTrainerId"].ToString());
        }

        public static bool IsAdmin(HttpSessionStateBase session)
        {
            return session != null && 
                   ("Admin".Equals(session["Role"] as string, System.StringComparison.OrdinalIgnoreCase) ||
                    session["AdminId"] != null ||
                    session["IsAdmin"] != null);
        }

        public static void ClearSession(HttpSessionStateBase session)
        {
            if (session == null) return;
            session.Clear();
            session.Abandon();
        }
    }
}
