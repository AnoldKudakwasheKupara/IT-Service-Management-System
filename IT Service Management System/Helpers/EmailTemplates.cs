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

        /// <summary>Email OTP code for MFA challenge during login.</summary>
        public static string MfaCode(string firstName, string code, int validityMinutes)
        {
            var body = $@"
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:0 0 16px;"">
  Use the verification code below to finish signing in:
</p>
<div style=""text-align:center;margin:24px 0;"">
  <div style=""display:inline-block;background:#eff6ff;border:1px dashed #3b82f6;border-radius:12px;
               padding:18px 32px;font-size:34px;font-weight:700;letter-spacing:8px;color:#1d4ed8;"">
    {code}
  </div>
</div>
{WarningBox($"&#9201;&nbsp; This code expires in <strong>{validityMinutes} minute(s)</strong>. Don't share it with anyone.")}
{InfoBox("&#128683;&nbsp; If you didn't try to sign in, change your password immediately — someone may have it.")}";

            return Wrap(firstName, body, "Axis IT will never ask you for this code.");
        }

        /// <summary>Sent when a user's email address is changed.</summary>
        public static string EmailChanged(string firstName, string oldEmail, string newEmail)
        {
            var body = $@"
{SuccessBox("&#10003;&nbsp; The email address on your account was changed.")}
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:16px 0;"">
  Old address: <strong>{oldEmail}</strong><br/>
  New address: <strong>{newEmail}</strong>
</p>
{WarningBox("&#128683;&nbsp; If you didn't make this change, contact IT Support immediately.")}";

            return Wrap(firstName, body);
        }

        /// <summary>Sent when a user's account role/permissions are changed by an admin.</summary>
        public static string RoleChanged(string firstName, string oldRole, string newRole)
        {
            var body = $@"
{SuccessBox("&#10003;&nbsp; The role on your account was changed.")}
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:16px 0;"">
  Previous role: <strong>{oldRole}</strong><br/>
  New role: <strong>{newRole}</strong>
</p>
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:16px 0;"">
  This may change what you can access in the system. The change takes effect the next time you sign in.
</p>
{WarningBox("&#128683;&nbsp; If you didn't expect this change, contact IT Support immediately.")}";

            return Wrap(firstName, body);
        }

        /// <summary>Sent when a user's phone number is changed.</summary>
        public static string PhoneChanged(string firstName, string? oldPhone, string newPhone)
        {
            var body = $@"
{SuccessBox("&#10003;&nbsp; The phone number on your account was changed.")}
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:16px 0;"">
  Old number: <strong>{(string.IsNullOrWhiteSpace(oldPhone) ? "(none)" : oldPhone)}</strong><br/>
  New number: <strong>{newPhone}</strong>
</p>
{WarningBox("&#128683;&nbsp; If you didn't make this change, contact IT Support immediately.")}";

            return Wrap(firstName, body);
        }

        /// <summary>Sent when a sign-in occurs from a device/IP not seen before.</summary>
        public static string NewDeviceLogin(string firstName, string device, string ip, string when)
        {
            var body = $@"
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:0 0 16px;"">
  Your account was just signed in from a device we haven't seen before:
</p>
<div style=""background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:14px 18px;margin:16px 0;font-size:14px;color:#334155;"">
  <strong>Device:</strong> {device}<br/>
  <strong>IP address:</strong> {ip}<br/>
  <strong>When:</strong> {when}
</div>
{WarningBox("&#128683;&nbsp; If this wasn't you, change your password and use \"Log out from all devices\" right away.")}";

            return Wrap(firstName, body);
        }

        /// <summary>Sent after repeated failed sign-in attempts (before lockout).</summary>
        public static string FailedLoginAttempt(string firstName, int attempts, string ip)
        {
            var body = $@"
{WarningBox($"&#9888;&nbsp; There have been <strong>{attempts}</strong> failed sign-in attempts on your account.")}
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:16px 0;"">
  Most recent attempt came from IP <strong>{ip}</strong>. If this was you, no action is needed.
  If not, consider changing your password.
</p>";

            return Wrap(firstName, body);
        }

        /// <summary>Sent when an account is locked due to failed sign-ins.</summary>
        public static string AccountLocked(string firstName, string until, string resetLink)
        {
            var body = $@"
{WarningBox($"&#128274;&nbsp; Your account has been <strong>locked</strong> until {until} after repeated failed sign-ins.")}
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:16px 0;"">
  You can wait for the lockout to expire, or reset your password now to regain access:
</p>
{PrimaryButton(resetLink, "&#128273;&nbsp; Reset My Password")}
{InfoBox("&#128683;&nbsp; If this wasn't you, reset your password — your credentials may be known to someone else.")}";

            return Wrap(firstName, body);
        }

        /// <summary>Sent when MFA is turned off for an account.</summary>
        public static string MfaDisabled(string firstName)
        {
            var body = $@"
{WarningBox("&#9888;&nbsp; Two-factor authentication (email OTP) has been <strong>disabled</strong> on your account.")}
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:16px 0;"">
  Your account is now protected by your password only. We recommend keeping MFA enabled for stronger security.
</p>
{InfoBox("&#128683;&nbsp; If you didn't disable MFA, re-enable it and change your password immediately.")}";

            return Wrap(firstName, body);
        }

        /// <summary>Generic security alert sent to administrators.</summary>
        public static string AdminAlert(string title, string severity, string intro, IEnumerable<KeyValuePair<string, string>> facts)
        {
            var color = severity == "critical" ? "#dc2626" : severity == "warning" ? "#f59e0b" : "#3b82f6";
            var rows = string.Join("", facts.Select(f => $@"
              <tr>
                <td style=""padding:6px 12px;color:#64748b;font-size:13px;white-space:nowrap;vertical-align:top;"">{f.Key}</td>
                <td style=""padding:6px 12px;color:#1e293b;font-size:13px;font-weight:600;"">{f.Value}</td>
              </tr>"));

            var body = $@"
<div style=""background:{color}1a;border-left:4px solid {color};border-radius:0 8px 8px 0;padding:14px 18px;margin:0 0 20px;"">
  <div style=""font-size:15px;font-weight:700;color:{color};"">&#9888;&nbsp; {title}</div>
</div>
<p style=""color:#334155;font-size:14px;line-height:1.7;margin:0 0 16px;"">{intro}</p>
<table style=""width:100%;border-collapse:collapse;background:#f8fafc;border-radius:8px;overflow:hidden;"">
  {rows}
</table>
<p style=""color:#64748b;font-size:12px;margin:18px 0 0;"">Review this in the Security Dashboard &rarr; Recent Security Events.</p>";

            return Wrap("Administrator", body, "Automated security alert — Axis IT Operations.");
        }

        // ── Ticketing ────────────────────────────────────────────────────────────────

        private static string TicketFacts(string reference, string title, string priority, string status) => $@"
<table style=""width:100%;border-collapse:collapse;background:#f8fafc;border-radius:8px;overflow:hidden;margin:16px 0;"">
  <tr><td style=""padding:7px 14px;color:#64748b;font-size:13px;width:120px;"">Reference</td><td style=""padding:7px 14px;color:#1e293b;font-size:13px;font-weight:700;"">{reference}</td></tr>
  <tr><td style=""padding:7px 14px;color:#64748b;font-size:13px;"">Subject</td><td style=""padding:7px 14px;color:#1e293b;font-size:13px;font-weight:600;"">{title}</td></tr>
  <tr><td style=""padding:7px 14px;color:#64748b;font-size:13px;"">Priority</td><td style=""padding:7px 14px;color:#1e293b;font-size:13px;"">{priority}</td></tr>
  <tr><td style=""padding:7px 14px;color:#64748b;font-size:13px;"">Status</td><td style=""padding:7px 14px;color:#1e293b;font-size:13px;"">{status}</td></tr>
</table>";

        private static string Quote(string text) => $@"
<div style=""background:#eff6ff;border-left:4px solid #3b82f6;border-radius:0 8px 8px 0;padding:14px 18px;margin:16px 0;color:#1e293b;font-size:14px;line-height:1.6;white-space:pre-wrap;"">{System.Net.WebUtility.HtmlEncode(text)}</div>";

        /// <summary>To admins/agents when a new ticket is logged.</summary>
        public static string TicketCreatedForStaff(string reference, string title, string description,
            string priority, string createdByName, string link)
        {
            var body = $@"
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:0 0 8px;"">
  A new support ticket has been logged by <strong>{createdByName}</strong> and needs attention.
</p>
{TicketFacts(reference, title, priority, "Open")}
<p style=""color:#64748b;font-size:13px;margin:0 0 4px;"">Description:</p>
{Quote(description)}
{PrimaryButton(link, "&#128203;&nbsp; Open Ticket")}";
            return Wrap("Team", body, "You receive this because you are an administrator.");
        }

        /// <summary>To the other party when a reply is posted.</summary>
        public static string TicketReply(string recipientFirstName, string reference, string title,
            string replierName, string message, string link)
        {
            var body = $@"
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:0 0 8px;"">
  <strong>{replierName}</strong> replied to ticket <strong>{reference}</strong> &mdash; <em>{title}</em>:
</p>
{Quote(message)}
{PrimaryButton(link, "&#128172;&nbsp; View &amp; Reply")}";
            return Wrap(recipientFirstName, body, $"Ticket {reference}");
        }

        /// <summary>To the ticket owner when status changes.</summary>
        public static string TicketStatusChanged(string recipientFirstName, string reference, string title,
            string newStatus, string byName, string link)
        {
            var box = newStatus == "Resolved" || newStatus == "Closed"
                ? SuccessBox($"&#10003;&nbsp; Ticket <strong>{reference}</strong> is now <strong>{newStatus}</strong>.")
                : InfoBox($"&#8505;&nbsp; Ticket <strong>{reference}</strong> status changed to <strong>{newStatus}</strong>.");
            var body = $@"
{box}
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:16px 0;"">
  <em>{title}</em> &mdash; updated by {byName}.
</p>
{PrimaryButton(link, "&#128203;&nbsp; View Ticket")}";
            return Wrap(recipientFirstName, body, $"Ticket {reference}");
        }

        /// <summary>To an agent when a ticket is assigned to them.</summary>
        public static string TicketAssigned(string assigneeFirstName, string reference, string title,
            string priority, string byName, string link)
        {
            var body = $@"
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:0 0 8px;"">
  <strong>{byName}</strong> assigned ticket <strong>{reference}</strong> to you.
</p>
{TicketFacts(reference, title, priority, "Assigned")}
{PrimaryButton(link, "&#128203;&nbsp; Open Ticket")}";
            return Wrap(assigneeFirstName, body, $"Ticket {reference}");
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

        // -- Operational reminders & digests -----------------------------------------

        private static string Enc(string? text) => System.Net.WebUtility.HtmlEncode(text ?? string.Empty);

        /// <summary>Two-column fact table. Values are encoded; keys are trusted literals.</summary>
        private static string FactTable(IEnumerable<KeyValuePair<string, string>> facts)
        {
            var rows = string.Join("", facts.Select(f => $@"
  <tr><td style=""padding:7px 14px;color:#64748b;font-size:13px;width:150px;"">{f.Key}</td><td style=""padding:7px 14px;color:#1e293b;font-size:13px;font-weight:600;"">{Enc(f.Value)}</td></tr>"));
            return $@"
<table style=""width:100%;border-collapse:collapse;background:#f8fafc;border-radius:8px;overflow:hidden;margin:16px 0;"">{rows}
</table>";
        }

        private static string DangerBox(string html) => $@"
<div style=""background:#fef2f2;border-left:4px solid #ef4444;border-radius:0 8px 8px 0;
             padding:14px 18px;margin:20px 0;font-size:13px;color:#991b1b;"">
  {html}
</div>";

        /// <summary>Chooses the callout style from how close (or past) the due date is.</summary>
        private static string DueBox(int daysUntilDue, string what)
        {
            if (daysUntilDue < 0)
                return DangerBox($"&#9888;&nbsp; {what} was due <strong>{Math.Abs(daysUntilDue)} day(s) ago</strong> and is overdue.");
            if (daysUntilDue == 0)
                return WarningBox($"&#9200;&nbsp; {what} is due <strong>today</strong>.");
            return WarningBox($"&#9200;&nbsp; {what} is due in <strong>{daysUntilDue} day(s)</strong>.");
        }

        /// <summary>An SSL certificate approaching expiry, or already expired.</summary>
        public static string CertificateExpiring(string systemName, string url, DateTime expiryDate,
            int daysRemaining, string link)
        {
            var body = $@"
{DueBox(daysRemaining, "An SSL certificate")}
{FactTable(new Dictionary<string, string>
{
    ["System"] = systemName,
    ["URL"] = url,
    ["Expires on"] = expiryDate.ToString("dddd, dd MMMM yyyy"),
    ["Days remaining"] = daysRemaining < 0 ? $"Expired {Math.Abs(daysRemaining)} day(s) ago" : daysRemaining.ToString()
})}
<p style=""color:#334155;font-size:14px;line-height:1.7;margin:16px 0 0;"">
  Renew the certificate before it lapses to avoid browser warnings and service interruption.
</p>
{PrimaryButton(link, "&#128274;&nbsp; Open SSL Tracker")}";
            return Wrap("Team", body, "Automated certificate reminder - Axis IT Operations.");
        }

        /// <summary>Scheduled maintenance coming up on an asset.</summary>
        public static string MaintenanceDue(string assetName, string maintenanceType, DateTime dueDate,
            int daysUntilDue, string? lastWorkDone, string link)
        {
            var facts = new Dictionary<string, string>
            {
                ["Asset"] = assetName,
                ["Type"] = maintenanceType,
                ["Scheduled for"] = dueDate.ToString("dddd, dd MMMM yyyy")
            };
            if (!string.IsNullOrWhiteSpace(lastWorkDone)) facts["Last work done"] = lastWorkDone!;

            var body = $@"
{DueBox(daysUntilDue, "Scheduled maintenance")}
{FactTable(facts)}
{PrimaryButton(link, "&#128295;&nbsp; Open Maintenance Records")}";
            return Wrap("Team", body, "Automated maintenance reminder - Axis IT Operations.");
        }

        /// <summary>A service payment falling due.</summary>
        public static string PaymentDue(string serviceName, decimal amount, DateTime dueDate,
            int daysUntilDue, string status, string link)
        {
            var body = $@"
{DueBox(daysUntilDue, "A service payment")}
{FactTable(new Dictionary<string, string>
{
    ["Service"] = serviceName,
    ["Amount"] = amount.ToString("N2"),
    ["Due on"] = dueDate.ToString("dddd, dd MMMM yyyy"),
    ["Status"] = status
})}
{PrimaryButton(link, "&#128179;&nbsp; Open Payments")}";
            return Wrap("Team", body, "Automated payment reminder - Axis IT Operations.");
        }

        /// <summary>A ticket escalated, either by an agent or automatically on SLA breach.</summary>
        public static string TicketEscalated(string recipientFirstName, string reference, string title,
            string priority, string byName, string? reason, string link)
        {
            var reasonBlock = string.IsNullOrWhiteSpace(reason)
                ? string.Empty
                : $@"<p style=""color:#64748b;font-size:13px;margin:0 0 4px;"">Reason:</p>{Quote(reason!)}";
            var body = $@"
{DangerBox($"&#9888;&nbsp; Ticket <strong>{Enc(reference)}</strong> has been escalated by {Enc(byName)}.")}
{TicketFacts(reference, title, priority, "Escalated")}
{reasonBlock}
{PrimaryButton(link, "&#128680;&nbsp; Open Ticket")}";
            return Wrap(recipientFirstName, body, $"Ticket {reference}");
        }

        /// <summary>An SLA warning threshold reached, or an SLA breached.</summary>
        public static string SlaAlert(string recipientFirstName, string reference, string title,
            string priority, string eventDescription, bool breached, DateTime? dueAt, string link)
        {
            var box = breached
                ? DangerBox($"&#9888;&nbsp; {Enc(eventDescription)}")
                : WarningBox($"&#9200;&nbsp; {Enc(eventDescription)}");
            var facts = new Dictionary<string, string>
            {
                ["Reference"] = reference,
                ["Subject"] = title,
                ["Priority"] = priority
            };
            if (dueAt.HasValue) facts["Target"] = dueAt.Value.ToString("ddd, dd MMM yyyy HH:mm");

            var body = $@"
{box}
{FactTable(facts)}
{PrimaryButton(link, "&#9201;&nbsp; Open Ticket")}";
            return Wrap(recipientFirstName, body, $"Ticket {reference} - SLA");
        }

        /// <summary>
        /// The daily digest. Headline figures render as a stat strip; each section is a heading
        /// plus plain lines, pre-composed by the caller so this template stays pure layout.
        /// </summary>
        public static string DailySummary(string recipientFirstName, DateTime forDate,
            IEnumerable<KeyValuePair<string, string>> headlineFigures,
            IEnumerable<(string Heading, IEnumerable<string> Lines)> sections, string link)
        {
            var cells = string.Join("", headlineFigures.Select(f => $@"
    <td align=""center"" style=""padding:14px 10px;background:#f8fafc;border-radius:8px;"">
      <div style=""font-size:24px;font-weight:700;color:#1d4ed8;"">{Enc(f.Value)}</div>
      <div style=""font-size:11px;color:#64748b;text-transform:uppercase;letter-spacing:.4px;margin-top:4px;"">{f.Key}</div>
    </td>
    <td style=""width:8px;""></td>"));

            var blocks = string.Join("", sections.Select(sec =>
            {
                var lines = sec.Lines.ToList();
                var items = lines.Count == 0
                    ? @"<li style=""color:#94a3b8;font-size:13px;"">Nothing to report.</li>"
                    : string.Join("", lines.Select(l => $@"<li style=""color:#334155;font-size:13px;line-height:1.8;"">{Enc(l)}</li>"));
                return $@"
<h3 style=""color:#1e293b;font-size:14px;margin:26px 0 8px;padding-bottom:6px;border-bottom:1px solid #e2e8f0;"">{sec.Heading}</h3>
<ul style=""margin:0;padding-left:20px;"">{items}</ul>";
            }));

            var body = $@"
<p style=""color:#334155;font-size:15px;line-height:1.7;margin:0 0 18px;"">
  Here is your summary for <strong>{forDate:dddd, dd MMMM yyyy}</strong>.
</p>
<table width=""100%"" cellpadding=""0"" cellspacing=""0""><tr>{cells}</tr></table>
{blocks}
{PrimaryButton(link, "&#128202;&nbsp; Open Dashboard")}";
            return Wrap(recipientFirstName, body, "Daily summary - Axis IT Operations.");
        }
    }
}
