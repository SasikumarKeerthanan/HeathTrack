using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace HealthTrack.Helpers
{
    /// Action filter to protect Trainer Area controllers and actions.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class TrainerAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException(nameof(filterContext));
            }

            var session = filterContext.HttpContext.Session;

            // 1. Check if user has an active Trainer session
            bool isAuthenticatedTrainer = session != null &&
                                          SessionHelper.IsTrainer(session) &&
                                          session["CurrentTrainerId"] != null &&
                                          !string.IsNullOrWhiteSpace(session["CurrentTrainerId"].ToString());

            // 2. Prevent browser from caching protected trainer pages (ensures Back button after logout doesn't show dashboard)
            var response = filterContext.HttpContext.Response;
            response.Cache.SetCacheability(HttpCacheability.NoCache);
            response.Cache.SetNoStore();
            response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));

            // 3. If not an authenticated trainer, redirect to Trainer Login
            if (!isAuthenticatedTrainer)
            {
                // If it's an AJAX request, return HTTP 401 Unauthorized
                if (filterContext.HttpContext.Request.IsAjaxRequest())
                {
                    filterContext.Result = new HttpStatusCodeResult(401, "Session expired or unauthorized access.");
                    return;
                }

                // Capture requested URL for redirect back after login
                string returnUrl = filterContext.HttpContext.Request.RawUrl;

                // User-friendly flash warning
                if (filterContext.Controller.TempData != null)
                {
                    filterContext.Controller.TempData["WarningMessage"] = "Please sign in to access the Trainer Portal.";
                }

                // Redirect to /Trainer/Account/Login
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "area", "Trainer" },
                        { "controller", "Account" },
                        { "action", "Login" },
                        { "returnUrl", returnUrl }
                    });
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
