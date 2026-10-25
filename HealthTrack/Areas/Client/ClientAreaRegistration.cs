using System.Web.Mvc;

namespace HealthTrack.Areas.Client
{
    public class ClientAreaRegistration : AreaRegistration 
    {
        public override string AreaName 
        {
            get 
            {
                return "Client";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context) 
        {
            context.MapRoute(
                name: "Client_direct",
                url: "Client/{controller}",
                defaults: new { action = "Index" },
                constraints: new { controller = "Dashboard|Workout|Diet|Feedback|TrainerChange|Notifications|Profile|Goals|Tracking|Appointments" },
                namespaces: new[] { "HealthTrack.Areas.Client.Controllers" }
            );

            context.MapRoute(
                name: "Client_default",
                url: "Client/{controller}/{action}/{id}",
                defaults: new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional },
                namespaces: new[] { "HealthTrack.Areas.Client.Controllers" }
            );
        }
    }
}
