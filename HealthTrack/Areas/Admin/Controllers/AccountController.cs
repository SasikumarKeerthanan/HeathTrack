using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using HealthTrack.Areas.Admin.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Admin.Controllers
{
    /// Administrator Account Controller: handles Admin login, lockout protection, and logout.
    public class AccountController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // =====================================================================
        // 1. LOGIN (GET: /Admin/Account/Login or /Admin/Login)
        // =====================================================================
        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            // If already logged in as Admin, redirect directly to Admin Dashboard
            if (SessionHelper.IsAdmin(Session))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }

            var model = new AdminLoginViewModel
            {
                ReturnUrl = returnUrl
            };

            // Check Remember Me cookie
            var cookie = Request.Cookies["HealthTrack_Admin_RememberEmail"];
            if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
            {
                model.Email = cookie.Value;
                model.RememberMe = true;
            }

            return View(model);
        }

        // =====================================================================
        // 2. LOGIN (POST: /Admin/Account/Login)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string normalizedEmail = model.Email.Trim().ToLower();
            const string genericError = "Invalid email address or password.";

            // 1. Fetch Administrator from the Admins table
            var admin = db.Admins.FirstOrDefault(a => a.Email.ToLower() == normalizedEmail);

            if (admin == null)
            {
                // Record failed login attempt in audit logs
                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = normalizedEmail,
                    UserRole = "Admin",
                    EventType = "LOGIN_FAILED",
                    Details = "Administrator login failed: Email not found in Admins table",
                    TimestampUtc = DateTime.UtcNow
                });
                db.SaveChanges();

                ModelState.AddModelError("", genericError);
                return View(model);
            }

            // 2. Check if Account is Active
            if (!admin.IsActive)
            {
                ModelState.AddModelError("", "Your administrator account has been deactivated. Please contact root support.");
                return View(model);
            }

            // 3. Check Brute-force Temporary Lockout (15 minutes window)
            if (admin.LockoutEndUtc.HasValue && admin.LockoutEndUtc.Value > DateTime.UtcNow)
            {
                int minutesRemaining = (int)Math.Ceiling((admin.LockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
                if (minutesRemaining < 1) minutesRemaining = 1;

                ModelState.AddModelError("", $"Account is temporarily locked due to multiple failed login attempts. Please try again in {minutesRemaining} minute(s).");
                return View(model);
            }

            // 4. Verify Password against stored PBKDF2 hash (FR-S-05)
            bool isPasswordValid = PasswordHelper.VerifyPassword(model.Password, admin.Password);

            // Safe legacy migration: If account still contains an unhashed password, upgrade to PBKDF2 on valid login
            if (!isPasswordValid && !PasswordHelper.IsHashed(admin.Password) && model.Password == admin.Password)
            {
                isPasswordValid = true;
                admin.Password = PasswordHelper.HashPassword(model.Password);
                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = admin.AdminId,
                    UserRole = "Admin",
                    EventType = "PASSWORD_MIGRATED",
                    Details = $"Legacy plaintext password for administrator {admin.FullName} was automatically upgraded to PBKDF2 salted hash upon login",
                    TimestampUtc = DateTime.UtcNow
                });
            }

            if (!isPasswordValid)
            {
                admin.FailedLoginAttempts += 1;

                if (admin.FailedLoginAttempts >= 5)
                {
                    admin.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                    db.AuthAuditLogs.Add(new AuthAuditLogs
                    {
                        UserId = admin.AdminId,
                        UserRole = "Admin",
                        EventType = "ACCOUNT_LOCKED",
                        Details = "Administrator account locked for 15 minutes after 5 consecutive failed login attempts",
                        TimestampUtc = DateTime.UtcNow
                    });
                    ModelState.AddModelError("", "Account temporarily locked for 15 minutes due to 5 consecutive failed login attempts.");
                }
                else
                {
                    int attemptsRemaining = 5 - admin.FailedLoginAttempts;
                    db.AuthAuditLogs.Add(new AuthAuditLogs
                    {
                        UserId = admin.AdminId,
                        UserRole = "Admin",
                        EventType = "LOGIN_FAILED",
                        Details = $"Incorrect admin password attempt ({admin.FailedLoginAttempts}/5)",
                        TimestampUtc = DateTime.UtcNow
                    });
                    ModelState.AddModelError("", $"{genericError} ({attemptsRemaining} attempt(s) remaining before temporary lockout)");
                }

                db.SaveChanges();
                return View(model);
            }

            // 5. Successful Login: Reset failed attempts & update timestamps
            admin.FailedLoginAttempts = 0;
            admin.LockoutEndUtc = null;
            admin.LastLoginAt = DateTime.UtcNow;

            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = admin.AdminId,
                UserRole = "Admin",
                EventType = "LOGIN_SUCCESS",
                Details = $"Administrator {admin.FullName} logged in successfully",
                TimestampUtc = DateTime.UtcNow
            });
            db.SaveChanges();

            // 6. Handle Remember Me Cookie
            if (model.RememberMe)
            {
                var rememberCookie = new HttpCookie("HealthTrack_Admin_RememberEmail", admin.Email)
                {
                    Expires = DateTime.Now.AddDays(30)
                };
                Response.Cookies.Add(rememberCookie);
            }
            else
            {
                if (Request.Cookies["HealthTrack_Admin_RememberEmail"] != null)
                {
                    var expireCookie = new HttpCookie("HealthTrack_Admin_RememberEmail", "")
                    {
                        Expires = DateTime.Now.AddDays(-1)
                    };
                    Response.Cookies.Add(expireCookie);
                }
            }

            // 7. Store Administrator Session
            SessionHelper.SetAdminSession(Session, admin.AdminId, admin.FullName, admin.Email);
            FormsAuthentication.SetAuthCookie(admin.Email, model.RememberMe);

            // 8. Redirect
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        // =====================================================================
        // 3. LOGOUT (GET/POST: /Admin/Account/Logout)
        // =====================================================================
        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public ActionResult Logout()
        {
            string adminId = Session["AdminId"] as string ?? "Admin";

            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = adminId,
                UserRole = "Admin",
                EventType = "LOGOUT",
                Details = "Administrator logged out",
                TimestampUtc = DateTime.UtcNow
            });
            db.SaveChanges();

            SessionHelper.ClearSession(Session);
            FormsAuthentication.SignOut();

            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Login", "Account", new { area = "Admin" });
        }

        // =====================================================================
        // 4. FORGOT PASSWORD (POST: /Admin/Account/ForgotPassword)
        // Self-service token generation for Administrator Password Reset (FR-A-04, FR-S-11)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ForgotPassword(string email)
        {
            const string enumerationSafeMessage = "If an Administrator account exists for this email address, a password reset link will be sent.";

            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = "Please enter your administrator email address." });
            }

            string normalizedEmail = email.Trim().ToLowerInvariant();

            try
            {
                var admin = db.Admins.FirstOrDefault(a => a.Email.ToLower() == normalizedEmail);

                if (admin == null)
                {
                    // Audit log for non-existent admin reset attempt (enumeration-safe response provided)
                    db.AuthAuditLogs.Add(new AuthAuditLogs
                    {
                        UserId = normalizedEmail,
                        UserRole = "Admin",
                        EventType = "PASSWORD_RESET_REQUEST",
                        Details = "Administrator password reset requested for non-existent email address (enumeration-safe response provided)",
                        TimestampUtc = DateTime.UtcNow
                    });
                    db.SaveChanges();

                    return Json(new { success = true, message = enumerationSafeMessage });
                }

                if (!admin.IsActive)
                {
                    // Audit log for inactive admin reset attempt (enumeration-safe response provided)
                    db.AuthAuditLogs.Add(new AuthAuditLogs
                    {
                        UserId = admin.AdminId,
                        UserRole = "Admin",
                        EventType = "PASSWORD_RESET_REQUEST",
                        Details = "Administrator password reset requested for deactivated account (enumeration-safe response provided)",
                        TimestampUtc = DateTime.UtcNow
                    });
                    db.SaveChanges();

                    return Json(new { success = true, message = enumerationSafeMessage });
                }

                DateTime nowUtc = DateTime.UtcNow;

                // 1. Revoke any previous unused tokens for this Administrator
                var activeTokens = db.AdminPasswordResetTokens
                    .Where(t => t.AdminId == admin.AdminId && !t.IsUsed && t.RevokedAt == null)
                    .ToList();

                foreach (var oldToken in activeTokens)
                {
                    oldToken.RevokedAt = nowUtc;
                }

                // 2. Generate a cryptographically secure random token and compute SHA-256 hash
                string rawToken = GenerateCryptographicToken();
                string tokenHash = ComputeSha256Hash(rawToken);

                // 3. Persist ONLY the SHA-256 hash with strict 30-minute expiration period
                var tokenRecord = new AdminPasswordResetTokens
                {
                    AdminId = admin.AdminId,
                    TokenHash = tokenHash,
                    CreatedAt = nowUtc,
                    ExpiresAtUtc = nowUtc.AddMinutes(30),
                    IsUsed = false,
                    UsedAt = null,
                    RevokedAt = null
                };
                db.AdminPasswordResetTokens.Add(tokenRecord);

                // 4. Record audit event
                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = admin.AdminId,
                    UserRole = "Admin",
                    EventType = "PASSWORD_RESET_REQUEST",
                    Details = $"Secure password reset token generated for administrator {admin.Email} (strict 30-minute expiration period)",
                    TimestampUtc = nowUtc
                });

                db.SaveChanges();

                // 5. Build absolute reset link with the unhashed token
                string resetLink = Url.Action("ResetPassword", "Account", new
                {
                    area = "Admin",
                    email = admin.Email,
                    token = rawToken
                }, Request.Url.Scheme);

                // Log to Debug output for developer/testing environments without exposing in response
                System.Diagnostics.Debug.WriteLine($"[Admin Password Reset Link] {resetLink}");

                // 6. Send password reset email via EmailHelper
                string smtpError;
                bool emailSent = EmailHelper.SendPasswordResetEmail(admin.Email, admin.FullName, resetLink, out smtpError);

                if (!emailSent)
                {
                    db.AuthAuditLogs.Add(new AuthAuditLogs
                    {
                        UserId = admin.AdminId,
                        UserRole = "Admin",
                        EventType = "PASSWORD_RESET_EMAIL_FAILED",
                        Details = "SMTP dispatch failed for administrator reset email",
                        TimestampUtc = DateTime.UtcNow
                    });
                    db.SaveChanges();
                    System.Diagnostics.Debug.WriteLine($"[Admin Reset Email SMTP Warning] {smtpError}");
                }

                // Never expose existence, raw tokens, or SMTP credentials in response
                return Json(new { success = true, message = enumerationSafeMessage });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Admin ForgotPassword Error] {ex.Message}");
                return Json(new { success = true, message = enumerationSafeMessage });
            }
        }

        // =====================================================================
        // 5. RESET PASSWORD (GET: /Admin/Account/ResetPassword)
        // Validates token hash, single-use state, and 30-minute expiration
        // =====================================================================
        [HttpGet]
        public ActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                TempData["ErrorMessage"] = "Invalid password reset link. Please request a new one.";
                return RedirectToAction("Login", "Account", new { area = "Admin" });
            }

            string normalizedEmail = email.Trim().ToLowerInvariant();
            var admin = db.Admins.FirstOrDefault(a => a.Email.ToLower() == normalizedEmail);

            if (admin == null || !admin.IsActive)
            {
                TempData["ErrorMessage"] = "Invalid or expired password reset link.";
                return RedirectToAction("Login", "Account", new { area = "Admin" });
            }

            string hashedToken = ComputeSha256Hash(token.Trim());
            var tokenRecord = db.AdminPasswordResetTokens
                .FirstOrDefault(t => t.TokenHash == hashedToken && t.AdminId == admin.AdminId);

            if (tokenRecord == null)
            {
                TempData["ErrorMessage"] = "Invalid password reset link. Please request a new one.";
                return RedirectToAction("Login", "Account", new { area = "Admin" });
            }

            if (tokenRecord.IsUsed)
            {
                TempData["ErrorMessage"] = "This password reset token has already been used. Please request a new link.";
                return RedirectToAction("Login", "Account", new { area = "Admin" });
            }

            if (tokenRecord.RevokedAt.HasValue)
            {
                TempData["ErrorMessage"] = "This password reset token has been revoked or superseded by a newer request.";
                return RedirectToAction("Login", "Account", new { area = "Admin" });
            }

            if (tokenRecord.ExpiresAtUtc <= DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "This password reset link has expired. Reset links are valid for 30 minutes.";
                return RedirectToAction("Login", "Account", new { area = "Admin" });
            }

            var model = new AdminResetPasswordViewModel
            {
                Email = admin.Email,
                Token = token.Trim()
            };

            return View(model);
        }

        // =====================================================================
        // 6. RESET PASSWORD (POST: /Admin/Account/ResetPassword)
        // Consumes token and updates Administrator password atomically (FR-A-04)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(AdminResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string normalizedEmail = (model.Email ?? "").Trim().ToLowerInvariant();
            var admin = db.Admins.FirstOrDefault(a => a.Email.ToLower() == normalizedEmail);

            if (admin == null || !admin.IsActive)
            {
                ModelState.AddModelError("", "Invalid administrator account or account is deactivated.");
                return View(model);
            }

            string hashedToken = ComputeSha256Hash((model.Token ?? "").Trim());
            var tokenRecord = db.AdminPasswordResetTokens
                .FirstOrDefault(t => t.TokenHash == hashedToken && t.AdminId == admin.AdminId);

            if (tokenRecord == null)
            {
                ModelState.AddModelError("", "Invalid password reset token.");
                return View(model);
            }

            if (tokenRecord.IsUsed)
            {
                ModelState.AddModelError("", "This password reset token has already been used. Please request a new link.");
                return View(model);
            }

            if (tokenRecord.RevokedAt.HasValue)
            {
                ModelState.AddModelError("", "This password reset token has been revoked. Please request a new link.");
                return View(model);
            }

            DateTime nowUtc = DateTime.UtcNow;

            if (tokenRecord.ExpiresAtUtc <= nowUtc)
            {
                ModelState.AddModelError("", "This password reset token has expired. Reset links are valid for 30 minutes.");
                return View(model);
            }

            // Atomic update: transaction ensures password update and token consumption succeed together
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // 1. Hash new password securely using PBKDF2 via PasswordHelper
                    admin.Password = PasswordHelper.HashPassword(model.Password);
                    admin.FailedLoginAttempts = 0;
                    admin.LockoutEndUtc = null;
                    admin.UpdatedAt = nowUtc;

                    // 2. Consume current token (single-use enforcement)
                    tokenRecord.IsUsed = true;
                    tokenRecord.UsedAt = nowUtc;

                    // 3. Invalidate/revoke any other remaining active tokens for this admin
                    var otherActiveTokens = db.AdminPasswordResetTokens
                        .Where(t => t.AdminId == admin.AdminId && t.ResetTokenId != tokenRecord.ResetTokenId && !t.IsUsed && t.RevokedAt == null)
                        .ToList();

                    foreach (var ot in otherActiveTokens)
                    {
                        ot.RevokedAt = nowUtc;
                    }

                    // 4. Record audit event
                    db.AuthAuditLogs.Add(new AuthAuditLogs
                    {
                        UserId = admin.AdminId,
                        UserRole = "Admin",
                        EventType = "PASSWORD_RESET_COMPLETED",
                        Details = $"Administrator password reset completed successfully for {admin.Email}. Reset token consumed.",
                        TimestampUtc = nowUtc
                    });

                    db.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    System.Diagnostics.Debug.WriteLine($"[Admin ResetPassword Atomic Update Error] {ex.Message}");
                    ModelState.AddModelError("", "An unexpected error occurred while resetting your password. Please try again.");
                    return View(model);
                }
            }

            TempData["SuccessMessage"] = "Your administrator password has been reset successfully. Please sign in with your new password.";
            return RedirectToAction("Login", "Account", new { area = "Admin" });
        }

        /// <summary>
        /// Generates a cryptographically secure random 256-bit token (32 bytes converted to a 64-char lowercase hex string).
        /// </summary>
        private static string GenerateCryptographicToken()
        {
            byte[] tokenBytes = new byte[32];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(tokenBytes);
            }
            return BitConverter.ToString(tokenBytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Computes the SHA-256 hash of the incoming raw token string for secure database storage and lookup.
        /// </summary>
        private static string ComputeSha256Hash(string rawToken)
        {
            if (string.IsNullOrEmpty(rawToken))
            {
                return string.Empty;
            }

            using (var sha256 = SHA256.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(rawToken);
                byte[] hashBytes = sha256.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
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
