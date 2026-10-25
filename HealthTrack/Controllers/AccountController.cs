using System.Web.Mvc;

namespace HealthTrack.Controllers
{
    /// Root AccountController that forwards requests to the Client Area AccountController.
    public class AccountController : Controller
    {
        // GET: /Account/Register -> Redirects to /Client/Account/Register
        [HttpGet]
        public ActionResult Register()
        {
            return RedirectToAction("Register", "Account", new { area = "Client" });
        }

        // GET: /Account/Login -> Redirects to /Client/Account/Login
        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            return RedirectToAction("Login", "Account", new { area = "Client", returnUrl = returnUrl });
        }

        // GET/POST: /Account/Logout -> Redirects to /Client/Account/Logout
        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public ActionResult Logout()
        {
            return RedirectToAction("Logout", "Account", new { area = "Client" });
        }

        // POST: /Account/ForgotPassword -> Forwards to /Client/Account/ForgotPassword (FR-S-11)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string email)
        {
            return RedirectToAction("ForgotPassword", "Account", new { area = "Client", email = email });
        }

        // GET: /Account/ResetPassword -> Redirects to /Client/Account/ResetPassword
        [HttpGet]
        public ActionResult ResetPassword(string email, string token)
        {
            return RedirectToAction("ResetPassword", "Account", new { area = "Client", email = email, token = token });
        }
    }
}
