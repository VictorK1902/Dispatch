using Azure.Messaging.ServiceBus;
using Dispatch.Contracts;
using Dispatch.Data;
using Dispatch.Data.Entities;
using Dispatch.Worker;
using Dispatch.Worker.Interfaces;
using Dispatch.Worker.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Worker.Tests.Functions;

public class WorkerExecuteTests : IDisposable
{
    private readonly DispatchDbContext _db;
    private readonly Mock<ServiceBusMessageActions> _messageActionsMock;
    private readonly Mock<IJobModuleHandler> _handlerMock;
    private readonly WorkerExecute _we;

    private const int TestModuleId = JobModuleTypes.WeatherReport;

    public WorkerExecuteTests()
    {
        var options = new DbContextOptionsBuilder<DispatchDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new DispatchDbContext(options);

        _messageActionsMock = new Mock<ServiceBusMessageActions>();
        _handlerMock = new Mock<IJobModuleHandler>();
        _handlerMock.Setup(h => h.JobModuleId).Returns(TestModuleId);

        var loggerMock = new Mock<ILogger<WorkerExecute>>();
        _we = new WorkerExecute(loggerMock.Object, _db, new[] { _handlerMock.Object });
    }

    public void Dispose() => _db.Dispose();

    private static ServiceBusReceivedMessage CreateMessage(string correlationId)
    {
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            correlationId: correlationId);
    }

    private Job SeedJob(Guid id, int moduleId = TestModuleId)
    {
        var job = new Job
        {
            Id = id,
            ClientId = "test-client",
            JobModuleId = moduleId,
            Status = JobStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow,
            DataPayload = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Jobs.Add(job);
        _db.SaveChanges();
        return job;
    }

    #region Run

    [Fact]
    public async Task Run_HappyPath_CompletesJobAndMessage()
    {
        var jobId = Guid.NewGuid();
        SeedJob(jobId);
        var message = CreateMessage(jobId.ToString());

        _handlerMock.Setup(h => h.ExecuteAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("acs-msg-123");

        await _we.Run(message, _messageActionsMock.Object, CancellationToken.None);

        var job = await _db.Jobs.FindAsync(jobId);
        Assert.Equal(JobStatus.Completed, job!.Status);
        Assert.Equal("acs-msg-123", job.AcsMessageId);
        _messageActionsMock.Verify(m => m.CompleteMessageAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_InvalidCorrelationId_DeadLetter()
    {
        var message = CreateMessage("not-a-guid");

        await _we.Run(message, _messageActionsMock.Object, CancellationToken.None);

        _messageActionsMock.Verify(m => m.DeadLetterMessageAsync(message, It.IsAny<Dictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_JobNotFound_DeadLetter()
    {
        var message = CreateMessage(Guid.NewGuid().ToString());

        await _we.Run(message, _messageActionsMock.Object, CancellationToken.None);

        _messageActionsMock.Verify(m => m.DeadLetterMessageAsync(message, It.IsAny<Dictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_NoHandlerForModule_DeadLetter()
    {
        var jobId = Guid.NewGuid();
        SeedJob(jobId, moduleId: 999);
        var message = CreateMessage(jobId.ToString());

        await _we.Run(message, _messageActionsMock.Object, CancellationToken.None);

        var job = await _db.Jobs.FindAsync(jobId);
        _messageActionsMock.Verify(m => m.DeadLetterMessageAsync(message, It.IsAny<Dictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_HandlerThrowsExternalApiException_DeadLetter()
    {
        var jobId = Guid.NewGuid();
        SeedJob(jobId);
        var message = CreateMessage(jobId.ToString());

        _handlerMock.Setup(h => h.ExecuteAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalApiException("API down"));

        await _we.Run(message, _messageActionsMock.Object, CancellationToken.None);

        var job = await _db.Jobs.FindAsync(jobId);
        _messageActionsMock.Verify(m => m.DeadLetterMessageAsync(message, It.IsAny<Dictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_HandlerThrowsOtherException_BubblesUp()
    {
        var jobId = Guid.NewGuid();
        SeedJob(jobId);
        var message = CreateMessage(jobId.ToString());

        _handlerMock.Setup(h => h.ExecuteAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unexpected"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _we.Run(message, _messageActionsMock.Object, CancellationToken.None));
    }

    #endregion
}
