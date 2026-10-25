using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace HealthTrack.Helpers
{
    /// Custom action filter to protect Client Area controllers and actions.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ClientAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException(nameof(filterContext));
            }

            var session = filterContext.HttpContext.Session;

            // Check if user has an active Client session
            bool isAuthenticatedClient = session != null &&
                                          SessionHelper.IsClient(session) &&
                                          session["CurrentClientId"] != null &&
                                          !string.IsNullOrWhiteSpace(session["CurrentClientId"].ToString());

            if (!isAuthenticatedClient)
            {
                // If it's an AJAX request, return HTTP 401 Unauthorized
                if (filterContext.HttpContext.Request.IsAjaxRequest())
                {
                    filterContext.Result = new HttpStatusCodeResult(401, "Session expired or unauthorized access.");
                    return;
                }

                // Capture the requested URL for redirect after login
                string returnUrl = filterContext.HttpContext.Request.RawUrl;

                // Provide a user-friendly flash message
                if (filterContext.Controller.TempData != null)
                {
                    filterContext.Controller.TempData["WarningMessage"] = "Please sign in to access your client dashboard and records.";
                }

                // Redirect to Client Login
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "area", "Client" },
                        { "controller", "Account" },
                        { "action", "Login" },
                        { "returnUrl", returnUrl }
                    });
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
