using Dispatch.Data.Entities;

namespace Dispatch.Worker.Interfaces;

public interface IJobModuleHandler
{
    int JobModuleId { get; }

    /// <summary>
    /// Run the job and return AcsMessageId
    /// </summary>
    Task<string> ExecuteAsync(Job job, CancellationToken cancellationToken);
}
