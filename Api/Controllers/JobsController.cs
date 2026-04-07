using System.Text.Json;
using Dispatch.Api.Models;
using Dispatch.Api.Services;
using Dispatch.Contracts;
using Dispatch.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dispatch.Api.Controllers;

[ApiController]
[Authorize(Roles = "Jobs.ReadWrite")]
[Route("api/v1/[controller]")]
public class JobsController : ControllerBase
{
    private readonly JobService _jobService;

    public JobsController(JobService jobService)
    {
        _jobService = jobService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobRequest request)
    {
        var clientId = GetClientId();
        if (clientId is null)
            return Problem("Unable to determine client identity.", statusCode: 401);

        var validationError = _jobService.ValidateJobModule(request.JobModuleId, request.Data);
        if (validationError is not null)
            return Problem(validationError, statusCode: 400);

        if (_jobService.IsWithinModificationThreshold(request.ScheduledAt))
            return Problem("ScheduledAt must be at least 10 minutes in the future.", statusCode: 400);

        try
        {
            var job = await _jobService.CreateAsync(clientId, request.JobModuleId, request.ScheduledAt, request.Data.GetRawText());
            return CreatedAtAction(nameof(Get), new { id = job.Id }, ToResponse(job));
        }
        catch
        {
            return Problem("Job was saved but failed to enqueue. Status set to Failed.", statusCode: 500);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var clientId = GetClientId();
        if (clientId is null)
            return Problem("Unable to determine client identity.", statusCode: 401);

        var job = await _jobService.GetAsync(id, clientId);
        if (job is null)
            return NotFound();

        return Ok(ToResponse(job));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateJobRequest request)
    {
        var clientId = GetClientId();
        if (clientId is null)
            return Problem("Unable to determine client identity.", statusCode: 401);

        var job = await _jobService.GetAsync(id, clientId);
        if (job is null)
            return NotFound();

        if (job.Status != JobStatus.Scheduled)
            return Problem("Only jobs with status 'Scheduled' can be modified.", statusCode: 409);

        if (_jobService.IsWithinModificationThreshold(job.ScheduledAt))
            return Problem("Cannot modify a job within 10 minutes of its scheduled execution.", statusCode: 409);

        var validationError = _jobService.ValidateJobModule(job.JobModuleId, request.Data);
        if (validationError is not null)
            return Problem(validationError, statusCode: 400);

        if (_jobService.IsWithinModificationThreshold(request.ScheduledAt))
            return Problem("ScheduledAt must be at least 10 minutes in the future.", statusCode: 400);

        try
        {
            var updated = await _jobService.UpdateAsync(job, request.ScheduledAt, request.Data.GetRawText());
            return Ok(ToResponse(updated));
        }
        catch
        {
            return Problem("Failed to update the scheduled job. Status set to Failed.", statusCode: 500);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var clientId = GetClientId();
        if (clientId is null)
            return Problem("Unable to determine client identity.", statusCode: 401);

        var job = await _jobService.GetAsync(id, clientId);
        if (job is null)
            return NotFound();

        if (job.Status != JobStatus.Scheduled)
            return Problem("Only jobs with status 'Scheduled' can be cancelled.", statusCode: 409);

        if (_jobService.IsWithinModificationThreshold(job.ScheduledAt))
            return Problem("Cannot cancel a job within 10 minutes of its scheduled execution.", statusCode: 409);

        await _jobService.CancelAsync(job);
        return NoContent();
    }

    #region Helpers
    private string? GetClientId()
    {
        return User.FindFirst("appid")?.Value
            ?? User.FindFirst("azp")?.Value;
    }

    private static JobResponse ToResponse(Job job) => new()
    {
        Id = job.Id,
        JobModuleId = job.JobModuleId,
        Status = job.Status,
        ScheduledAt = job.ScheduledAt,
        Data = JsonSerializer.Deserialize<JsonElement>(job.DataPayload),
        CreatedAt = job.CreatedAt,
        UpdatedAt = job.UpdatedAt
    };
    #endregion    
}
