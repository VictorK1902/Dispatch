using Azure.Communication.Email;
using Dispatch.Worker.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dispatch.Worker.Services;

public class EmailServiceOptions
{
    public string SenderAddress { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
}

public class EmailService : IEmailService
{
    private readonly EmailClient _emailClient;
    private readonly string _senderAddress;
    private readonly string _adminEmail;

    private readonly ILogger<EmailService> _logger;

    public EmailService(EmailClient emailClient, IOptions<EmailServiceOptions> options, ILogger<EmailService> logger)
    {
        _emailClient = emailClient;
        _senderAddress = options.Value.SenderAddress;
        _adminEmail = options.Value.AdminEmail;
        _logger = logger;
    }

    public async Task<string> SendAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken)
    {
        var emailMessage = new EmailMessage(
            senderAddress: _senderAddress,
            recipientAddress: recipientEmail,
            content: new EmailContent(subject) { Html = body });

        var operation = await _emailClient.SendAsync(Azure.WaitUntil.Completed, emailMessage, cancellationToken);        

        _logger.LogInformation("Email sent to {Recipient}, MessageId: {MessageId}", recipientEmail, operation.Id);

        return operation.Id;
    }
    
    public async Task<string> SendAsyncWithImageAttachment(string recipientEmail, string subject, string body, byte[] image, string imageName, string imageType, CancellationToken cancellationToken)
    {
        var emailAttachment = new EmailAttachment(imageName, imageType, BinaryData.FromBytes(image));
        var emailMessage = new EmailMessage(
            senderAddress: _senderAddress,
            recipientAddress: recipientEmail,
            content: new EmailContent(subject) { Html = body });
        emailMessage.Attachments.Add(emailAttachment);

        var operation = await _emailClient.SendAsync(Azure.WaitUntil.Completed, emailMessage, cancellationToken);        

        _logger.LogInformation("Email sent to {Recipient}, MessageId: {MessageId}", recipientEmail, operation.Id);

        return operation.Id;
    }

    public Task<string> SendToAdminAsync(string subject, string body, CancellationToken cancellationToken)
    {
        return SendAsync(_adminEmail, subject, body, cancellationToken);
    }
}
