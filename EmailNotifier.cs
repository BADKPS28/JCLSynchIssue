using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace FileWatcherService;

public interface IEmailNotifier
{
    Task SendMissingFilesReportAsync(List<string> missingFiles, CancellationToken cancellationToken);
}

public class EmailNotifier : IEmailNotifier
{
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<EmailNotifier> _logger;
    private readonly GraphServiceClient _graphClient;

    public EmailNotifier(
        IOptions<EmailOptions> emailOptions,
        IOptions<SharePointOptions> sharePointOptions,
        ILogger<EmailNotifier> logger)
    {
        _emailOptions = emailOptions.Value;
        _logger = logger;

        var credential = new ClientSecretCredential(
            sharePointOptions.Value.TenantId,
            sharePointOptions.Value.ClientId,
            sharePointOptions.Value.ClientSecret);

        _graphClient = new GraphServiceClient(credential);
    }

    public async Task SendMissingFilesReportAsync(List<string> missingFiles, CancellationToken cancellationToken)
    {
        try
        {
            var subject = $"FileWatcher Alert: {missingFiles.Count} missing file(s) – {DateTime.Now:MM/dd/yyyy}";

            var body = BuildEmailBody(missingFiles);

            var recipients = _emailOptions.Recipients
                .Select(r => new Recipient { EmailAddress = new EmailAddress { Address = r } })
                .ToList();

            var requestBody = new SendMailPostRequestBody
            {
                Message = new Message
                {
                    Subject = subject,
                    Body = new ItemBody { ContentType = BodyType.Text, Content = body },
                    ToRecipients = recipients
                },
                SaveToSentItems = false
            };

            await _graphClient.Users[_emailOptions.SenderAddress]
                .SendMail
                .PostAsync(requestBody, cancellationToken: cancellationToken);

            _logger.LogInformation("Email notification sent to {Count} recipient(s).", _emailOptions.Recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification.");
        }
    }

    private static string BuildEmailBody(List<string> missingFiles)
    {
        var lines = new List<string>
        {
            $"FileWatcher detected missing file(s) on {DateTime.Now:MM/dd/yyyy HH:mm:ss}.",
            ""
        };

        foreach (var f in missingFiles)
            lines.Add($"  - {f}");

        lines.Add("");
        lines.Add("Please verify the files are available on SharePoint.");

        return string.Join(Environment.NewLine, lines);
    }
}
