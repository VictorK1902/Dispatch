using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace OceanLight.Function;

public class WorkerExecute
{
    private readonly ILogger<WorkerExecute> _logger;

    public WorkerExecute(ILogger<WorkerExecute> logger)
    {
        _logger = logger;
    }

    [Function(nameof(WorkerExecute))]
    public async Task Run(
        [ServiceBusTrigger("jobs-queue", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Message MessageId: {MessageId}", message.MessageId);
        _logger.LogInformation("Message ScheduledEnqueueTime: {scheduledTime}", message.ScheduledEnqueueTime.ToString("g"));
        _logger.LogInformation("Message CorrelationId: {CorrelationId}", message.CorrelationId);

        // Complete the message
        await messageActions.CompleteMessageAsync(message);
    }
}