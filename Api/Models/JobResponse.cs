using System.Text.Json;

namespace Dispatch.Api.Models;

public class JobResponse
{
    public Guid Id { get; set; }
    public int JobModuleId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset ScheduledAt { get; set; }
    public JsonElement Data { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
