using System.Text.Json;
using Dispatch.Data.Entities;

namespace Dispatch.Api.Services;

public interface IJobService
{
    Task<Job> CreateAsync(string clientId, int jobModuleId, DateTimeOffset scheduledAtUtc, string dataPayload, CancellationToken cancellationToken = default);
    Task<Job?> GetAsync(Guid id, string clientId, CancellationToken cancellationToken = default);
    Task<Job> UpdateAsync(Job job, DateTimeOffset scheduledAtUtc, string dataPayload, CancellationToken cancellationToken = default);
    Task CancelAsync(Job job, CancellationToken cancellationToken = default);
    bool IsWithinModificationThreshold(DateTimeOffset time);
    string? ValidateJobModule(int jobModuleId, JsonElement data);
}
