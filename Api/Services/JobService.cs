using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Dispatch.Contracts;
using Dispatch.Contracts.JobModules;
using Dispatch.Data;
using Dispatch.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Services;

public class JobService
{
    private static readonly TimeSpan ModificationThreshold = TimeSpan.FromMinutes(10);

    private readonly DispatchDbContext _db;
    private readonly ServiceBusSender _sender;
    private readonly ILogger<JobService> _logger;

    public JobService(DispatchDbContext db, ServiceBusSender sender, ILogger<JobService> logger)
    {
        _db = db;
        _sender = sender;
        _logger = logger;
    }

    public async Task<Job> CreateAsync(string clientId, int jobModuleId, DateTimeOffset scheduledAtUtc, string dataPayload)
    {
        var now = DateTimeOffset.UtcNow;
        var job = new Job
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            JobModuleId = jobModuleId,
            Status = JobStatus.Scheduled,
            ScheduledAt = scheduledAtUtc,
            DataPayload = dataPayload,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        try
        {
            job.ServiceBusSequenceNumber = await ScheduleMessageAsync(job.Id, job.ScheduledAt);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule Service Bus message for job {JobId}", job.Id);
            job.Status = JobStatus.Failed;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            throw;
        }

        return job;
    }

    public Task<Job?> GetAsync(Guid id, string clientId)
    {
        return _db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.ClientId == clientId);
    }

    public async Task<Job> UpdateAsync(Job job, DateTimeOffset scheduledAtUtc, string dataPayload)
    {
        if (job.ServiceBusSequenceNumber.HasValue)
        {
            try
            {
                await _sender.CancelScheduledMessageAsync(job.ServiceBusSequenceNumber.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel scheduled message {SequenceNumber} for job {JobId}", job.ServiceBusSequenceNumber, job.Id);
                job.Status = JobStatus.Failed;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync();
                throw;
            }
        }

        job.ScheduledAt = scheduledAtUtc;
        job.DataPayload = dataPayload;
        job.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            job.ServiceBusSequenceNumber = await ScheduleMessageAsync(job.Id, job.ScheduledAt);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reschedule Service Bus message for job {JobId}", job.Id);
            job.Status = JobStatus.Failed;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            throw;
        }

        return job;
    }

    public async Task CancelAsync(Job job)
    {
        if (job.ServiceBusSequenceNumber.HasValue)
        {
            try
            {
                await _sender.CancelScheduledMessageAsync(job.ServiceBusSequenceNumber.Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cancel scheduled message {SequenceNumber} for job {JobId}, proceeding with cancellation", job.ServiceBusSequenceNumber, job.Id);
            }
        }

        job.Status = JobStatus.Cancelled;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public bool IsWithinModificationThreshold(DateTimeOffset time)
    {
        return time <= DateTimeOffset.UtcNow.Add(ModificationThreshold);
    }

    public string? ValidateJobModule(int jobModuleId, JsonElement data)
    {
        switch (jobModuleId)
        {
            case JobModuleTypes.WeatherReport:
                try
                {
                    var weather = JsonSerializer.Deserialize<WeatherReportInput>(data.GetRawText());
                    if (weather is null || string.IsNullOrWhiteSpace(weather.SendTo))
                        return "Invalid data for Weather Report module.";
                    if (weather.ForecastDays is not (1 or 3 or 7))
                        return "ForecastDays must be 1, 3, or 7.";
                }
                catch (JsonException)
                {
                    return "Invalid JSON for Weather Report module.";
                }
                break;

            case JobModuleTypes.StockPriceReport:
                try
                {
                    var stock = JsonSerializer.Deserialize<StockPriceReportInput>(data.GetRawText());
                    if (stock is null || string.IsNullOrWhiteSpace(stock.Symbol) || string.IsNullOrWhiteSpace(stock.SendTo))
                        return "Invalid data for Stock Price Report module.";
                }
                catch (JsonException)
                {
                    return "Invalid JSON for Stock Price Report module.";
                }
                break;

            default:
                return $"Unknown JobModuleId: {jobModuleId}.";
        }

        return null;
    }

    private async Task<long> ScheduleMessageAsync(Guid jobId, DateTimeOffset scheduledAt)
    {
        var message = new ServiceBusMessage(jobId.ToString())
        {
            CorrelationId = jobId.ToString()
        };
        return await _sender.ScheduleMessageAsync(message, scheduledAt);
    }
}
