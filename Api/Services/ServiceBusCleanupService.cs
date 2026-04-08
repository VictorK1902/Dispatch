using Azure.Messaging.ServiceBus;

namespace Dispatch.Api.Services;

public class ServiceBusCleanupService(
    ServiceBusSender sender,
    ServiceBusClient client) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await sender.DisposeAsync();
        await client.DisposeAsync();
    }
}
