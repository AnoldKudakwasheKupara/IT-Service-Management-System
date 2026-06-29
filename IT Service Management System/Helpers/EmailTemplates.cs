namespace IT_Service_Management_System.Helpers
{
    /// <summary>
    /// Branded HTML email templates. All return a self-contained HTML string ready for SendEmailAsync.
    /// </summary>
    public static class EmailTemplates
    {
        // ── shared chrome ────────────────────────────────────────────────────────────

        private static string Wrap(string firstName, string bodyContent, string footerNote = "") => $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>Axis IT Operations</title>
</head>
<body style=""margin:0;padding:0;background:#f1f5f9;font-family:'Segoe UI',Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f1f5f9;padding:32px 16px;"">
    <tr>
      <td align=""center"">
        <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;width:100%;"">

          <!-- Header -->
          <tr>
            <td style=""background:linear-gradient(135deg,#1d4ed8 0%,#3b82f6 100%);
                        border-radius:12px 12px 0 0;padding:32px 40px;text-align:center;"">
              <div style=""display:inline-block;background:rgba(255,255,255,0.15);
                           border-radius:12px;padding:10px 18px;margin-bottom:12px;"">
                <span style=""font-size:22px;font-weight:700;color:#fff;letter-spacing:1px;"">
                  &#9881; Axis IT Operations
                </span>
              </div>
              <p style=""color:rgba(255,255,255,0.85);font-size:13px;margin:4px 0 0;"">IT Service Management System</p>
            </td>
          </tr>

          <!-- Body -->
          <tr>
            <td style=""background:#ffffff;padding:40px;border-left:1px solid #e2e8f0;border-right:1px solid #e2e8f0;"">
              <p style=""color:#64748b;font-size:14px;margin:0 0 24px;"">Hi <strong style=""color:#1e293b;"">{firstName}</strong>,</p>
              {bodyContent}
              <p style=""color:#64748b;font-size:14px;margin:32px 0 0;"">Kind regards,<br/>
                <strong style=""color:#1e293b;"">Axis IT Support Team</strong>
              </p>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style=""background:#f8fafc;border:1px solid #e2e8f0;border-top:none;
                        border-radius:0 0 12px 12px;padding:20px 40px;text-align:center;"">
              <p style=""color:#94a3b8;font-size:12px;margin:0;"">
                &#128274; This is an automated message from the Axis IT Service Management System.
                {(string.IsNullOrEmpty(footerNote) ? "" : $"<br/>{footerNote}")}
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

        private static string PrimaryButton(string href, string label) => $@"
<table cellpadding=""0"" cellspacing=""0"" style=""margin:24px 0;"">
  <tr>
    <td style=""background:linear-gradient(135deg,#1d4ed8,#3b82f6);border-radius:8px;"">
      <a href=""{href}""
         style=""display:inline-block;padding:14px 32px;color:#ffffff;font-weight:600;
                 font-size:15px;text-decoration:none;border-radius:8px;"">
        {label}
      </a>
    </td>
  </tr>
</table>";

        private static string InfoBox(string html) => $@"
<div style=""background:#eff6ff;border-left:4px solid #3b82f6;border-radius:0 8px 8px 0;
             padding:14px 18px;margin:20px 0;font-size:13px;color:#1e40af;"">
  {html}
</div>";

        private static string WarningBox(string html) => $@"
<div style=""background:#fffbeb;border-left:4px solid #f59e0b;border-radius:0 8px 8px 0;
             padding:14px 18px;margin:20px 0;font-size:13px;color:#92400e;"">
  {html}
</div>";

        private static string SuccessBox(string html) => $@"
<div style=""background:#f0fdf4;border-left:4px solid #22c55e;border-radius:0 8px 8px 0;
             padding:14px 18px;margin:20px 0;font-size:13px;color:#166534;"">
  {html}
</div>";

        // ── templates ────────────────────────────────────────────────────────────────

        /// <summary>Sent when admin creates a new user account.</summary>
        public static string WelcomeActivation(string firstName, string activationLink, string appBaseUrl)
        {
            var body = $@"
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:0 0 16px;"">
  Welcome to the <strong>Axis IT Service Management System</strong>. Your account has been created
  and is ready for activation.
</p>
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:0 0 4px;"">
  Click the button below to set your password and activate your account:
</p>
{PrimaryButton(activationLink, "&#128273;&nbsp; Activate My Account")}
{WarningBox("&#9201;&nbsp; This link expires in <strong>24 hours</strong>. If you did not expect this email, you can safely ignore it.")}
<p style=""color:#64748b;font-size:13px;margin:16px 0 0;"">
  If the button doesn't work, copy and paste this link into your browser:<br/>
  <a href=""{activationLink}"" style=""color:#3b82f6;word-break:break-all;"">{activationLink}</a>
</p>";

            return Wrap(firstName, body,
                "Do not share this link. It is personal and single-use.");
        }

        /// <summary>Sent when a user requests a password reset via Forgot Password.</summary>
        public static string PasswordReset(string firstName, string resetLink)
        {
            var body = $@"
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:0 0 16px;"">
  We received a request to reset the password for your account.
  If you made this request, click the button below:
</p>
{PrimaryButton(resetLink, "&#128274;&nbsp; Reset My Password")}
{WarningBox("&#9201;&nbsp; This link expires in <strong>24 hours</strong>.")}
{InfoBox("&#128683;&nbsp; If you did not request a password reset, you can safely ignore this email. Your password will not change.")}
<p style=""color:#64748b;font-size:13px;margin:16px 0 0;"">
  If the button doesn't work, copy and paste this link into your browser:<br/>
  <a href=""{resetLink}"" style=""color:#3b82f6;word-break:break-all;"">{resetLink}</a>
</p>";

            return Wrap(firstName, body,
                "This reset link is single-use and expires in 24 hours.");
        }

        /// <summary>Sent after a user successfully sets or resets their password.</summary>
        public static string PasswordChanged(string firstName, string loginUrl, bool isNewAccount = false)
        {
            var action = isNewAccount ? "activated and your password has been set" : "password has been changed";
            var body = $@"
{SuccessBox($"&#10003;&nbsp; Your account has been <strong>{action}</strong> successfully.")}
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:16px 0 16px;"">
  You can now sign in to the Axis IT Service Management System using your email address and new password.
</p>
{PrimaryButton(loginUrl, "&#8594;&nbsp; Go to Login")}
{WarningBox("&#128683;&nbsp; If you did not make this change, contact IT Support immediately.")}";

            return Wrap(firstName, body);
        }

        /// <summary>Sent when admin resends an activation link to an inactive user.</summary>
        public static string ResendActivation(string firstName, string activationLink)
        {
            var body = $@"
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:0 0 16px;"">
  A new account activation link has been generated for your account.
  Your previous link (if any) has been invalidated.
</p>
{PrimaryButton(activationLink, "&#128273;&nbsp; Activate My Account")}
{WarningBox("&#9201;&nbsp; This link expires in <strong>24 hours</strong>.")}
<p style=""color:#64748b;font-size:13px;margin:16px 0 0;"">
  If the button doesn't work, copy and paste this link into your browser:<br/>
  <a href=""{activationLink}"" style=""color:#3b82f6;word-break:break-all;"">{activationLink}</a>
</p>";

            return Wrap(firstName, body,
                "Do not share this link. It is personal and single-use.");
        }
    }
}
