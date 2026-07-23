using System.Net;
using System.Net.Mail;

namespace IndkobsApp.Api.Services;

/// <summary>
/// Udsender e-mails (bekræftelse, kode-nulstilling, invitation). Bag et interface, så
/// udbyderen kan skiftes uden at røre auth-logikken:
///  - Dev: <see cref="ConsoleEmailSender"/> logger mailen (inkl. link/token) til konsollen.
///  - Prod: <see cref="SmtpEmailSender"/> sender via SMTP (fx SendGrid/Mailgun/Gmail) sat via config.
/// Valget træffes i <c>Program.cs</c> ud fra <c>Email:Provider</c> (standard "console").
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>
/// Dev-standard: skriver e-mailen til loggen i stedet for at sende den. Gør at fx
/// glemt-kode-flowet kan testes end-to-end lokalt — nulstillingslinket står i konsollen.
/// </summary>
public sealed class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _log;
    public ConsoleEmailSender(ILogger<ConsoleEmailSender> log) => _log = log;

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        _log.LogInformation(
            "\n===== E-MAIL (dev, ikke afsendt) =====\nTil: {To}\nEmne: {Subject}\n{Body}\n=====================================",
            toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Prod-udbyder: sender via SMTP. Konfigureres udelukkende via config/env (ingen hardcodet
/// udbyder), fx SendGrid: Host=smtp.sendgrid.net, Port=587, User=apikey, Password=&lt;api-key&gt;.
/// Nødvendige nøgler: Email:Smtp:Host, Email:Smtp:Port, Email:From, samt evt.
/// Email:Smtp:User/Password og Email:Smtp:UseSsl (standard true).
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<SmtpEmailSender> _log;
    public SmtpEmailSender(IConfiguration cfg, ILogger<SmtpEmailSender> log) { _cfg = cfg; _log = log; }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = _cfg["Email:Smtp:Host"];
        var from = _cfg["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            _log.LogError("SMTP er valgt (Email:Provider=smtp), men Email:Smtp:Host/Email:From mangler — mail til {To} blev IKKE sendt.", toEmail);
            return;
        }

        var port = _cfg.GetValue<int?>("Email:Smtp:Port") ?? 587;
        var user = _cfg["Email:Smtp:User"];
        var pass = _cfg["Email:Smtp:Password"];
        var useSsl = _cfg.GetValue<bool?>("Email:Smtp:UseSsl") ?? true;

        using var client = new SmtpClient(host, port) { EnableSsl = useSsl };
        if (!string.IsNullOrWhiteSpace(user))
            client.Credentials = new NetworkCredential(user, pass);

        using var msg = new MailMessage(from, toEmail, subject, htmlBody) { IsBodyHtml = true };
        await client.SendMailAsync(msg, ct);
    }
}
