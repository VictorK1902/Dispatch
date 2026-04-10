using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Dispatch.Api.Services;
using Dispatch.Contracts;
using Dispatch.Data;
using Dispatch.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Api.Tests.Services;

public class JobServiceTests : IDisposable
{
    private readonly DispatchDbContext _db;
    private readonly Mock<ServiceBusSender> _senderMock;
    private readonly JobService _service;

    private const string ClientId = "test-client-id";
    private readonly DateTime baseScheduledDate = new DateTime(2026, 4, 20);
    private readonly string weatherReportInput = JsonSerializer.Serialize(new
        {
            sendTo = "test@test.com",
            latitude = 0,
            longitude = 0,
            forecastDays = 1,
            day = new DateTime(2026, 4, 20)
        });

    public JobServiceTests()
    {
        var options = new DbContextOptionsBuilder<DispatchDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DispatchDbContext(options);
        _senderMock = new Mock<ServiceBusSender>();
        var loggerMock = new Mock<ILogger<JobService>>();

        _service = new JobService(_db, _senderMock.Object, loggerMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private static JsonElement ToJsonElement(object obj)
    {
        return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(obj));
    }

    private static void JobsAreEqual(Job left, Job right)
    {
        Assert.Equal(left.Id, right.Id);
        Assert.Equal(left.ClientId, right.ClientId);
        Assert.Equal(left.JobModuleId, right.JobModuleId);
        Assert.Equal(left.Status, right.Status);
        Assert.Equal(left.ScheduledAt, right.ScheduledAt);
        Assert.Equal(left.DataPayload, right.DataPayload);
        Assert.Equal(left.CreatedAt, right.CreatedAt);
        Assert.Equal(left.UpdatedAt, right.UpdatedAt);
    }

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_HappyPath_CreatesJobAndSchedulesMessage()
    {
        _senderMock.Setup(s => s.ScheduleMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42L);

        var scheduledAt = baseScheduledDate.AddHours(1);
        var dataPayload = weatherReportInput;

        var job = await _service.CreateAsync(ClientId, JobModuleTypes.WeatherReport, scheduledAt, dataPayload);

        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Equal(42L, job.ServiceBusSequenceNumber);
        Assert.Equal(ClientId, job.ClientId);
        Assert.Equal(dataPayload, job.DataPayload);
        Assert.Equal(JobModuleTypes.WeatherReport, job.JobModuleId);
        Assert.Equal(scheduledAt, job.ScheduledAt);

        var saved = await _db.Jobs.FindAsync(job.Id);
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task CreateAsync_ServiceBusFailure_SetsStatusToFailedAndThrows()
    {
        _senderMock.Setup(s => s.ScheduleMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceBusException("Connection failed", ServiceBusFailureReason.ServiceBusy));

        await Assert.ThrowsAsync<ServiceBusException>(() =>
            _service.CreateAsync(ClientId, JobModuleTypes.WeatherReport, baseScheduledDate.AddHours(1), weatherReportInput));

        // This is the partial failure case where the job is saved but message queue fails to deliver.        
        var job = await _db.Jobs.FirstAsync();
        Assert.Equal(JobStatus.Failed, job.Status);
    }

    #endregion

    #region GetAsync

    [Fact]
    public async Task GetAsync_Found_ReturnsJob()
    {
        var now = DateTimeOffset.UtcNow;
        var job = new Job
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            JobModuleId = JobModuleTypes.WeatherReport,
            Status = JobStatus.Scheduled,
            ScheduledAt = baseScheduledDate.AddHours(1),
            DataPayload = "{}",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        var result = await _service.GetAsync(job.Id, ClientId);

        Assert.NotNull(result);
        JobsAreEqual(job, result);
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        var result = await _service.GetAsync(Guid.NewGuid(), ClientId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WrongClientId_ReturnsNull()
    {
        var now = DateTimeOffset.UtcNow;
        var job = new Job
        {
            Id = Guid.NewGuid(),
            ClientId = "other-client-id",
            JobModuleId = JobModuleTypes.WeatherReport,
            Status = JobStatus.Scheduled,
            ScheduledAt = baseScheduledDate.AddHours(1),
            DataPayload = weatherReportInput,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        var result = await _service.GetAsync(job.Id, ClientId);

        Assert.Null(result);
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_HappyPath_CancelsOldAndSchedulesNew()
    {
        var now = DateTimeOffset.UtcNow;
        var job = new Job
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            JobModuleId = JobModuleTypes.WeatherReport,
            Status = JobStatus.Scheduled,
            ScheduledAt = baseScheduledDate.AddHours(1),
            DataPayload = weatherReportInput,
            ServiceBusSequenceNumber = 10L,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        _senderMock.Setup(s => s.CancelScheduledMessageAsync(10L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _senderMock.Setup(s => s.ScheduleMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(99L);

        var newScheduledAt = baseScheduledDate.AddHours(3);
        var newPayload = JsonSerializer.Serialize(new
        {
            sendTo = "updated@test.com",
            latitude = 10,
            longitude = 20,
            forecastDays = 3,
            day = new DateTime(2026, 4, 21)
        });

        var result = await _service.UpdateAsync(job, newScheduledAt, newPayload);

        Assert.Equal(newScheduledAt, result.ScheduledAt);
        Assert.Equal(newPayload, result.DataPayload);
        Assert.Equal(99L, result.ServiceBusSequenceNumber);
        _senderMock.Verify(s => s.CancelScheduledMessageAsync(10L, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_CancelFailure_SetsFailedAndThrows()
    {
        var now = DateTimeOffset.UtcNow;
        var job = new Job
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            JobModuleId = JobModuleTypes.WeatherReport,
            Status = JobStatus.Scheduled,
            ScheduledAt = baseScheduledDate.AddHours(1),
            DataPayload = weatherReportInput,
            ServiceBusSequenceNumber = 10L,
            CreatedAt = now,
            UpdatedAt = now
        };
        var newPayload = JsonSerializer.Serialize(new
        {
            sendTo = "updated@test.com",
            latitude = 10,
            longitude = 20,
            forecastDays = 3,
            day = new DateTime(2026, 4, 21)
        });
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        _senderMock.Setup(s => s.CancelScheduledMessageAsync(10L, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceBusException("fail", ServiceBusFailureReason.ServiceBusy));

        await Assert.ThrowsAsync<ServiceBusException>(() =>
            _service.UpdateAsync(job, baseScheduledDate.AddHours(3), newPayload));

        Assert.Equal(JobStatus.Failed, job.Status);
    }

    [Fact]
    public async Task UpdateAsync_RescheduleFailure_SetsFailedAndThrows()
    {
        var now = DateTimeOffset.UtcNow;
        var job = new Job
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            JobModuleId = JobModuleTypes.WeatherReport,
            Status = JobStatus.Scheduled,
            ScheduledAt = baseScheduledDate.AddHours(1),
            DataPayload = weatherReportInput,
            ServiceBusSequenceNumber = 10L,
            CreatedAt = now,
            UpdatedAt = now
        };
        var newPayload = JsonSerializer.Serialize(new
        {
            sendTo = "updated@test.com",
            latitude = 10,
            longitude = 20,
            forecastDays = 3,
            day = new DateTime(2026, 4, 21)
        });
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        _senderMock.Setup(s => s.CancelScheduledMessageAsync(10L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _senderMock.Setup(s => s.ScheduleMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceBusException("fail", ServiceBusFailureReason.ServiceBusy));

        await Assert.ThrowsAsync<ServiceBusException>(() =>
            _service.UpdateAsync(job, baseScheduledDate.AddHours(3), newPayload));

        Assert.Equal(JobStatus.Failed, job.Status);
    }

    #endregion

    #region CancelAsync

    [Fact]
    public async Task CancelAsync_HappyPath_SetsCancelledAndCancelsMessage()
    {
        var now = DateTimeOffset.Now;
        var job = new Job
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            JobModuleId = JobModuleTypes.WeatherReport,
            Status = JobStatus.Scheduled,
            ScheduledAt = baseScheduledDate.AddHours(1),
            DataPayload = weatherReportInput,
            ServiceBusSequenceNumber = 10L,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        _senderMock.Setup(s => s.CancelScheduledMessageAsync(10L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.CancelAsync(job);

        Assert.Equal(JobStatus.Cancelled, job.Status);
        _senderMock.Verify(s => s.CancelScheduledMessageAsync(10L, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_MessageCancelFails_StillSetsCancelled()
    {
        var now = DateTimeOffset.UtcNow;
        var job = new Job
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            JobModuleId = JobModuleTypes.WeatherReport,
            Status = JobStatus.Scheduled,
            ScheduledAt = baseScheduledDate.AddHours(1),
            DataPayload = weatherReportInput,
            ServiceBusSequenceNumber = 10L,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        _senderMock.Setup(s => s.CancelScheduledMessageAsync(10L, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceBusException("fail", ServiceBusFailureReason.ServiceBusy));

        await _service.CancelAsync(job);

        Assert.Equal(JobStatus.Cancelled, job.Status);
    }

    #endregion

    #region ValidateJobModule

    [Fact]
    public void ValidateJobModule_WeatherReport_Valid_ReturnsNull()
    {
        var data = ToJsonElement(new { sendTo = "test@test.com", latitude = 0, longitude = 0, forecastDays = 3, day = baseScheduledDate });

        var result = _service.ValidateJobModule(JobModuleTypes.WeatherReport, data);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateJobModule_WeatherReport_MissingSendTo_ReturnsError()
    {
        var data = ToJsonElement(new { latitude = 0, longitude = 0, forecastDays = 3, day = baseScheduledDate });

        var result = _service.ValidateJobModule(JobModuleTypes.WeatherReport, data);

        Assert.NotNull(result);
        Assert.Contains("Invalid data for Weather Report", result);
    }

    [Fact]
    public void ValidateJobModule_WeatherReport_InvalidForecastDays_ReturnsError()
    {
        var data = ToJsonElement(new { sendTo = "test@test.com", latitude = 0, longitude = 0, forecastDays = 5, day = baseScheduledDate });

        var result = _service.ValidateJobModule(JobModuleTypes.WeatherReport, data);

        Assert.NotNull(result);
        Assert.Contains("ForecastDays must be 1, 3, or 7", result);
    }

    [Fact]
    public void ValidateJobModule_WeatherReport_BadJson_ReturnsError()
    {
        var data = ToJsonElement("not an object");

        var result = _service.ValidateJobModule(JobModuleTypes.WeatherReport, data);

        Assert.NotNull(result);
        Assert.Contains("Invalid JSON for Weather Report", result);
    }

    [Fact]
    public void ValidateJobModule_StockPriceReport_Valid_ReturnsNull()
    {
        var data = ToJsonElement(new { symbol = "AAPL", sendTo = "test@test.com" });

        var result = _service.ValidateJobModule(JobModuleTypes.StockPriceReport, data);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateJobModule_StockPriceReport_MissingFields_ReturnsError()
    {
        var data = ToJsonElement(new { symbol = "" });

        var result = _service.ValidateJobModule(JobModuleTypes.StockPriceReport, data);

        Assert.NotNull(result);
        Assert.Contains("Invalid data for Stock Price Report", result);
    }

    [Fact]
    public void ValidateJobModule_StockPriceReport_BadJson_ReturnsError()
    {
        var data = ToJsonElement("not an object");

        var result = _service.ValidateJobModule(JobModuleTypes.StockPriceReport, data);

        Assert.NotNull(result);
        Assert.Contains("Invalid JSON for Stock Price Report", result);
    }

    [Fact]
    public void ValidateJobModule_UnknownModule_ReturnsError()
    {
        var data = ToJsonElement(new { });

        var result = _service.ValidateJobModule(999, data);

        Assert.NotNull(result);
        Assert.Contains("Unknown JobModuleId: 999", result);
    }

    #endregion

    #region IsWithinModificationThreshold

    [Fact]
    public void IsWithinModificationThreshold_TimeWithinThreshold_ReturnsTrue()
    {
        var time = DateTimeOffset.UtcNow.AddSeconds(30);

        var result = _service.IsWithinModificationThreshold(time);

        Assert.True(result);
    }

    [Fact]
    public void IsWithinModificationThreshold_TimeBeyondThreshold_ReturnsFalse()
    {
        var time = DateTimeOffset.UtcNow.AddMinutes(5);

        var result = _service.IsWithinModificationThreshold(time);

        Assert.False(result);
    }

    #endregion
}
