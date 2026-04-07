using System.ComponentModel.DataAnnotations;

namespace Dispatch.Data.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }

    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(200)]
    public string RecipientEmail { get; set; } = string.Empty;

    public DateTimeOffset SentAt { get; set; }

    [MaxLength(200)]
    public string AcsMessageId { get; set; } = string.Empty;

    public Job Job { get; set; } = null!;
}
