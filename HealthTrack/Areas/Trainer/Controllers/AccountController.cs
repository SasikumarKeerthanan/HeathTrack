using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HealthTrack.Areas.Trainer.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Trainer.Controllers
{
    /// Account Controller for Trainers: handles Trainer login, logout, password resets, and invitations.
    public class AccountController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // =====================================================================
        // 1. LOGIN (GET: /Trainer/Account/Login)
        // =====================================================================
        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            // If already logged in as a Trainer, redirect to Trainer Dashboard
            if (SessionHelper.IsTrainer(Session))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Trainer" });
            }

            var model = new TrainerLoginViewModel
            {
                ReturnUrl = returnUrl
            };

            // Check Remember Me cookie
            var cookie = Request.Cookies["HealthTrack_Trainer_RememberEmail"];
            if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
            {
                model.Email = cookie.Value;
                model.RememberMe = true;
            }

            return View(model);
        }

        // =====================================================================
        // 2. LOGIN (POST: /Trainer/Account/Login)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(TrainerLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string normalizedEmail = model.Email.Trim().ToLower();
            const string genericError = "Invalid email address or password.";

            // 1. Fetch Trainer from Database
            var trainer = db.Trainers.FirstOrDefault(t => t.Email.ToLower() == normalizedEmail);

            if (trainer == null)
            {
                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = normalizedEmail,
                    UserRole = "Trainer",
                    EventType = "LOGIN_FAILED",
                    Details = "Trainer login failed: Email not found in system",
                    TimestampUtc = DateTime.UtcNow
                });
                db.SaveChanges();

                ModelState.AddModelError("", genericError);
                return View(model);
            }

            // 2. Check if Account is Active
            if (!trainer.IsActive)
            {
                ModelState.AddModelError("", "Your trainer account has been deactivated. Please contact an administrator.");
                return View(model);
            }

            // 3. Check if Account was Activated
            if (!trainer.IsInvitationAccepted || string.IsNullOrEmpty(trainer.Password))
            {
                ModelState.AddModelError("", "Your trainer account is not yet activated. Please use the activation invitation email sent by your administrator to create your password.");
                return View(model);
            }

            // 4. Check Brute-force Temporary Lockout (15 minutes)
            if (trainer.LockoutEndUtc.HasValue && trainer.LockoutEndUtc.Value > DateTime.UtcNow)
            {
                int minutesRemaining = (int)Math.Ceiling((trainer.LockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
                if (minutesRemaining < 1) minutesRemaining = 1;

                ModelState.AddModelError("", $"Account is temporarily locked due to multiple failed login attempts. Please try again in {minutesRemaining} minute(s).");
                return View(model);
            }

            // 5. Verify Password against stored salted hash (FR-S-05)
            bool isPasswordValid = PasswordHelper.VerifyPassword(model.Password, trainer.Password);

            // Safe legacy migration: If account still contains an unhashed password, upgrade to PBKDF2 on valid login
            if (!isPasswordValid && !PasswordHelper.IsHashed(trainer.Password) && model.Password == trainer.Password)
            {
                isPasswordValid = true;
                trainer.Password = PasswordHelper.HashPassword(model.Password);
                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = trainer.TrainerId,
                    UserRole = "Trainer",
                    EventType = "PASSWORD_MIGRATED",
                    Details = $"Legacy plaintext password for trainer {trainer.FullName} was automatically upgraded to PBKDF2 salted hash upon login",
                    TimestampUtc = DateTime.UtcNow
                });
            }

            if (!isPasswordValid)
            {
                trainer.FailedLoginAttempts += 1;

                if (trainer.FailedLoginAttempts >= 5)
                {
                    trainer.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                    db.AuthAuditLogs.Add(new AuthAuditLogs
                    {
                        UserId = trainer.TrainerId,
                        UserRole = "Trainer",
                        EventType = "ACCOUNT_LOCKED",
                        Details = "Trainer account locked for 15 minutes after 5 failed login attempts",
                        TimestampUtc = DateTime.UtcNow
                    });
                    ModelState.AddModelError("", "Account temporarily locked for 15 minutes due to 5 consecutive failed login attempts.");
                }
                else
                {
                    int attemptsRemaining = 5 - trainer.FailedLoginAttempts;
                    db.AuthAuditLogs.Add(new AuthAuditLogs
                    {
                        UserId = trainer.TrainerId,
                        UserRole = "Trainer",
                        EventType = "LOGIN_FAILED",
                        Details = $"Incorrect password attempt ({trainer.FailedLoginAttempts}/5)",
                        TimestampUtc = DateTime.UtcNow
                    });
                    ModelState.AddModelError("", $"{genericError} ({attemptsRemaining} attempt(s) remaining before temporary lockout)");
                }

                db.SaveChanges();
                return View(model);
            }

            // 6. Reset failed login attempts on success
            trainer.FailedLoginAttempts = 0;
            trainer.LockoutEndUtc = null;

            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = trainer.TrainerId,
                UserRole = "Trainer",
                EventType = "LOGIN_SUCCESS",
                Details = $"Coach {trainer.FullName} logged in successfully",
                TimestampUtc = DateTime.UtcNow
            });
            db.SaveChanges();

            // 7. Handle Remember Me Cookie
            if (model.RememberMe)
            {
                var cookie = new HttpCookie("HealthTrack_Trainer_RememberEmail", trainer.Email)
                {
                    Expires = DateTime.Now.AddDays(30)
                };
                Response.Cookies.Add(cookie);
            }
            else
            {
                if (Request.Cookies["HealthTrack_Trainer_RememberEmail"] != null)
                {
                    var cookie = new HttpCookie("HealthTrack_Trainer_RememberEmail", "")
                    {
                        Expires = DateTime.Now.AddDays(-1)
                    };
                    Response.Cookies.Add(cookie);
                }
            }

            // 8. Set Trainer Session
            SessionHelper.SetTrainerSession(Session, trainer.TrainerId, trainer.FullName, trainer.Email);

            // 9. Redirect to ReturnUrl or Trainer Dashboard
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Dashboard", new { area = "Trainer" });
        }

        // =====================================================================
        // 3. LOGOUT (GET & POST: /Trainer/Account/Logout)
        // =====================================================================
        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public ActionResult Logout()
        {
            SessionHelper.ClearSession(Session);

            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));

            TempData["InfoMessage"] = "You have been successfully logged out of the Trainer Portal.";
            return RedirectToAction("Login", "Account", new { area = "Trainer" });
        }

        // =====================================================================
        // 4. FORGOT PASSWORD (POST: /Trainer/Account/ForgotPassword) (FR-S-11)
        // Sends password reset email link to the Trainer
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = "Please enter your registered email address." });
            }

            string normalizedEmail = email.Trim().ToLower();
            var trainer = db.Trainers.FirstOrDefault(t => t.Email.ToLower() == normalizedEmail);

            if (trainer == null)
            {
                return Json(new { success = false, message = "No trainer account found with this email address." });
            }

            if (!trainer.IsActive)
            {
                return Json(new { success = false, message = "This trainer account is inactive. Please contact an administrator." });
            }

            try
            {
                // Generate 24-hour reset token
                string resetToken = Guid.NewGuid().ToString("N");
                trainer.InvitationToken = resetToken;
                trainer.InvitationExpiresUtc = DateTime.UtcNow.AddHours(24);

                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = trainer.TrainerId,
                    UserRole = "Trainer",
                    EventType = "PASSWORD_RESET_REQUEST",
                    Details = $"Password reset link requested for trainer {trainer.Email}",
                    TimestampUtc = DateTime.UtcNow
                });
                db.SaveChanges();

                // Build reset link
                string resetLink = Url.Action("ResetPassword", "Account", new
                {
                    area = "Trainer",
                    email = trainer.Email,
                    token = resetToken
                }, Request.Url.Scheme);

                // Dispatch email
                string smtpError;
                bool emailSent = EmailHelper.SendPasswordResetEmail(trainer.Email, trainer.FullName, resetLink, out smtpError);

                if (emailSent)
                {
                    return Json(new
                    {
                        success = true,
                        message = $"Password reset instructions have been sent to {trainer.Email}! Please check your inbox and spam folder."
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = true,
                        isDemo = true,
                        resetLink = resetLink,
                        message = $"Password reset link generated! (SMTP Note: {smtpError}) Click the button below to reset your password:"
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // =====================================================================
        // 5. RESET PASSWORD (GET: /Trainer/Account/ResetPassword)
        // =====================================================================
        [HttpGet]
        public ActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                TempData["ErrorMessage"] = "Invalid or expired password reset link.";
                return RedirectToAction("Login");
            }

            string normalizedEmail = email.Trim().ToLower();
            var trainer = db.Trainers.FirstOrDefault(t => t.Email.ToLower() == normalizedEmail);

            if (trainer == null)
            {
                TempData["ErrorMessage"] = "Trainer account not found.";
                return RedirectToAction("Login");
            }

            if (string.IsNullOrEmpty(trainer.InvitationToken) || !trainer.InvitationToken.Equals(token, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Invalid or expired reset token.";
                return RedirectToAction("Login");
            }

            if (trainer.InvitationExpiresUtc.HasValue && trainer.InvitationExpiresUtc.Value < DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "This reset link has expired. Please request a new one.";
                return RedirectToAction("Login");
            }

            var model = new TrainerResetPasswordViewModel
            {
                Email = trainer.Email,
                Token = token
            };

            return View(model);
        }

        // =====================================================================
        // 6. RESET PASSWORD (POST: /Trainer/Account/ResetPassword)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(TrainerResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string normalizedEmail = model.Email.Trim().ToLower();
            var trainer = db.Trainers.FirstOrDefault(t => t.Email.ToLower() == normalizedEmail);

            if (trainer == null)
            {
                ModelState.AddModelError("", "Trainer account not found.");
                return View(model);
            }

            if (string.IsNullOrEmpty(trainer.InvitationToken) || !trainer.InvitationToken.Equals(model.Token, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Invalid or expired reset token.");
                return View(model);
            }

            if (trainer.InvitationExpiresUtc.HasValue && trainer.InvitationExpiresUtc.Value < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "This reset token has expired.");
                return View(model);
            }

            // Update with salted & hashed password
            trainer.Password = PasswordHelper.HashPassword(model.Password);
            trainer.InvitationToken = null;
            trainer.InvitationExpiresUtc = null;
            trainer.FailedLoginAttempts = 0;
            trainer.LockoutEndUtc = null;

            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = trainer.TrainerId,
                UserRole = "Trainer",
                EventType = "PASSWORD_RESET_SUCCESS",
                Details = $"Coach {trainer.FullName} reset their password successfully",
                TimestampUtc = DateTime.UtcNow
            });
            db.SaveChanges();

            TempData["SuccessMessage"] = "Your password has been successfully reset! You can now log in.";
            return RedirectToAction("Login");
        }

        // =====================================================================
        // 7. ACCEPT INVITATION (GET: /Trainer/Account/AcceptInvitation)
        // =====================================================================
        [HttpGet]
        public ActionResult AcceptInvitation(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                TempData["ErrorMessage"] = "Invalid invitation link.";
                return RedirectToAction("Login");
            }

            string normalizedEmail = email.Trim().ToLower();
            var trainer = db.Trainers.FirstOrDefault(t => t.Email.ToLower() == normalizedEmail);

            if (trainer == null)
            {
                TempData["ErrorMessage"] = "No trainer account was found for this invitation.";
                return RedirectToAction("Login");
            }

            if (trainer.IsInvitationAccepted)
            {
                TempData["InfoMessage"] = "Your trainer account is already activated. Please sign in below.";
                return RedirectToAction("Login");
            }

            if (string.IsNullOrEmpty(trainer.InvitationToken) || !trainer.InvitationToken.Equals(token, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "This invitation link is invalid or has already been used.";
                return RedirectToAction("Login");
            }

            if (trainer.InvitationExpiresUtc.HasValue && trainer.InvitationExpiresUtc.Value < DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "This invitation link has expired. Please contact your gym administrator to resend an invitation.";
                return RedirectToAction("Login");
            }

            var model = new AcceptInvitationViewModel
            {
                Email = trainer.Email,
                Token = token,
                TrainerName = trainer.FullName
            };

            return View(model);
        }

        // =====================================================================
        // 8. ACCEPT INVITATION (POST: /Trainer/Account/AcceptInvitation)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AcceptInvitation(AcceptInvitationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string normalizedEmail = model.Email.Trim().ToLower();
            var trainer = db.Trainers.FirstOrDefault(t => t.Email.ToLower() == normalizedEmail);

            if (trainer == null)
            {
                TempData["ErrorMessage"] = "Trainer account not found.";
                return RedirectToAction("Login");
            }

            if (trainer.IsInvitationAccepted)
            {
                TempData["InfoMessage"] = "Your account is already active. Please sign in.";
                return RedirectToAction("Login");
            }

            if (string.IsNullOrEmpty(trainer.InvitationToken) || !trainer.InvitationToken.Equals(model.Token, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Invalid or expired invitation token.");
                return View(model);
            }

            if (trainer.InvitationExpiresUtc.HasValue && trainer.InvitationExpiresUtc.Value < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "This invitation token has expired. Please contact an administrator.");
                return View(model);
            }

            try
            {
                // Hash the password with per-password salt using PBKDF2
                string saltedHash = PasswordHelper.HashPassword(model.Password);

                trainer.Password = saltedHash;
                trainer.IsInvitationAccepted = true;
                trainer.InvitationToken = null;
                trainer.InvitationExpiresUtc = null;
                trainer.IsActive = true;
                trainer.FailedLoginAttempts = 0;
                trainer.LockoutEndUtc = null;

                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = trainer.TrainerId,
                    UserRole = "Trainer",
                    EventType = "INVITATION_ACCEPTED",
                    Details = $"Trainer {trainer.TrainerId} ({trainer.Email}) activated account and set initial password",
                    TimestampUtc = DateTime.UtcNow
                });
                db.SaveChanges();

                // Set session and redirect to dashboard
                SessionHelper.SetTrainerSession(Session, trainer.TrainerId, trainer.FullName, trainer.Email);

                TempData["SuccessMessage"] = $"Welcome to HealthTrack, Coach {trainer.FullName}! Your trainer account is now active.";
                return RedirectToAction("Index", "Dashboard", new { area = "Trainer" });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while activating your account: " + ex.Message);
                return View(model);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
