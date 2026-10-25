using System;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Areas.Admin.ViewModels;
using HealthTrack.Helpers;
using HealthTrack.Models;

namespace HealthTrack.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class ProfileController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        // Helper method: get the currently active administrator from the database
        private Admins GetCurrentAdmin()
        {
            string adminId = Session["AdminId"] as string;
            string adminEmail = Session["AdminEmail"] as string;

            Admins admin = null;

            if (!string.IsNullOrEmpty(adminId))
            {
                admin = db.Admins.FirstOrDefault(a => a.AdminId == adminId);
            }

            if (admin == null && !string.IsNullOrEmpty(adminEmail))
            {
                admin = db.Admins.FirstOrDefault(a => a.Email.ToLower() == adminEmail.ToLower());
            }

            if (admin == null)
            {
                // Fallback to the first administrator in the table
                admin = db.Admins.OrderBy(a => a.AdminId).FirstOrDefault();
            }

            // If table was empty, insert default admin record so system always works
            if (admin == null)
            {
                admin = new Admins
                {
                    AdminId = "ADMIN-01",
                    FullName = "System Administrator",
                    Email = "admin@healthtrack.com",
                    Password = PasswordHelper.HashPassword("Admin@123"),
                    Role = "Admin",
                    PhoneNumber = "+1 (555) 019-2834",
                    IsActive = true,
                    FailedLoginAttempts = 0,
                    CreatedAt = DateTime.UtcNow
                };
                db.Admins.Add(admin);
                db.SaveChanges();
            }

            // Sync session values
            Session["AdminId"] = admin.AdminId;
            Session["AdminName"] = admin.FullName;
            Session["AdminEmail"] = admin.Email;

            return admin;
        }

        // =====================================================================
        // GET: /Admin/Profile
        // Loads administrator details directly from the Admins database table
        // =====================================================================
        [HttpGet]
        public ActionResult Index()
        {
            var admin = GetCurrentAdmin();

            var model = new AdminProfileViewModel
            {
                AdminId = admin.AdminId,
                FullName = admin.FullName,
                Email = admin.Email,
                PhoneNumber = admin.PhoneNumber,
                Role = admin.Role ?? "Admin",
                IsActive = admin.IsActive,
                CreatedAt = admin.CreatedAt,
                LastLoginAt = admin.LastLoginAt,
                TotalClientsCount = db.Clients.Count(),
                TotalTrainersCount = db.Trainers.Count(),
                TotalTicketsCount = db.SupportTickets.Count(),
                PasswordForm = new AdminChangePasswordViewModel()
            };

            // Load all registered Administrator accounts for the roster (FR-A-09)
            var allAdmins = db.Admins.OrderBy(a => a.AdminId).ToList();
            model.Administrators = allAdmins.Select(a => new AdminListItemViewModel
            {
                AdminId = a.AdminId,
                FullName = a.FullName,
                Email = a.Email,
                PhoneNumber = a.PhoneNumber,
                Role = a.Role ?? "Admin",
                IsActive = a.IsActive,
                CreatedAt = a.CreatedAt,
                LastLoginAt = a.LastLoginAt,
                IsCurrentAdmin = a.AdminId == admin.AdminId
            }).ToList();

            return View(model);
        }

        // =====================================================================
        // POST: /Admin/Profile/UpdateProfile
        // Saves updated FullName, Email, and Phone directly into the Admins table
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateProfile(AdminProfileViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.FullName))
            {
                TempData["ErrorMessage"] = "Full Name is required.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(model.Email))
            {
                TempData["ErrorMessage"] = "Email address is required.";
                return RedirectToAction("Index");
            }

            string normalizedEmail = model.Email.Trim().ToLower();

            var admin = GetCurrentAdmin();

            // Check if email is already taken by another admin
            bool emailExists = db.Admins.Any(a => a.Email.ToLower() == normalizedEmail && a.AdminId != admin.AdminId);
            if (emailExists)
            {
                TempData["ErrorMessage"] = "This email address is already in use by another administrator.";
                return RedirectToAction("Index");
            }

            // Update database fields
            admin.FullName = model.FullName.Trim();
            admin.Email = normalizedEmail;
            admin.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
            admin.UpdatedAt = DateTime.UtcNow;

            // Audit Log
            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = admin.AdminId,
                UserRole = "Admin",
                EventType = "ADMIN_PROFILE_UPDATED",
                Details = $"Administrator profile updated: {admin.FullName} ({admin.Email})",
                TimestampUtc = DateTime.UtcNow
            });

            db.SaveChanges();

            // Update session values
            Session["AdminName"] = admin.FullName;
            Session["AdminEmail"] = admin.Email;

            TempData["SuccessMessage"] = "Profile details updated successfully.";
            return RedirectToAction("Index");
        }

        // =====================================================================
        // POST: /Admin/Profile/ChangePassword
        // Verifies current password and saves newly hashed password into Admins table
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(AdminChangePasswordViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.CurrentPassword))
            {
                TempData["ErrorMessage"] = "Please enter your current password.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 6)
            {
                TempData["ErrorMessage"] = "New password must be at least 6 characters long.";
                return RedirectToAction("Index");
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                TempData["ErrorMessage"] = "New password and confirmation password do not match.";
                return RedirectToAction("Index");
            }

            var admin = GetCurrentAdmin();

            // Verify current password against database hash (FR-S-05)
            bool isCurrentPasswordValid = PasswordHelper.VerifyPassword(model.CurrentPassword, admin.Password)
                                       || (!PasswordHelper.IsHashed(admin.Password) && model.CurrentPassword == admin.Password);

            if (!isCurrentPasswordValid)
            {
                TempData["ErrorMessage"] = "The current password you entered is incorrect.";
                return RedirectToAction("Index");
            }

            // Hash the new password with salt and update
            admin.Password = PasswordHelper.HashPassword(model.NewPassword);
            admin.UpdatedAt = DateTime.UtcNow;

            // Audit log
            db.AuthAuditLogs.Add(new AuthAuditLogs
            {
                UserId = admin.AdminId,
                UserRole = "Admin",
                EventType = "ADMIN_PASSWORD_CHANGED",
                Details = $"Administrator {admin.FullName} changed account password",
                TimestampUtc = DateTime.UtcNow
            });

            db.SaveChanges();

            TempData["SuccessMessage"] = "Account password updated successfully.";
            return RedirectToAction("Index");
        }

        // =====================================================================
        // 4. CREATE ADMINISTRATOR (GET: /Admin/Profile/CreateAdmin)
        // Displays form for authorized administrator to create a new admin account (FR-A-09)
        // =====================================================================
        [HttpGet]
        public ActionResult CreateAdmin()
        {
            var model = new CreateAdminViewModel();
            return View(model);
        }

        // =====================================================================
        // 5. CREATE ADMINISTRATOR (POST: /Admin/Profile/CreateAdmin)
        // Validates, hashes password, and persists additional administrator account (FR-A-09)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateAdmin(CreateAdminViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 1. Ensure Full Name is not empty or whitespace-only
            if (string.IsNullOrWhiteSpace(model.FullName))
            {
                ModelState.AddModelError("FullName", "Full Name cannot be empty or whitespace only.");
                return View(model);
            }

            string normalizedEmail = (model.Email ?? "").Trim().ToLowerInvariant();

            // 2. Prevent duplicate Administrator email addresses
            if (db.Admins.Any(a => a.Email.ToLower() == normalizedEmail))
            {
                ModelState.AddModelError("Email", "An administrator account with this email address already exists.");
                return View(model);
            }

            // 3. Preserve global email uniqueness across account types
            if (db.Clients.Any(c => c.Email.ToLower() == normalizedEmail))
            {
                ModelState.AddModelError("Email", "This email address is already registered to a Client account.");
                return View(model);
            }

            if (db.Trainers.Any(t => t.Email.ToLower() == normalizedEmail))
            {
                ModelState.AddModelError("Email", "This email address is already registered to a Trainer account.");
                return View(model);
            }

            // 4. Reject invalid or unsupported Administrator role values
            if (!"Admin".Equals(model.Role, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Role", "Invalid or unsupported Administrator role. Allowed role is 'Admin'.");
                return View(model);
            }

            // 5. Validate password policy & confirmation match
            if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
            {
                ModelState.AddModelError("Password", "Password must be at least 6 characters long.");
                return View(model);
            }

            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "The password and confirmation password do not match.");
                return View(model);
            }

            try
            {
                // 6. Generate next unique AdminId (e.g. ADMIN-02, ADMIN-03)
                string newAdminId = GenerateNextAdminId();
                DateTime nowUtc = DateTime.UtcNow;

                // 7. Hash initial password securely using PBKDF2 (never plaintext)
                string hashedPassword = PasswordHelper.HashPassword(model.Password);

                // 8. Construct new Admin entity using only permitted fields
                var newAdmin = new Admins
                {
                    AdminId = newAdminId,
                    FullName = model.FullName.Trim(),
                    Email = normalizedEmail,
                    Password = hashedPassword,
                    Role = "Admin",
                    PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                    IsActive = true,
                    FailedLoginAttempts = 0,
                    LockoutEndUtc = null,
                    LastLoginAt = null,
                    CreatedAt = nowUtc,
                    UpdatedAt = null
                };

                db.Admins.Add(newAdmin);

                // 9. Record security audit event (never logging plaintext password or hashes)
                string currentAdminId = Session["AdminId"] as string ?? "Admin";
                db.AuthAuditLogs.Add(new AuthAuditLogs
                {
                    UserId = currentAdminId,
                    UserRole = "Admin",
                    EventType = "ADMIN_CREATED",
                    Details = $"Administrator '{currentAdminId}' created new administrator account '{newAdmin.AdminId}' ({newAdmin.Email}) with role 'Admin'",
                    TimestampUtc = nowUtc
                });

                db.SaveChanges();

                // 10. Set success feedback and redirect to administrator list
                TempData["SuccessMessage"] = $"Administrator '{newAdmin.FullName}' ({newAdmin.AdminId}) was created successfully.";
                return RedirectToAction("Index");
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Admin Creation DB Conflict] {ex.Message}");
                ModelState.AddModelError("", "A database conflict occurred while creating the administrator account. Please verify the email address is unique and try again.");
                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Admin Creation Error] {ex.Message}");
                ModelState.AddModelError("", "An unexpected error occurred while creating the administrator account. Please try again.");
                return View(model);
            }
        }

        /// <summary>
        /// Generates the next sequential Administrator ID matching existing conventions (e.g. ADMIN-02, ADMIN-03).
        /// </summary>
        private string GenerateNextAdminId()
        {
            int count = db.Admins.Count() + 1;
            string newAdminId = $"ADMIN-{count:D2}";
            while (db.Admins.Any(a => a.AdminId == newAdminId))
            {
                count++;
                newAdminId = $"ADMIN-{count:D2}";
            }
            return newAdminId;
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
