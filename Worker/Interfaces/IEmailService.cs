namespace Dispatch.Worker.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Send plain text email
    /// </summary>
    Task<string> SendAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken);

    /// <summary>
    /// Send email with image attachment (image/png, image/jpeg or image/jpg)
    /// </summary>
    Task<string> SendAsyncWithImageAttachment(string recipientEmail, string subject, string body, byte[] image, string imageName, string imageType, CancellationToken cancellationToken);

    /// <summary>
    /// Send plain text email to admin
    /// </summary>
    Task<string> SendToAdminAsync(string subject, string body, CancellationToken cancellationToken);
}
