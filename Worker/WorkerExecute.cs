using Azure.Messaging.ServiceBus;
using Dispatch.Contracts;
using Dispatch.Data;
using Dispatch.Worker.Interfaces;
using Dispatch.Worker.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dispatch.Worker;

public class WorkerExecute
{
    private readonly ILogger<WorkerExecute> _logger;
    private readonly DispatchDbContext _db;
    private readonly IEnumerable<IJobModuleHandler> _handlers;

    public WorkerExecute(ILogger<WorkerExecute> logger, DispatchDbContext db, IEnumerable<IJobModuleHandler> handlers)
    {
        _logger = logger;
        _db = db;
        _handlers = handlers;
    }

    [Function(nameof(WorkerExecute))]
    public async Task Run(
        [ServiceBusTrigger("jobs-queue", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing message with SequenceNumber: {SequenceNumber}, CorrelationId: {CorrelationId}",
            message.SequenceNumber, message.CorrelationId);

        // CorrelationId maps to jobId
        if (!Guid.TryParse(message.CorrelationId.ToString(), out var jobId))
        {
            _logger.LogError("Unable to parse CorrelationId into Job ID (GUID)");
            // Move it to DLQ
            await messageActions.DeadLetterMessageAsync(message);
            return;
        }

        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
        if (job is null)
        {
            _logger.LogError("Job {JobId} not found in database", jobId);
            // Move it to DLQ
            await messageActions.DeadLetterMessageAsync(message);
            return;
        }

        var handler = _handlers.FirstOrDefault(h => h.JobModuleId == job.JobModuleId);
        if (handler is null)
        {
            _logger.LogError("No handler registered for JobModuleId {ModuleId}", job.JobModuleId);
            job.Status = JobStatus.Failed;
            await _db.SaveChangesAsync();
            // Move it to DLQ
            await messageActions.DeadLetterMessageAsync(message);
            return;
        }

        string acsMessageId;
        try
        {
            acsMessageId = await handler.ExecuteAsync(job, cancellationToken);
        }
        catch (ExternalApiException ex)
        {
            // Catching ExternalApiException specifically
            // Other exception types should still cause the message to be re-delivered.
            _logger.LogError(ex, "Job {JobId} failed due to an API error: {Message}", jobId, ex.Message);
            job.Status = JobStatus.Failed;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: ex.Message);
            return;
        }
        // Success otherwise
        job.Status = JobStatus.Completed;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        job.AcsMessageId = acsMessageId;
        await _db.SaveChangesAsync();
        await messageActions.CompleteMessageAsync(message);

        _logger.LogInformation("Job {JobId} completed successfully", jobId);
    }
}
