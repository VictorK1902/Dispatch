using Azure.Messaging.ServiceBus;
using Dispatch.Data;
using Dispatch.Worker.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Dispatch.Contracts;

namespace Dispatch.Worker;

public class WorkerDeadLetter
{
    private readonly ILogger<WorkerDeadLetter> _logger;
    private readonly DispatchDbContext _db;
    private readonly IEmailService _emailService;

    public WorkerDeadLetter(IEmailService emailService, DispatchDbContext db, ILogger<WorkerDeadLetter> logger)
    {
        _emailService = emailService;
        _db = db;
        _logger = logger;
    }

    [Function(nameof(WorkerDeadLetter))]
    public async Task Run(
        [ServiceBusTrigger("jobs-queue/$deadletterqueue", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
        // Send email first (best-effort — don't let email failure prevent DB update)
        string emailBody = $"Dead-lettered message — MessageId: {message.MessageId}, CorrelationId: {message.CorrelationId}, Reason: {message.DeadLetterReason}, Description: {message.DeadLetterErrorDescription}";
        string? acsMessageId = null;
        try
        {
            acsMessageId = await _emailService.SendToAdminAsync("DLQ", emailBody, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send DLQ notification email for MessageId: {MessageId}", message.MessageId);
        }
        _logger.LogWarning("Dead-lettered message — MessageId: {MessageId}, CorrelationId: {CorrelationId}, AcsMessageId {AcsMessageId}, Reason: {DeadLetterReason}, Description: {DeadLetterErrorDescription}",
        message.MessageId, message.CorrelationId, acsMessageId, message.DeadLetterReason, message.DeadLetterErrorDescription);
        
        // Now try updating job status in db
        // CorrelationId maps to jobId
        if (!Guid.TryParse(message.CorrelationId.ToString(), out var jobId))
        {
            _logger.LogError("Unable to parse CorrelationId into Job ID (GUID)");
            // Just mark it done
            await messageActions.CompleteMessageAsync(message);
            return;
        }
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
        if (job is null)
        {
            _logger.LogError("Job {JobId} not found in database", jobId);
            // Just mark it done
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        job.Status = JobStatus.Failed;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        job.AcsMessageId = acsMessageId;
        await _db.SaveChangesAsync();
        await messageActions.CompleteMessageAsync(message);
    }
}
