using System.Text.Json;

namespace Dispatch.Api.Models;

public class UpdateJobRequest
{
    public DateTimeOffset ScheduledAt { get; set; }
    public JsonElement Data { get; set; }
}
