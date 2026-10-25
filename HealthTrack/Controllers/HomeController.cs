using System;
using System.Diagnostics;
using System.Web.Mvc;
using HealthTrack.Models;
using HealthTrack.Helpers;

namespace HealthTrack.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            if (SessionHelper.IsClient(Session))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Client" });
            }
            
            if (SessionHelper.IsTrainer(Session))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Trainer" });
            }

            // Default primary page when starting the system:
            return RedirectToAction("Login", "Account", new { area = "Client" });
        }


        public ActionResult Privacy()
        {
            return View();
        }

        [OutputCache(Duration = 0, NoStore = true)]
        public ActionResult Error()
        {
            return View();
        }
    }
}

