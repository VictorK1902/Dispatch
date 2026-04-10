using Azure.Messaging.ServiceBus;
using Dispatch.Contracts;
using Dispatch.Data;
using Dispatch.Data.Entities;
using Dispatch.Worker;
using Dispatch.Worker.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Worker.Tests.Functions;

public class WorkerDeadLetterTests : IDisposable
{
    private readonly DispatchDbContext _db;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ServiceBusMessageActions> _messageActionsMock;
    private readonly WorkerDeadLetter _wdl;

    public WorkerDeadLetterTests()
    {
        var options = new DbContextOptionsBuilder<DispatchDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new DispatchDbContext(options);

        _emailServiceMock = new Mock<IEmailService>();
        _messageActionsMock = new Mock<ServiceBusMessageActions>();
        var loggerMock = new Mock<ILogger<WorkerDeadLetter>>();

        _wdl = new WorkerDeadLetter(_emailServiceMock.Object, _db, loggerMock.Object);
    }

    public void Dispose() => _db.Dispose();

    private static ServiceBusReceivedMessage CreateMessage(string correlationId)
    {
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            correlationId: correlationId);
    }

    private Job SeedJob(Guid id)
    {
        var job = new Job
        {
            Id = id,
            ClientId = "test-client",
            JobModuleId = JobModuleTypes.WeatherReport,
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
    public async Task Run_HappyPath_SendsEmailAndUpdatesJob()
    {
        var jobId = Guid.NewGuid();
        SeedJob(jobId);
        var message = CreateMessage(jobId.ToString());

        _emailServiceMock.Setup(e => e.SendToAdminAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("acs-dlq-123");

        await _wdl.Run(message, _messageActionsMock.Object, CancellationToken.None);

        var job = await _db.Jobs.FindAsync(jobId);
        Assert.Equal(JobStatus.Failed, job!.Status);
        Assert.Equal("acs-dlq-123", job.AcsMessageId);
        _emailServiceMock.Verify(e => e.SendToAdminAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _messageActionsMock.Verify(m => m.CompleteMessageAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_EmailFails_StillUpdatesJobAndCompletes()
    {
        var jobId = Guid.NewGuid();
        SeedJob(jobId);
        var message = CreateMessage(jobId.ToString());

        _emailServiceMock.Setup(e => e.SendToAdminAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Email failure"));

        await _wdl.Run(message, _messageActionsMock.Object, CancellationToken.None);

        var job = await _db.Jobs.FindAsync(jobId);
        Assert.Equal(JobStatus.Failed, job!.Status);
        Assert.Null(job.AcsMessageId);
        _messageActionsMock.Verify(m => m.CompleteMessageAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_InvalidCorrelationId_CompletesMessage()
    {
        var message = CreateMessage("not-a-guid");

        _emailServiceMock.Setup(e => e.SendToAdminAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("acs-id");

        await _wdl.Run(message, _messageActionsMock.Object, CancellationToken.None);

        _messageActionsMock.Verify(m => m.CompleteMessageAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_JobNotFound_CompletesMessage()
    {
        var message = CreateMessage(Guid.NewGuid().ToString());

        _emailServiceMock.Setup(e => e.SendToAdminAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("acs-id");

        await _wdl.Run(message, _messageActionsMock.Object, CancellationToken.None);

        _messageActionsMock.Verify(m => m.CompleteMessageAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
