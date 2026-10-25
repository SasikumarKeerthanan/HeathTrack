using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace HealthTrack.Helpers
{
    /// Action filter to protect Admin Area controllers and actions.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class AdminAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException(nameof(filterContext));
            }

            var session = filterContext.HttpContext.Session;

            // 1. Check if user has an active Admin session
            bool isAuthenticatedAdmin = session != null &&
                                         (SessionHelper.IsAdmin(session) ||
                                          session["AdminId"] != null ||
                                          "Admin".Equals(session["Role"] as string, StringComparison.OrdinalIgnoreCase));

            // 2. Prevent browser from caching protected admin pages
            var response = filterContext.HttpContext.Response;
            response.Cache.SetCacheability(HttpCacheability.NoCache);
            response.Cache.SetNoStore();
            response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));

            // 3. If not an authenticated admin, redirect to Admin Login
            if (!isAuthenticatedAdmin)
            {
                // If it's an AJAX request, return HTTP 401 Unauthorized
                if (filterContext.HttpContext.Request.IsAjaxRequest())
                {
                    filterContext.Result = new HttpStatusCodeResult(401, "Session expired or unauthorized access.");
                    return;
                }

                // Capture requested URL for redirect back after login
                string returnUrl = filterContext.HttpContext.Request.RawUrl;

                if (filterContext.Controller.TempData != null)
                {
                    filterContext.Controller.TempData["ErrorMessage"] = "Please sign in to access the Administrator Portal.";
                }

                // Redirect to /Admin/Account/Login
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "area", "Admin" },
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
