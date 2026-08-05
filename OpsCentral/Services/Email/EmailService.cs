using System.Net.Mail;
using Microsoft.Extensions.Options;
using OpsCentral.Options;

namespace OpsCentral.Services.Email;

public class EmailService(IOptions<SmtpOptions> options) : IEmailService
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = false,
            UseDefaultCredentials = false
        };

        using var message = new MailMessage(_options.FromAddress, to, subject, body);

        await client.SendMailAsync(message, ct);
    }
}
