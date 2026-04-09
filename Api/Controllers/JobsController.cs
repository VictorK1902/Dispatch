using System.Text.Json;
using Dispatch.Api.Models;
using Dispatch.Api.Services;
using Dispatch.Contracts;
using Dispatch.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dispatch.Api.Controllers;

[ApiController]
[Authorize(Roles = "Job.ReadWrite")]
[Route("api/v1/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IJobService _jobService;

    public JobsController(IJobService jobService)
    {
        _jobService = jobService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobRequest request, CancellationToken cancellationToken)
    {
        var clientId = GetClientId();
        if (clientId is null)
            return Problem("Unable to determine client identity.", statusCode: 401);

        var validationError = _jobService.ValidateJobModule(request.JobModuleId, request.Data);
        if (validationError is not null)
            return Problem(validationError, statusCode: 400);

        var job = await _jobService.CreateAsync(clientId, request.JobModuleId, request.ScheduledAt, request.Data.GetRawText(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = job.Id }, ToResponse(job));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var clientId = GetClientId();
        if (clientId is null)
            return Problem("Unable to determine client identity.", statusCode: 401);

        var job = await _jobService.GetAsync(id, clientId, cancellationToken);
        if (job is null)
            return NotFound();

        if (job.ClientId != clientId)
            return Problem("Cannot view jobs from other clients.", statusCode: 403);

        return Ok(ToResponse(job));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateJobRequest request, CancellationToken cancellationToken)
    {
        var clientId = GetClientId();
        if (clientId is null)
            return Problem("Unable to determine client identity.", statusCode: 401);

        var job = await _jobService.GetAsync(id, clientId, cancellationToken);
        if (job is null)
            return NotFound();

        if (job.ClientId != clientId)
            return Problem("Cannot update jobs from other clients.", statusCode: 403);

        if (job.Status != JobStatus.Scheduled)
            return Problem("Only jobs with status 'Scheduled' can be modified.", statusCode: 409);

        if (_jobService.IsWithinModificationThreshold(job.ScheduledAt))
            return Problem("Cannot modify a job within 1 minute of its scheduled execution.", statusCode: 409);

        var validationError = _jobService.ValidateJobModule(job.JobModuleId, request.Data);
        if (validationError is not null)
            return Problem(validationError, statusCode: 400);

        if (_jobService.IsWithinModificationThreshold(request.ScheduledAt))
            return Problem("Updated ScheduledAt must be at least 1 minute in the future.", statusCode: 400);

        var updated = await _jobService.UpdateAsync(job, request.ScheduledAt, request.Data.GetRawText(), cancellationToken);
        return Ok(ToResponse(updated));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(Guid id, [FromBody] PatchJobRequest request, CancellationToken cancellationToken)
    {
        var clientId = GetClientId();
        if (clientId is null)
            return Problem("Unable to determine client identity.", statusCode: 401);

        if (request.ScheduledAt is null && request.Data is null)
            return Problem("At least one of 'ScheduledAt' or 'Data' must be provided.", statusCode: 400);

        var job = await _jobService.GetAsync(id, clientId, cancellationToken);
        if (job is null)
            return NotFound();

        if (job.ClientId != clientId)
            return Problem("Cannot update jobs from other clients.", statusCode: 403);

        if (job.Status != JobStatus.Scheduled)
            return Problem("Only jobs with status 'Scheduled' can be modified.", statusCode: 409);

        if (_jobService.IsWithinModificationThreshold(job.ScheduledAt))
            return Problem("Cannot modify a job within 1 minute of its scheduled execution.", statusCode: 409);

        var newScheduledAt = request.ScheduledAt ?? job.ScheduledAt;
        var newDataPayload = request.Data is not null ? request.Data.Value.GetRawText() : job.DataPayload;

        if (request.Data is not null)
        {
            var validationError = _jobService.ValidateJobModule(job.JobModuleId, request.Data.Value);
            if (validationError is not null)
                return Problem(validationError, statusCode: 400);
        }

        if (request.ScheduledAt is not null && _jobService.IsWithinModificationThreshold(request.ScheduledAt.Value))
            return Problem("Updated ScheduledAt must be at least 1 minute in the future.", statusCode: 400);

        var updated = await _jobService.UpdateAsync(job, newScheduledAt, newDataPayload, cancellationToken);
        return Ok(ToResponse(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var clientId = GetClientId();
        if (clientId is null)
            return Problem("Unable to determine client identity.", statusCode: 401);

        var job = await _jobService.GetAsync(id, clientId, cancellationToken);
        if (job is null)
            return NotFound();

        if (job.ClientId != clientId)
            return Problem("Cannot cancel jobs from other clients.", statusCode: 403);

        if (job.Status != JobStatus.Scheduled)
            return Problem("Only jobs with status 'Scheduled' can be cancelled.", statusCode: 409);

        if (_jobService.IsWithinModificationThreshold(job.ScheduledAt))
            return Problem("Cannot cancel a job within 1 minute of its scheduled execution.", statusCode: 409);

        await _jobService.CancelAsync(job, cancellationToken);
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
