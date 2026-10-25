using System.Web.Mvc;

namespace HealthTrack.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration 
    {
        public override string AreaName 
        {
            get 
            {
                return "Admin";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context) 
        {
            context.MapRoute(
                name: "Admin_Login",
                url: "Admin/Login",
                defaults: new { controller = "Account", action = "Login" },
                namespaces: new[] { "HealthTrack.Areas.Admin.Controllers" }
            );

            context.MapRoute(
                name: "Admin_default",
                url: "Admin/{controller}/{action}/{id}",
                defaults: new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional },
                namespaces: new[] { "HealthTrack.Areas.Admin.Controllers" }
            );
        }
    }
}
