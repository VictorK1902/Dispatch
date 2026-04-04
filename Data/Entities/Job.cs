using System.ComponentModel.DataAnnotations;

namespace Dispatch.Data.Entities;

public class Job
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string ClientId { get; set; } = string.Empty;

    public int JobModuleId { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Scheduled";

    public DateTime ScheduledAt { get; set; }
    public string DataPayload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long? ServiceBusSequenceNumber { get; set; }

    public JobModule JobModule { get; set; } = null!;
}
