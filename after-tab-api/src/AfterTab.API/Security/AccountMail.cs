using System.Net;
using System.Net.Mail;

namespace AfterTab.API.Security;
public interface IAccountMail
{
    Task SendAsync(string email, string subject, string link, CancellationToken ct);
}
public sealed class AccountMail(IConfiguration config, IWebHostEnvironment environment) : IAccountMail
{
    public async Task SendAsync(string email, string subject, string link, CancellationToken ct)
    {
        var host = config["Mail:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            // Explicit development-only file pickup, never log confirmation/reset secrets.
            if (!environment.IsDevelopment() || string.IsNullOrWhiteSpace(config["Mail:PickupDirectory"]))
                throw new InvalidOperationException("Skonfiguruj Mail:Host i Mail:From albo lokalny Mail:PickupDirectory w Development.");
        }
        using var client = new SmtpClient();
        if (string.IsNullOrWhiteSpace(host))
        {
            var directory = Path.GetFullPath(config["Mail:PickupDirectory"]!);
            Directory.CreateDirectory(directory);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = directory;
        }
        else
        {
            client.Host = host;
            client.Port = config.GetValue("Mail:Port", 587);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(config["Mail:Username"], config["Mail:Password"]);
        }
        using var message = new MailMessage(config["Mail:From"] ?? "aftertab@localhost.test", email, subject,
            $"AfterTab\n\n{subject}\n\n{link}\n\nJeśli nie zlecałeś tej operacji, zignoruj tę wiadomość.");
        await client.SendMailAsync(message, ct);
    }
}
