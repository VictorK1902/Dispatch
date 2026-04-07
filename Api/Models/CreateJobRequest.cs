using System.Text.Json;

namespace Dispatch.Api.Models;

public class CreateJobRequest
{
    public int JobModuleId { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public JsonElement Data { get; set; }
}
