using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace QCPCMvc.Services;

public class EmailSettings
{
    public string Host        { get; set; } = "svdrmailbox001.ethiopianairlines.com";
    public int    Port        { get; set; } = 587;
    public bool   UseSsl      { get; set; } = false;  // false = STARTTLS on 587
    public string UserName    { get; set; } = "";
    public string Password    { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName    { get; set; } = "QCPC Issue Tracker";
    public bool   Enabled     { get; set; } = true;  // set true in production
    public bool   AcceptInvalidCertificates { get; set; } = true;  // For internal SMTP servers with self-signed certs
}

public class EmailService
{
    private readonly EmailSettings _cfg;
    private readonly ILogger<EmailService> _log;

    public EmailService(EmailSettings cfg, ILogger<EmailService> log)
    { _cfg = cfg; _log = log; }

    // ── Mention notification ──────────────────────────────────────────────────
    public async Task SendMentionEmailAsync(
        string toEmail, string toName,
        string mentionedByName,
        string issueTitle, int issueId,
        string baseUrl)
    {
        var issueUrl = $"{baseUrl.TrimEnd('/')}/Issues/Detail/{issueId}";
        var subject  = $"[QCPC] {mentionedByName} mentioned you in \"{issueTitle}\"";
        var html = HtmlTemplate(
            title: "You were mentioned",
            preheader: $"{mentionedByName} mentioned you on an issue",
            body: $@"
            <p style='margin:0 0 16px 0; font-family:Arial, sans-serif; font-size:14px; line-height:1.5; color:#333333;'>Hi <strong>{Encode(toName)}</strong>,</p>
            <p style='margin:0 0 16px 0; font-family:Arial, sans-serif; font-size:14px; line-height:1.5; color:#333333;'>
                <strong>{Encode(mentionedByName)}</strong> mentioned you on the QCPC Issue Tracker
                and would like your participation:
            </p>
            <table width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 24px 0;'>
                <tr>
                    <td style='background-color:#F8F9FF; border-left:4px solid #4C6EF5; padding:16px 20px; font-family:Arial, sans-serif; font-size:14px; color:#333333;'>
                        <strong style='color:#1A1F36; font-size:15px;'>{Encode(issueTitle)}</strong>
                    </td>
                </tr>
            </table>
            <p style='margin:0 0 24px 0; font-family:Arial, sans-serif; font-size:14px; color:#6B7280; line-height:1.5;'>
                Please visit the issue to answer or leave a comment — your expertise is valued!
            </p>
            <table width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 24px 0;'>
                <tr>
                    <td align='center'>
                        <table cellpadding='0' cellspacing='0' style='margin:0 auto;'>
                            <tr>
                                <td align='center' bgcolor='#4C6EF5' style='background-color:#4C6EF5; padding:12px 28px; border-radius:4px;'>
                                    <a href='{issueUrl}' style='color:#ffffff; font-family:Arial, sans-serif; font-size:14px; font-weight:600; text-decoration:none; display:inline-block;'>View Issue & Respond</a>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>",
            footerNote: "You received this because someone mentioned you on QCPC Issue Tracker."
        );

        await SendAsync(toEmail, toName, subject, html);
    }

    // ── OTP email ─────────────────────────────────────────────────────────────
    public async Task SendOtpEmailAsync(
        string toEmail, string toName, string otp, int expiryMinutes = 10)
    {
        var subject = "[QCPC] Your verification code";
        var html = HtmlTemplate(
            title: "Verify your email",
            preheader: "Your QCPC email verification code",
            body: $@"
            <p style='margin:0 0 16px 0; font-family:Arial, sans-serif; font-size:14px; line-height:1.5; color:#333333;'>Hi <strong>{Encode(toName)}</strong>,</p>
            <p style='margin:0 0 24px 0; font-family:Arial, sans-serif; font-size:14px; line-height:1.5; color:#333333;'>
                Please use the code below to verify your Ethiopian Airlines email address
                and complete your QCPC registration:
            </p>
            <table width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 24px 0;'>
                <tr>
                    <td align='center'>
                        <table cellpadding='0' cellspacing='0' style='margin:0 auto; border:2px solid #E5E7EB; border-radius:8px; background-color:#F9FAFB;'>
                            <tr>
                                <td align='center' style='padding:24px 40px; font-family:Consolas, Monaco, 'Courier New', monospace; font-size:38px; font-weight:800; letter-spacing:8px; color:#1A1F36; line-height:1.2;'>
                                    {otp}
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
            <table width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 8px 0;'>
                <tr>
                    <td style='font-family:Arial, sans-serif; font-size:13px; color:#6B7280; line-height:1.5;'>
                        ⏱ This code expires in <strong>{expiryMinutes} minutes</strong>.
                    </td>
                </tr>
            </table>
            <table width='100%' cellpadding='0' cellspacing='0'>
                <tr>
                    <td style='font-family:Arial, sans-serif; font-size:13px; color:#6B7280; line-height:1.5;'>
                        If you did not request this, please ignore this email.
                    </td>
                </tr>
            </table>",
            footerNote: "You received this because someone registered with your email on QCPC Issue Tracker."
        );

        await SendAsync(toEmail, toName, subject, html);
    }

    // ── Password Reset email ───────────────────────────────────────────────────
    public async Task SendPasswordResetEmailAsync(
        string toEmail, string toName, string resetUrl)
    {
        var subject = "[QCPC] Reset your password";
        var html = HtmlTemplate(
            title: "Reset your password",
            preheader: "Reset your QCPC password",
            body: $@"
            <p style='margin:0 0 16px 0; font-family:Arial, sans-serif; font-size:14px; line-height:1.5; color:#333333;'>Hi <strong>{Encode(toName)}</strong>,</p>
            <p style='margin:0 0 24px 0; font-family:Arial, sans-serif; font-size:14px; line-height:1.5; color:#333333;'>
                We received a request to reset your password for the QCPC Issue Tracker.
                Click the button below to create a new password:
            </p>
            <table width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 24px 0;'>
                <tr>
                    <td align='center'>
                        <table cellpadding='0' cellspacing='0' style='margin:0 auto;'>
                            <tr>
                                <td align='center' bgcolor='#4C6EF5' style='background-color:#4C6EF5; padding:14px 32px; border-radius:4px;'>
                                    <a href='{resetUrl}' style='color:#ffffff; font-family:Arial, sans-serif; font-size:15px; font-weight:600; text-decoration:none; display:inline-block;'>Reset Password</a>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
            <p style='margin:0 0 16px 0; font-family:Arial, sans-serif; font-size:13px; color:#6B7280; line-height:1.5;'>
                This link will expire in 24 hours.
            </p>
            <p style='margin:0 0 24px 0; font-family:Arial, sans-serif; font-size:13px; color:#6B7280; line-height:1.5;'>
                If you did not request a password reset, you can safely ignore this email.
                Your password will remain unchanged.
            </p>
            <table width='100%' cellpadding='0' cellspacing='0' style='margin:16px 0 0 0; border-top:1px solid #E5E7EB;'>
                <tr>
                    <td style='padding-top:16px; font-family:Arial, sans-serif; font-size:12px; color:#6B7280; line-height:1.5;'>
                        If the button above doesn't work, copy and paste this link into your browser:<br/>
                        <a href='{resetUrl}' style='color:#4C6EF5; text-decoration:underline; word-break:break-all;'>{resetUrl}</a>
                    </td>
                </tr>
            </table>",
            footerNote: "You received this because a password reset was requested for your QCPC Issue Tracker account."
        );

        await SendAsync(toEmail, toName, subject, html);
    }

    // ── New Issue Notification to Department Group ────────────────────────────────
    public async Task SendNewIssueNotificationToGroupAsync(
        string groupEmail, string departmentName,
        string submittedByName, string issueTitle, int responseNumber, string baseUrl)
    {
        var issueUrl = $"{baseUrl.TrimEnd('/')}/Issues/Detail/{responseNumber}";
        var subject  = $"[QCPC] New Issue #{responseNumber} - {issueTitle}";
        var html = HtmlTemplate(
            title: "New Issue Created",
            preheader: $"New issue submitted by {submittedByName}",
            body: $@"
            <p style='margin:0 0 16px 0; font-family:Arial, sans-serif; font-size:14px; line-height:1.5; color:#333333;'>Hi <strong>{Encode(departmentName)} Team</strong>,</p>
            <p style='margin:0 0 16px 0; font-family:Arial, sans-serif; font-size:14px; line-height:1.5; color:#333333;'>
                A new issue has been submitted to the QCPC Issue Tracker.
            </p>
            <table width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 24px 0;'>
                <tr>
                    <td style='background-color:#F8F9FF; border-left:4px solid #4C6EF5; padding:16px 20px; font-family:Arial, sans-serif;'>
                        <strong style='color:#1A1F36; font-size:15px; display:block; margin-bottom:6px;'>#{responseNumber} - {Encode(issueTitle)}</strong>
                        <span style='color:#6B7280; font-size:13px;'>Submitted by: {Encode(submittedByName)}</span>
                    </td>
                </tr>
            </table>
            <p style='margin:0 0 24px 0; font-family:Arial, sans-serif; font-size:14px; color:#6B7280; line-height:1.5;'>
                Please review and take appropriate action.
            </p>
            <table width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 24px 0;'>
                <tr>
                    <td align='center'>
                        <table cellpadding='0' cellspacing='0' style='margin:0 auto;'>
                            <tr>
                                <td align='center' bgcolor='#4C6EF5' style='background-color:#4C6EF5; padding:12px 28px; border-radius:4px;'>
                                    <a href='{issueUrl}' style='color:#ffffff; font-family:Arial, sans-serif; font-size:14px; font-weight:600; text-decoration:none; display:inline-block;'>View Issue</a>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>",
            footerNote: "You received this because you are a member of the " + Encode(departmentName) + " department on QCPC Issue Tracker."
        );

        await SendAsync(groupEmail, departmentName + " Team", subject, html);
    }

    // ── Core send ─────────────────────────────────────────────────────────────
    private async Task SendAsync(string toEmail, string toName, string subject, string html)
    {
        if (!_cfg.Enabled)
        {
            _log.LogInformation("[Email DISABLED] To:{To} Subject:{Subject}", toEmail, subject);
            return;
        }

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_cfg.FromName, _cfg.FromAddress));
            msg.To.Add(new MailboxAddress(toName, toEmail));
            msg.Subject = subject;

            var body = new BodyBuilder { HtmlBody = html };
            msg.Body = body.ToMessageBody();

            using var client = new SmtpClient();
            
            // Handle SSL certificate validation for internal SMTP servers with self-signed certs
            if (_cfg.AcceptInvalidCertificates)
            {
                client.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }
            
            await client.ConnectAsync(_cfg.Host, _cfg.Port,
                _cfg.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_cfg.UserName, _cfg.Password);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);

            _log.LogInformation("[Email SENT] To:{To} Subject:{Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[Email ERROR] To:{To} Subject:{Subject}", toEmail, subject);
            // Don't rethrow — email failure shouldn't crash the user's request
        }
    }

    // ── HTML Template ─────────────────────────────────────────────────────────
    private static string HtmlTemplate(string title, string preheader,
        string body, string footerNote)
    {
        return $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='utf-8'/>
    <meta name='viewport' content='width=device-width, initial-scale=1'/>
    <title>{Encode(title)}</title>
</head>
<body style='margin:0; padding:0; background-color:#F5F7FA; font-family:Arial, sans-serif;'>
    
    <!-- Preheader (hidden preview text) -->
    <div style='display:none; font-size:1px; color:#F5F7FA; line-height:1px; max-height:0px; max-width:0px; opacity:0; overflow:hidden;'>
        {Encode(preheader)}
    </div>

    <!-- Main Email Container -->
    <table width='100%' cellpadding='0' cellspacing='0' border='0' style='background-color:#F5F7FA; padding:30px 10px;'>
        <tr>
            <td align='center' style='padding:0;'>
                
                <!-- Outer Table (600px max width) -->
                <table width='100%' cellpadding='0' cellspacing='0' border='0' style='max-width:600px; width:100%; background-color:#FFFFFF; border-radius:8px; box-shadow:0 2px 8px rgba(0,0,0,0.05); border-collapse:separate;'>
                    
                    <!-- Header -->
                    <tr>
                        <td style='background-color:#4C6EF5; padding:24px 32px; border-radius:8px 8px 0 0;'>
                            <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                                <tr>
                                    <td width='40' style='width:40px;'>
                                        <table cellpadding='0' cellspacing='0' border='0' style='background-color:rgba(255,255,255,0.15); border-radius:8px; width:40px; height:40px;'>
                                            <tr>
                                                <td align='center' valign='middle' style='font-size:22px; line-height:40px; color:#FFFFFF; text-align:center; vertical-align:middle;'>🛡</td>
                                            </tr>
                                        </table>
                                    </td>
                                    <td style='padding-left:12px;'>
                                        <div style='color:#FFFFFF; font-family:Arial, sans-serif; font-weight:bold; font-size:18px; letter-spacing:0.5px;'>QCPC</div>
                                        <div style='color:rgba(255,255,255,0.7); font-family:Arial, sans-serif; font-size:12px;'>Issue Tracker</div>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Body Content -->
                    <tr>
                        <td style='padding:32px 32px 24px 32px; background-color:#FFFFFF;'>
                            
                            <!-- Title -->
                            <h1 style='margin:0 0 24px 0; font-family:Arial, sans-serif; font-weight:bold; font-size:22px; color:#1A1F36; line-height:1.3;'>
                                {Encode(title)}
                            </h1>
                            
                            <!-- Main Body Content (injected) -->
                            {body}
                            
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background-color:#F9FAFC; padding:24px 32px; border-top:1px solid #E5E7EB; border-radius:0 0 8px 8px;'>
                            <p style='margin:0; font-family:Arial, sans-serif; font-size:12px; color:#6B7280; line-height:1.6;'>
                                {Encode(footerNote)}<br/>
                                Ethiopian Airlines · QCPC Quality Control Process Centre
                            </p>
                            <p style='margin:16px 0 0 0; font-family:Arial, sans-serif; font-size:11px; color:#9CA3AF; line-height:1.5; border-top:1px solid #E5E7EB; padding-top:16px;'>
                                This is an automated message from QCPC Issue Tracker. Please do not reply to this email.
                            </p>
                        </td>
                    </tr>
                    
                </table>
                <!-- End Outer Table -->
                
            </td>
        </tr>
    </table>
    
    <!-- Outlook-specific fix for background colors -->
    <!--[if gte mso 9]>
    <style>
        .outlook-table {{ border-collapse: collapse; mso-table-lspace: 0pt; mso-table-rspace: 0pt; }}
        .outlook-cell {{ mso-line-height-rule: exactly; }}
    </style>
    <![endif]-->
    
</body>
</html>";
    }

    private static string Encode(string s) =>
        System.Net.WebUtility.HtmlEncode(s);
}