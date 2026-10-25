using System.Web.Mvc;

namespace HealthTrack.Areas.Trainer
{
    public class TrainerAreaRegistration : AreaRegistration 
    {
        public override string AreaName 
        {
            get 
            {
                return "Trainer";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context) 
        {
            context.MapRoute(
                name: "Trainer_direct",
                url: "Trainer/{controller}",
                defaults: new { action = "Index" },
                constraints: new { controller = "Dashboard|Clients|Exercises|WorkoutPlans|DietPlans|Feedback|Progress|Profile|Appointments" },
                namespaces: new[] { "HealthTrack.Areas.Trainer.Controllers" }
            );

            context.MapRoute(
                name: "Trainer_default",
                url: "Trainer/{controller}/{action}/{id}",
                defaults: new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional },
                namespaces: new[] { "HealthTrack.Areas.Trainer.Controllers" }
            );
        }
    }
}
