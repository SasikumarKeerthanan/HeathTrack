using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;

namespace HealthTrack.Helpers
{
    /// Helper for sending password reset emails via SMTP (Gmail, Outlook, etc.).
    public static class EmailHelper
    {
        /// Sends a password reset email to the specified client with their personalized reset link.
        public static bool SendPasswordResetEmail(string recipientEmail, string recipientName, string resetLink, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                string host = ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
                int port = int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out int p) ? p : 587;
                string senderEmail = ConfigurationManager.AppSettings["SmtpSenderEmail"] ?? "";
                string senderPassword = ConfigurationManager.AppSettings["SmtpSenderPassword"] ?? "";
                bool enableSsl = bool.TryParse(ConfigurationManager.AppSettings["SmtpEnableSsl"], out bool ssl) ? ssl : true;

                if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword) 
                    || senderEmail.Contains("YOUR_") || senderPassword.Contains("YOUR_"))
                {
                    errorMessage = "Gmail sender email & 16-character Google App Password are not yet configured in Web.config.";
                    return false;
                }

                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(senderEmail, "HealthTrack");
                    message.To.Add(new MailAddress(recipientEmail, recipientName));
                    message.Subject = "Reset Your HealthTrack Password";
                    message.IsBodyHtml = true;
                    message.Body = $@"
                        <div style='background-color: #0f0d1b; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; padding: 40px 20px; color: #ffffff;'>
                            <div style='max-width: 520px; margin: 0 auto; background: #1c1833; border: 1px solid rgba(109, 84, 209, 0.3); border-radius: 16px; padding: 32px 28px; box-shadow: 0 10px 30px rgba(0, 0, 0, 0.4);'>
                                <div style='text-align: center; margin-bottom: 24px;'>
                                    <h2 style='color: #a894ff; margin: 0; font-size: 24px; font-weight: 700; letter-spacing: -0.5px;'>HealthTrack</h2>
                                    <p style='color: #7d7799; margin: 4px 0 0; font-size: 13px;'>Fitness, Workout &amp; Nutrition System</p>
                                </div>
                                
                                <p style='color: #ffffff; font-size: 16px; margin: 0 0 12px;'>Hello <strong>{recipientName}</strong>,</p>
                                <p style='color: #b3aec9; font-size: 14px; line-height: 1.6; margin: 0 0 24px;'>
                                    We received a request to reset your password for your HealthTrack account. Click the button below to choose your new password:
                                </p>
                                
                                <div style='text-align: center; margin: 28px 0;'>
                                    <a href='{resetLink}' style='background: linear-gradient(135deg, #7c5ce9, #5e3cd9); color: #ffffff; padding: 13px 32px; text-decoration: none; border-radius: 10px; font-weight: 600; display: inline-block; font-size: 15px; box-shadow: 0 4px 15px rgba(109, 84, 209, 0.4);'>
                                        Reset Password
                                    </a>
                                </div>
                                
                                <p style='color: #7d7799; font-size: 12px; line-height: 1.5; margin: 24px 0 0;'>
                                    If the button doesn't work, copy and paste this link into your web browser:<br />
                                    <a href='{resetLink}' style='color: #a894ff; word-break: break-all;'>{resetLink}</a>
                                </p>
                                
                                <hr style='border: none; border-top: 1px solid rgba(255, 255, 255, 0.08); margin: 24px 0;' />
                                
                                <p style='color: #615c7a; font-size: 11px; margin: 0; text-align: center;'>
                                    If you did not request a password reset, please ignore this email. Your account remains completely secure.
                                </p>
                            </div>
                        </div>";

                    using (var smtp = new SmtpClient(host, port))
                    {
                        smtp.EnableSsl = enableSsl;
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = new NetworkCredential(senderEmail, senderPassword);
                        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                        smtp.Timeout = 20000;

                        smtp.Send(message);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[EmailHelper Error] {ex.Message}");
                return false;
            }
        }

        /// Sends an initial password-reset invitation email to a newly created Trainer.
        public static bool SendTrainerInvitationEmail(string recipientEmail, string recipientName, string invitationLink, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                string host = ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
                int port = int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out int p) ? p : 587;
                string senderEmail = ConfigurationManager.AppSettings["SmtpSenderEmail"] ?? "";
                string senderPassword = ConfigurationManager.AppSettings["SmtpSenderPassword"] ?? "";
                bool enableSsl = bool.TryParse(ConfigurationManager.AppSettings["SmtpEnableSsl"], out bool ssl) ? ssl : true;

                if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword) 
                    || senderEmail.Contains("YOUR_") || senderPassword.Contains("YOUR_"))
                {
                    errorMessage = "Gmail sender email & 16-character Google App Password are not yet configured in Web.config.";
                    return false;
                }

                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(senderEmail, "HealthTrack Administration");
                    message.To.Add(new MailAddress(recipientEmail, recipientName));
                    message.Subject = "Welcome to HealthTrack! Activate Your Trainer Account";
                    message.IsBodyHtml = true;
                    message.Body = $@"
                        <div style='background-color: #0f0d1b; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; padding: 40px 20px; color: #ffffff;'>
                            <div style='max-width: 540px; margin: 0 auto; background: #1c1833; border: 1px solid rgba(109, 84, 209, 0.35); border-radius: 16px; padding: 36px 30px; box-shadow: 0 10px 30px rgba(0, 0, 0, 0.4);'>
                                <div style='text-align: center; margin-bottom: 24px;'>
                                    <h2 style='color: #a894ff; margin: 0; font-size: 26px; font-weight: 700; letter-spacing: -0.5px;'>HealthTrack</h2>
                                    <p style='color: #7d7799; margin: 4px 0 0; font-size: 13px;'>Trainer Portal Onboarding</p>
                                </div>
                                
                                <p style='color: #ffffff; font-size: 16px; margin: 0 0 14px;'>Hello <strong>{recipientName}</strong>,</p>
                                <p style='color: #b3aec9; font-size: 14px; line-height: 1.6; margin: 0 0 20px;'>
                                    An authorized Administrator has created a <strong>Trainer Account</strong> for you on the HealthTrack Fitness Management System.
                                </p>
                                <p style='color: #b3aec9; font-size: 14px; line-height: 1.6; margin: 0 0 24px;'>
                                    To activate your account and start managing your clients and workout routines, please click the button below to set up your secure password:
                                </p>
                                
                                <div style='text-align: center; margin: 28px 0;'>
                                    <a href='{invitationLink}' style='background: linear-gradient(135deg, #7c5ce9, #5e3cd9); color: #ffffff; padding: 14px 34px; text-decoration: none; border-radius: 10px; font-weight: 600; display: inline-block; font-size: 15px; box-shadow: 0 4px 15px rgba(109, 84, 209, 0.4);'>
                                        Set Up Password &amp; Activate Account
                                    </a>
                                </div>
                                
                                <p style='color: #8b85a3; font-size: 12px; line-height: 1.5; margin: 24px 0 0;'>
                                    ⏰ <em>Note: This initial activation invitation link will expire in 48 hours for security reasons.</em>
                                </p>
                                
                                <p style='color: #7d7799; font-size: 12px; line-height: 1.5; margin: 12px 0 0;'>
                                    If the button doesn't work, copy and paste this link into your web browser:<br />
                                    <a href='{invitationLink}' style='color: #a894ff; word-break: break-all;'>{invitationLink}</a>
                                </p>
                                
                                <hr style='border: none; border-top: 1px solid rgba(255, 255, 255, 0.08); margin: 24px 0;' />
                                
                                <p style='color: #615c7a; font-size: 11px; margin: 0; text-align: center;'>
                                    If you were not expecting this invitation, please contact your gym administrator.
                                </p>
                            </div>
                        </div>";

                    using (var smtp = new SmtpClient(host, port))
                    {
                        smtp.EnableSsl = enableSsl;
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = new NetworkCredential(senderEmail, senderPassword);
                        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                        smtp.Timeout = 20000;

                        smtp.Send(message);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[EmailHelper Error] {ex.Message}");
                return false;
            }
        }
    }
}
