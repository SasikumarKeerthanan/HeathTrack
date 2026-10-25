using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using HealthTrack.Models;
using HealthTrack.Helpers;
using HealthTrack.Areas.Client.ViewModels;
using HealthTrack.ViewModels;

namespace HealthTrack.Areas.Client.Controllers
{
    /// Client Area Authentication Controller: handles login, registration, and logout.
    public class AccountController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // =====================================================================
        // 1. REGISTRATION (GET & POST)
        // =====================================================================

        // GET: /Client/Account/Register
        [HttpGet]
        public ActionResult Register()
        {
            if (SessionHelper.IsClient(Session))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Client" });
            }

            var model = new ClientRegisterViewModel();
            return View(model);
        }

        // POST: /Client/Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(ClientRegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 1. Check if Email is already registered (FR-C-01: Unique Email)
            string normalizedEmail = model.Email.Trim().ToLower();
            bool emailExists = db.Clients.Any(c => c.Email.ToLower() == normalizedEmail);
            if (emailExists)
            {
                ModelState.AddModelError("Email", "An account with this email address already exists.");
                return View(model);
            }

            try
            {
                // 2. Generate a clean, unique ClientId (e.g. CL-205)
                int existingCount = db.Clients.Count();
                string newClientId = $"CL-{existingCount + 201}";
                while (db.Clients.Any(c => c.ClientId == newClientId))
                {
                    existingCount++;
                    newClientId = $"CL-{existingCount + 201}";
                }

                // 3. Create the new Client record with salted password hash (FR-S-05)
                var client = new Clients
                {
                    ClientId = newClientId,
                    FullName = model.FullName.Trim(),
                    Email = normalizedEmail,
                    Password = PasswordHelper.HashPassword(model.Password),
                    PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                    Gender = model.Gender,
                    DateOfBirth = model.DateOfBirth,
                    HeightCm = model.HeightCm,
                    Role = "Client",
                    IsActive = true,
                    FailedLoginAttempts = 0,
                    CreatedAt = DateTime.UtcNow,
                    PasswordUpdatedAt = DateTime.UtcNow
                };

                // Handle Profile Picture upload if provided (nullable/optional)
                if (model.ProfileImage != null && model.ProfileImage.ContentLength > 0)
                {
                    string ext = System.IO.Path.GetExtension(model.ProfileImage.FileName).ToLower();
                    string[] allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    if (allowedExts.Contains(ext))
                    {
                        string uploadFolder = Server.MapPath("~/Uploads/ProfilePictures/");
                        if (!System.IO.Directory.Exists(uploadFolder))
                        {
                            System.IO.Directory.CreateDirectory(uploadFolder);
                        }

                        string uniqueFileName = $"{newClientId}_{Guid.NewGuid().ToString("N").Substring(0, 8)}{ext}";
                        string filePath = System.IO.Path.Combine(uploadFolder, uniqueFileName);
                        model.ProfileImage.SaveAs(filePath);
                        client.ProfilePicturePath = $"/Uploads/ProfilePictures/{uniqueFileName}";
                    }
                }

                // 4. Create the initial ClientPreferences record
                var preferences = new ClientPreferences
                {
                    ClientId = newClientId,
                    PrimaryGoal = string.IsNullOrWhiteSpace(model.PrimaryGoal) ? "General Fitness" : model.PrimaryGoal,
                    FitnessLevel = string.IsNullOrWhiteSpace(model.FitnessLevel) ? "Beginner" : model.FitnessLevel,
                    PreferredWorkoutType = string.IsNullOrWhiteSpace(model.PreferredWorkoutType) ? "Mixed / Hybrid" : model.PreferredWorkoutType,
                    TargetDaysPerWeek = model.TargetDaysPerWeek ?? 3,
                    PreferredWorkoutDurationMinutes = model.PreferredWorkoutDurationMinutes ?? 45,
                    PreferredWorkoutTime = model.PreferredWorkoutTime ?? "Morning",
                    PreferredWorkoutLocation = model.PreferredWorkoutLocation ?? "Commercial Gym",
                    PreferredDietType = model.PreferredDietType ?? "Standard Balanced",
                    TargetTimeframeMonths = model.TargetTimeframeMonths ?? 3,
                    PreferredTrainerGender = model.PreferredTrainerGender ?? "No Preference",
                    AdditionalPreferences = model.AdditionalPreferences,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // 5. Automatically match and assign the best personal trainer based on client preferences
                int matchScore = 0;
                var assignedTrainer = TrainerMatchingHelper.FindBestMatchingTrainer(
                    db,
                    preferences.PrimaryGoal,
                    preferences.PreferredWorkoutType,
                    preferences.FitnessLevel,
                    preferences.PreferredTrainerGender,
                    out matchScore
                );

                if (assignedTrainer != null)
                {
                    client.AssignedTrainerId = assignedTrainer.TrainerId;
                }

                // 6. Save client and related baseline records in database
                db.Clients.Add(client);
                db.ClientPreferences.Add(preferences);

                // Initial Health Profile (Baseline Height) - Encrypted at rest (FR-S-14)
                var healthProfile = new ClientHealthProfile
                {
                    ClientId = newClientId,
                    BaselineHeightCm = model.HeightCm.GetValueOrDefault(),
                    Allergies = "None Reported",
                    DietaryRestrictions = "None Reported",
                    MedicalConditions = "None Reported",
                    InjuriesHistory = "No Active Injuries",
                    UpdatedAt = DateTime.UtcNow
                };
                HealthDataProtectionHelper.EncryptProfile(healthProfile);
                db.ClientHealthProfile.Add(healthProfile);

                // Initial Progress Record (Starting Weight & Biometrics) - Encrypted notes (FR-S-14)
                if (model.WeightKg.HasValue && model.WeightKg.Value > 0)
                {
                    var initialProgress = new ClientProgressRecords
                    {
                        ClientId = newClientId,
                        RecordedDate = DateTime.Today,
                        WeightKg = model.WeightKg.Value,
                        HeightCm = model.HeightCm.GetValueOrDefault(),
                        CaloriesConsumed = 0,
                        CaloriesBurned = 0,
                        WaterIntakeMl = 0,
                        Notes = "Initial onboarding baseline weight",
                        CreatedAt = DateTime.UtcNow
                    };
                    HealthDataProtectionHelper.EncryptProgressRecord(initialProgress);
                    db.ClientProgressRecords.Add(initialProgress);

                    // Initial Target Weight Goal in ClientGoals
                    decimal targetWeight = (model.TargetWeightKg.HasValue && model.TargetWeightKg.Value > 0)
                        ? model.TargetWeightKg.Value
                        : model.WeightKg.Value;

                    var weightGoal = new ClientGoals
                    {
                        ClientId = newClientId,
                        GoalName = "Target Weight",
                        Category = "Weight",
                        BaselineValue = model.WeightKg.Value,
                        CurrentValue = model.WeightKg.Value,
                        TargetValue = targetWeight,
                        Unit = "kg",
                        StatusMessage = "In Progress",
                        IsAchieved = false,
                        TargetDate = DateTime.Today.AddMonths(model.TargetTimeframeMonths ?? 3),
                        CreatedAt = DateTime.UtcNow
                    };
                    db.ClientGoals.Add(weightGoal);
                }

                // 7. Record Registration Audit Log (FR-S-26)
                string auditDetails = assignedTrainer != null
                    ? $"Client account created for {client.Email}. Automatically paired with Coach {assignedTrainer.FullName} ({matchScore}% match)."
                    : $"Client account created for {client.Email}";

                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = newClientId,
                    UserRole = "Client",
                    EventType = "REGISTRATION_SUCCESS",
                    Details = auditDetails,
                    TimestampUtc = DateTime.UtcNow
                });

                db.SaveChanges();

                // FR-S-20: Audit initial health profile and progress record creation
                HealthAuditHelper.Log(newClientId, "Client", newClientId, "HealthProfile", healthProfile.ProfileId.ToString(), "Create", true, "Created initial client health profile upon registration");
                if (model.WeightKg.HasValue && model.WeightKg.Value > 0)
                {
                    HealthAuditHelper.Log(newClientId, "Client", newClientId, "ProgressRecord", null, "Create", true, "Recorded baseline biometrics upon registration");
                }

                // 8. Log in automatically and redirect to Client Dashboard (FR-C-04)
                SessionHelper.SetClientSession(Session, client.ClientId, client.FullName, client.Email, client.ProfilePicturePath);
                FormsAuthentication.SetAuthCookie(client.Email, false);

                if (assignedTrainer != null)
                {
                    TempData["SuccessMessage"] = $"Welcome to HealthTrack, {client.FullName}! Based on your fitness goals, you have been automatically paired with Coach {assignedTrainer.FullName} ({matchScore}% compatibility match)!";
                }
                else
                {
                    TempData["SuccessMessage"] = $"Welcome to HealthTrack, {client.FullName}! Your profile and preferences have been set up.";
                }

                return RedirectToAction("Index", "Dashboard", new { area = "Client" });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An unexpected error occurred while creating your account. Please try again.");
                System.Diagnostics.Debug.WriteLine("Registration error: " + ex.Message);
                return View(model);
            }
        }

        // =====================================================================
        // 2. LOGIN (GET & POST)
        // =====================================================================

        // GET: /Client/Account/Login
        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            if (SessionHelper.IsClient(Session))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Client" });
            }

            var model = new ClientLoginViewModel
            {
                ReturnUrl = returnUrl
            };

            // Remember Me: pre-fill email if cookie was previously saved
            HttpCookie savedEmailCookie = Request.Cookies["HealthTrack_RememberEmail"];
            if (savedEmailCookie != null && !string.IsNullOrWhiteSpace(savedEmailCookie.Value))
            {
                model.Email = savedEmailCookie.Value;
                model.RememberMe = true;
            }

            return View(model);
        }

        // POST: /Client/Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(ClientLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string normalizedEmail = model.Email.Trim().ToLower();

            // 1. Fetch Client from Database
            var client = db.Clients.FirstOrDefault(c => c.Email.ToLower() == normalizedEmail);

            // Generic error message to prevent user enumeration (FR-S-16)
            const string genericError = "Invalid email address or password.";

            if (client == null)
            {
                // Check if user is a Trainer
                var trainer = db.Trainers.FirstOrDefault(t => t.Email.ToLower() == normalizedEmail);
                if (trainer != null)
                {
                    if (!trainer.IsActive)
                    {
                        ModelState.AddModelError("", "Your trainer account has been deactivated. Please contact an administrator.");
                        return View(model);
                    }

                    if (!trainer.IsInvitationAccepted || string.IsNullOrEmpty(trainer.Password))
                    {
                        ModelState.AddModelError("", "Your trainer account is not yet activated. Please use the invitation link sent to your email to set your password.");
                        return View(model);
                    }

                    if (trainer.LockoutEndUtc.HasValue && trainer.LockoutEndUtc.Value > DateTime.UtcNow)
                    {
                        int minutesRemaining = (int)Math.Ceiling((trainer.LockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
                        if (minutesRemaining < 1) minutesRemaining = 1;
                        ModelState.AddModelError("", $"Account is temporarily locked due to multiple failed login attempts. Please try again in {minutesRemaining} minute(s).");
                        return View(model);
                    }

                    bool isTrainerPassValid = PasswordHelper.VerifyPassword(model.Password, trainer.Password);
                    if (!isTrainerPassValid && !PasswordHelper.IsHashed(trainer.Password) && model.Password == trainer.Password)
                    {
                        isTrainerPassValid = true;
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

                    if (!isTrainerPassValid)
                    {
                        trainer.FailedLoginAttempts += 1;
                        if (trainer.FailedLoginAttempts >= 5)
                        {
                            trainer.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                        }
                        db.SaveChanges();
                        ModelState.AddModelError("", genericError);
                        return View(model);
                    }

                    // Reset failed login attempts
                    trainer.FailedLoginAttempts = 0;
                    trainer.LockoutEndUtc = null;
                    db.SaveChanges();

                    // Audit Log
                    db.AuthAuditLogs.Add(new AuthAuditLogs
                    {
                        UserId = trainer.TrainerId,
                        UserRole = "Trainer",
                        EventType = "LOGIN_SUCCESS",
                        Details = $"Trainer {trainer.FullName} logged in successfully",
                        TimestampUtc = DateTime.UtcNow
                    });
                    db.SaveChanges();

                    // Set Trainer Session
                    SessionHelper.SetTrainerSession(Session, trainer.TrainerId, trainer.FullName, trainer.Email);

                    return RedirectToAction("Index", "Dashboard", new { area = "Trainer" });
                }

                // If neither Client nor Trainer
                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = normalizedEmail,
                    UserRole = "Unknown",
                    EventType = "LOGIN_FAILED",
                    Details = "Login failed: Email not found in system",
                    TimestampUtc = DateTime.UtcNow
                });
                db.SaveChanges();

                ModelState.AddModelError("", genericError);
                return View(model);
            }

            // 2. Check if Account is Active (FR-A-06, FR-A-18)
            if (!client.IsActive)
            {
                ModelState.AddModelError("", "Your account has been deactivated. Please contact an administrator.");
                return View(model);
            }

            // 3. Check Brute-force Temporary Lockout (FR-S-25: 15 minutes lockout)
            if (client.LockoutEndUtc.HasValue && client.LockoutEndUtc.Value > DateTime.UtcNow)
            {
                int minutesRemaining = (int)Math.Ceiling((client.LockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
                if (minutesRemaining < 1) minutesRemaining = 1;

                ModelState.AddModelError("", $"Account is temporarily locked due to multiple failed login attempts. Please try again in {minutesRemaining} minute(s).");
                return View(model);
            }

            // 4. Verify Password against stored salted hash (FR-S-05)
            bool isPasswordValid = PasswordHelper.VerifyPassword(model.Password, client.Password);

            // Safe legacy migration: If account still contains an unhashed password, upgrade to PBKDF2 on valid login
            if (!isPasswordValid && !PasswordHelper.IsHashed(client.Password) && model.Password == client.Password)
            {
                isPasswordValid = true;
                client.Password = PasswordHelper.HashPassword(model.Password);
                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = client.ClientId,
                    UserRole = "Client",
                    EventType = "PASSWORD_MIGRATED",
                    Details = $"Legacy plaintext password for client {client.FullName} was automatically upgraded to PBKDF2 salted hash upon login",
                    TimestampUtc = DateTime.UtcNow
                });
            }

            if (!isPasswordValid)
            {
                client.FailedLoginAttempts += 1;

                if (client.FailedLoginAttempts >= 5)
                {
                    client.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);

                    db.AuthAuditLogs.Add(new AuthAuditLogs
                    {
                        UserId = client.ClientId,
                        UserRole = "Client",
                        EventType = "ACCOUNT_LOCKED",
                        Details = "Account locked for 15 minutes after 5 failed login attempts",
                        TimestampUtc = DateTime.UtcNow
                    });

                    ModelState.AddModelError("", "Account temporarily locked for 15 minutes due to 5 consecutive failed login attempts.");
                }
                else
                {
                    int attemptsRemaining = 5 - client.FailedLoginAttempts;

                    db.AuthAuditLogs.Add(new AuthAuditLogs
                    {
                        UserId = client.ClientId,
                        UserRole = "Client",
                        EventType = "LOGIN_FAILED",
                        Details = $"Incorrect password attempt ({client.FailedLoginAttempts}/5)",
                        TimestampUtc = DateTime.UtcNow
                    });

                    ModelState.AddModelError("", $"{genericError} ({attemptsRemaining} attempt(s) remaining before temporary lockout)");
                }

                db.SaveChanges();
                return View(model);
            }

            // 5. Successful Login! Reset lockout counters & update LastLoginAt
            client.FailedLoginAttempts = 0;
            client.LockoutEndUtc = null;
            client.LastLoginAt = DateTime.UtcNow;

            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = client.ClientId,
                UserRole = "Client",
                EventType = "LOGIN_SUCCESS",
                Details = "Successful Client login",
                TimestampUtc = DateTime.UtcNow
            });

            db.SaveChanges();

            // 6. Set Session & Auth Cookie (FR-S-06)
            SessionHelper.SetClientSession(Session, client.ClientId, client.FullName, client.Email, client.ProfilePicturePath);
            FormsAuthentication.SetAuthCookie(client.Email, model.RememberMe);

            // Remember Me: save or remove email cookie based on user preference
            if (model.RememberMe)
            {
                HttpCookie rememberCookie = new HttpCookie("HealthTrack_RememberEmail", client.Email)
                {
                    Expires = DateTime.Now.AddDays(30)
                };
                Response.Cookies.Add(rememberCookie);
            }
            else
            {
                if (Request.Cookies["HealthTrack_RememberEmail"] != null)
                {
                    HttpCookie rememberCookie = new HttpCookie("HealthTrack_RememberEmail", "")
                    {
                        Expires = DateTime.Now.AddDays(-1)
                    };
                    Response.Cookies.Add(rememberCookie);
                }
            }

            // 7. Redirect to requested return URL or default Client Dashboard (FR-C-04)
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Dashboard", new { area = "Client" });
        }

        // =====================================================================
        // 3. LOGOUT (GET & POST) (FR-C-02, FR-S-07)
        // =====================================================================

        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public ActionResult Logout()
        {
            string clientId = SessionHelper.GetCurrentClientId(Session);

            if (!string.IsNullOrEmpty(clientId))
            {
                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = clientId,
                    UserRole = "Client",
                    EventType = "LOGOUT",
                    Details = "Client logged out",
                    TimestampUtc = DateTime.UtcNow
                });
                db.SaveChanges();
            }

            // Clear and abandon session (FR-S-07)
            Session.Clear();
            Session.Abandon();
            FormsAuthentication.SignOut();

            TempData["InfoMessage"] = "You have been successfully logged out.";
            return RedirectToAction("Login", "Account", new { area = "Client" });
        }

        // =====================================================================
        // 4. FORGOT PASSWORD (POST) (FR-S-11)
        // Sends password reset link instructions to the client's email via SMTP
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
            var client = db.Clients.FirstOrDefault(c => c.Email.ToLower() == normalizedEmail);

            if (client == null)
            {
                return Json(new { success = false, message = "No account found with this email address." });
            }

            if (!client.IsActive)
            {
                return Json(new { success = false, message = "This account is inactive. Please contact an administrator." });
            }

            try
            {
                // 1. Generate unique reset token & log audit event
                string resetToken = Guid.NewGuid().ToString("N");

                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = client.ClientId,
                    UserRole = "Client",
                    EventType = "PASSWORD_RESET_REQUEST",
                    Details = $"Password reset link requested for {client.Email}",
                    TimestampUtc = DateTime.UtcNow
                });
                db.SaveChanges();

                // 2. Build full password reset link
                string resetLink = Url.Action("ResetPassword", "Account", new { 
                    area = "Client", 
                    email = client.Email, 
                    token = resetToken 
                }, Request.Url.Scheme);

                // 3. Send email using EmailHelper
                string smtpError;
                bool emailSent = EmailHelper.SendPasswordResetEmail(client.Email, client.FullName, resetLink, out smtpError);

                if (emailSent)
                {
                    return Json(new { 
                        success = true, 
                        message = $"Password reset link has been successfully sent to {client.Email}! Please check your inbox and spam folder." 
                    });
                }
                else
                {
                    return Json(new { 
                        success = true, 
                        isDemo = true,
                        resetLink = resetLink,
                        message = $"Password reset link generated! (SMTP Note: {smtpError}) You can click the link below to open the Reset Password page."
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while sending the reset link: " + ex.Message });
            }
        }

        // =====================================================================
        // 5. RESET PASSWORD (GET & POST)
        // Allows user to choose a new password using their emailed link
        // =====================================================================

        // GET: /Client/Account/ResetPassword
        [HttpGet]
        public ActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                TempData["ErrorMessage"] = "Invalid or expired password reset link.";
                return RedirectToAction("Login");
            }

            string normalizedEmail = email.Trim().ToLower();
            var client = db.Clients.FirstOrDefault(c => c.Email.ToLower() == normalizedEmail);

            if (client == null)
            {
                TempData["ErrorMessage"] = "Account not found.";
                return RedirectToAction("Login");
            }

            var model = new ResetPasswordViewModel
            {
                Email = client.Email,
                Token = token
            };

            return View(model);
        }

        // POST: /Client/Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string normalizedEmail = model.Email.Trim().ToLower();
            var client = db.Clients.FirstOrDefault(c => c.Email.ToLower() == normalizedEmail);

            if (client == null)
            {
                ModelState.AddModelError("", "No account found with this email address.");
                return View(model);
            }

            // Update password with salted hash
            client.Password = PasswordHelper.HashPassword(model.Password);
            client.FailedLoginAttempts = 0;
            client.LockoutEndUtc = null;
            client.PasswordUpdatedAt = DateTime.UtcNow;

            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = client.ClientId,
                UserRole = "Client",
                EventType = "PASSWORD_RESET_COMPLETED",
                Details = "Password successfully reset via emailed link",
                TimestampUtc = DateTime.UtcNow
            });

            db.SaveChanges();

            TempData["SuccessMessage"] = "Your password has been successfully reset! Please log in with your new password.";
            return RedirectToAction("Login");
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
