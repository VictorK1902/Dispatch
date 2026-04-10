using System.Security.Claims;
using System.Text.Json;
using Dispatch.Api.Controllers;
using Dispatch.Api.Models;
using Dispatch.Api.Services;
using Dispatch.Contracts;
using Dispatch.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Api.Tests.Controllers;

public class JobsControllerTests
{
    private readonly Mock<IJobService> _jobServiceMock;
    private readonly JobsController _controller;

    private const string ClientId = "test-client-id";
    private static readonly DateTime weatherDate = new DateTime(2026, 4, 20);
    private static readonly DateTime baseScheduledDate = new DateTime(2026, 4, 20);
    private static readonly string weatherReportInput = JsonSerializer.Serialize(new
        {
            sendTo = "test@test.com",
            latitude = 0,
            longitude = 0,
            forecastDays = 1,
            day = weatherDate
        });

    public JobsControllerTests()
    {
        _jobServiceMock = new Mock<IJobService>();
        _controller = new JobsController(_jobServiceMock.Object);
    }

    private void SetClientId(string? clientId)
    {
        var claims = new List<Claim>();
        if (clientId is not null)
            claims.Add(new Claim("appid", clientId));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private static Job CreateWeatherJob(string clientId = ClientId, string status = JobStatus.Scheduled, DateTimeOffset? scheduledAt = null)
    {
        return new Job
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            JobModuleId = JobModuleTypes.WeatherReport,
            Status = status,
            ScheduledAt = scheduledAt ?? baseScheduledDate.AddHours(1),
            DataPayload = weatherReportInput,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static void WeatherDataAreEqual(JsonElement left, JsonElement right)
    {
        Assert.Equal(left.GetProperty("latitude").GetDouble(), right.GetProperty("latitude").GetDouble());
        Assert.Equal(left.GetProperty("longitude").GetDouble(), right.GetProperty("longitude").GetDouble());
        Assert.Equal(left.GetProperty("day").GetDateTime(), right.GetProperty("day").GetDateTime());
        Assert.Equal(left.GetProperty("forecastDays").GetInt32(), right.GetProperty("forecastDays").GetInt32());
        Assert.Equal(left.GetProperty("sendTo").GetString(), right.GetProperty("sendTo").GetString());
    }

    private static JsonElement ToJsonElement(object obj)
    {
        return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(obj));
    }

    #region Create

    [Fact]
    public async Task Create_HappyPath_ReturnsCreatedAtAction()
    {
        SetClientId(ClientId);
        var request = new CreateJobRequest
        {
            JobModuleId = JobModuleTypes.WeatherReport,
            ScheduledAt = baseScheduledDate.AddHours(3),
            Data = ToJsonElement(new { sendTo = "test@test.com", latitude = 0, longitude = 0, forecastDays = 1, day = weatherDate })
        };
        var job = CreateWeatherJob(scheduledAt: request.ScheduledAt);

        _jobServiceMock.Setup(s => s.ValidateJobModule(request.JobModuleId, It.IsAny<JsonElement>()))
            .Returns((string?)null);
        _jobServiceMock.Setup(s => s.CreateAsync(ClientId, request.JobModuleId, request.ScheduledAt, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(JobsController.Get), created.ActionName);
        var jobresponse = Assert.IsType<JobResponse>(created.Value);
        Assert.Equal(request.ScheduledAt, jobresponse.ScheduledAt);
        WeatherDataAreEqual(request.Data, jobresponse.Data);
    }

    [Fact]
    public async Task Create_MissingClientId_Returns401()
    {
        SetClientId(null);
        var request = new CreateJobRequest
        {
            JobModuleId = JobModuleTypes.WeatherReport,
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(1),
            Data = ToJsonElement(new { sendTo = "test@test.com" })
        };

        var result = await _controller.Create(request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Unable to determine client identity.", details.Detail);
    }

    [Fact]
    public async Task Create_ValidationFailure_Returns400()
    {
        SetClientId(ClientId);
        var request = new CreateJobRequest
        {
            JobModuleId = 999,
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(1),
            Data = ToJsonElement(new { })
        };

        _jobServiceMock.Setup(s => s.ValidateJobModule(999, It.IsAny<JsonElement>()))
            .Returns("Unknown JobModuleId: 999.");

        var result = await _controller.Create(request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Unknown JobModuleId: 999.", details.Detail);
    }

    #endregion

    #region Get

    [Fact]
    public async Task Get_HappyPath_ReturnsOk()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob(scheduledAt: baseScheduledDate);

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var result = await _controller.Get(job.Id, CancellationToken.None);

        var objResult = Assert.IsType<OkObjectResult>(result);
        var jobresponse = Assert.IsType<JobResponse>(objResult.Value);
        Assert.Equal(job.ScheduledAt, jobresponse.ScheduledAt);
        WeatherDataAreEqual(JsonSerializer.Deserialize<JsonElement>(job.DataPayload), jobresponse.Data);
    }

    [Fact]
    public async Task Get_MissingClientId_Returns401()
    {
        SetClientId(null);

        var result = await _controller.Get(Guid.NewGuid(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Unable to determine client identity.", details.Detail);
    }

    [Fact]
    public async Task Get_JobNotFound_Returns404()
    {
        SetClientId(ClientId);
        var id = Guid.NewGuid();

        _jobServiceMock.Setup(s => s.GetAsync(id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        var result = await _controller.Get(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Update

    [Fact]
    public async Task Update_HappyPath_ReturnsOk()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob(scheduledAt: baseScheduledDate.AddHours(2));
        var request = new UpdateJobRequest
        {
            ScheduledAt = baseScheduledDate.AddHours(3),
            Data = ToJsonElement(new { sendTo = "test@test.com", latitude = 100, longitude = 0, forecastDays = 3, day = weatherDate.AddDays(2).Date })
        };

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(job.ScheduledAt))
            .Returns(false);
        _jobServiceMock.Setup(s => s.ValidateJobModule(job.JobModuleId, It.IsAny<JsonElement>()))
            .Returns((string?)null);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(request.ScheduledAt))
            .Returns(false);
        // Do a manual update and ensure they match before mocking
        job.ScheduledAt = request.ScheduledAt;
        job.DataPayload = JsonSerializer.Serialize(request.Data);
        _jobServiceMock.Setup(s => s.UpdateAsync(job, request.ScheduledAt, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var result = await _controller.Update(job.Id, request, CancellationToken.None);

        var objResult = Assert.IsType<OkObjectResult>(result);
        var jobresponse = Assert.IsType<JobResponse>(objResult.Value);
        Assert.Equal(request.ScheduledAt, jobresponse.ScheduledAt);
        WeatherDataAreEqual(request.Data, jobresponse.Data);
    }

    [Fact]
    public async Task Update_MissingClientId_Returns401()
    {
        SetClientId(null);
        var request = new UpdateJobRequest
        {
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(3),
            Data = ToJsonElement(new { })
        };

        var result = await _controller.Update(Guid.NewGuid(), request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Unable to determine client identity.", details.Detail);
    }

    [Fact]
    public async Task Update_JobNotFound_Returns404()
    {
        SetClientId(ClientId);
        var id = Guid.NewGuid();

        _jobServiceMock.Setup(s => s.GetAsync(id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        var request = new UpdateJobRequest
        {
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(3),
            Data = ToJsonElement(new { })
        };

        var result = await _controller.Update(id, request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_JobNotScheduled_Returns409()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob(status: JobStatus.Completed);

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var request = new UpdateJobRequest
        {
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(3),
            Data = ToJsonElement(new { })
        };

        var result = await _controller.Update(job.Id, request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Only jobs with status 'Scheduled' can be modified.", details.Detail);
    }

    [Fact]
    public async Task Update_WithinModificationThreshold_Returns409()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob();

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(job.ScheduledAt))
            .Returns(true);

        var request = new UpdateJobRequest
        {
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(3),
            Data = ToJsonElement(new { })
        };

        var result = await _controller.Update(job.Id, request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Cannot modify a job within 1 minute of its scheduled execution.", details.Detail);
    }

    [Fact]
    public async Task Update_ValidationFailure_Returns400()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob(scheduledAt: DateTimeOffset.UtcNow.AddHours(2));

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(job.ScheduledAt))
            .Returns(false);
        _jobServiceMock.Setup(s => s.ValidateJobModule(job.JobModuleId, It.IsAny<JsonElement>()))
            .Returns("Invalid JSON for Weather Report module.");

        var request = new UpdateJobRequest
        {
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(3),
            Data = ToJsonElement(new { })
        };

        var result = await _controller.Update(job.Id, request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Invalid JSON for Weather Report module.", details.Detail);
    }

    [Fact]
    public async Task Update_NewScheduledAtWithinThreshold_Returns400()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob(scheduledAt: DateTimeOffset.UtcNow.AddHours(2));
        var newScheduledAt = DateTimeOffset.UtcNow.AddSeconds(30);

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(job.ScheduledAt))
            .Returns(false);
        _jobServiceMock.Setup(s => s.ValidateJobModule(job.JobModuleId, It.IsAny<JsonElement>()))
            .Returns((string?)null);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(newScheduledAt))
            .Returns(true);

        var request = new UpdateJobRequest
        {
            ScheduledAt = newScheduledAt,
            Data = ToJsonElement(new { sendTo = "test@test.com", latitude = 0, longitude = 0, forecastDays = 1 })
        };

        var result = await _controller.Update(job.Id, request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Updated ScheduledAt must be at least 1 minute in the future.", details.Detail);
    }

    #endregion

    #region Patch

    [Fact]
    public async Task Patch_BothFields_ReturnsOk()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob(scheduledAt: baseScheduledDate.AddHours(2));
        var newScheduledAt = baseScheduledDate.AddHours(3);
        var newData = ToJsonElement(new { sendTo = "new@test.com", latitude = 1, longitude = 1, forecastDays = 3, day = weatherDate.AddDays(1).Date });
        var request = new PatchJobRequest { ScheduledAt = newScheduledAt, Data = newData };

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(job.ScheduledAt))
            .Returns(false);
        _jobServiceMock.Setup(s => s.ValidateJobModule(job.JobModuleId, It.IsAny<JsonElement>()))
            .Returns((string?)null);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(newScheduledAt))
            .Returns(false);
        // Manual update before mocking
        job.ScheduledAt = newScheduledAt;
        job.DataPayload = JsonSerializer.Serialize(newData);
        _jobServiceMock.Setup(s => s.UpdateAsync(job, newScheduledAt, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var result = await _controller.Patch(job.Id, request, CancellationToken.None);

        var objResult = Assert.IsType<OkObjectResult>(result);
        var jobresponse = Assert.IsType<JobResponse>(objResult.Value);
        Assert.Equal(request.ScheduledAt, jobresponse.ScheduledAt);
        WeatherDataAreEqual(newData, jobresponse.Data);
    }

    [Fact]
    public async Task Patch_OnlyScheduledAt_ReturnsOk()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob(scheduledAt: baseScheduledDate.AddHours(2));
        var newScheduledAt = baseScheduledDate.AddHours(3);
        var request = new PatchJobRequest { ScheduledAt = newScheduledAt };

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(job.ScheduledAt))
            .Returns(false);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(newScheduledAt))
            .Returns(false);

        job.ScheduledAt = newScheduledAt;
        _jobServiceMock.Setup(s => s.UpdateAsync(job, newScheduledAt, job.DataPayload, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var result = await _controller.Patch(job.Id, request, CancellationToken.None);

        var objResult = Assert.IsType<OkObjectResult>(result);
        var jobresponse = Assert.IsType<JobResponse>(objResult.Value);
        Assert.Equal(request.ScheduledAt, jobresponse.ScheduledAt);
    }

    [Fact]
    public async Task Patch_OnlyData_ReturnsOk()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob(scheduledAt: baseScheduledDate.AddHours(2));
        var newData = ToJsonElement(new { sendTo = "new@test.com", latitude = 1, longitude = 1, forecastDays = 3, day = weatherDate.AddDays(1).Date });
        var request = new PatchJobRequest { Data = newData };

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(job.ScheduledAt))
            .Returns(false);
        _jobServiceMock.Setup(s => s.ValidateJobModule(job.JobModuleId, It.IsAny<JsonElement>()))
            .Returns((string?)null);
        
        job.DataPayload = JsonSerializer.Serialize(newData);
        _jobServiceMock.Setup(s => s.UpdateAsync(job, job.ScheduledAt, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var result = await _controller.Patch(job.Id, request, CancellationToken.None);

        var objResult = Assert.IsType<OkObjectResult>(result);
        var jobresponse = Assert.IsType<JobResponse>(objResult.Value);
        WeatherDataAreEqual(newData, jobresponse.Data);
    }

    [Fact]
    public async Task Patch_NoFieldsProvided_Returns400()
    {
        SetClientId(ClientId);
        var request = new PatchJobRequest();

        var result = await _controller.Patch(Guid.NewGuid(), request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("At least one of 'ScheduledAt' or 'Data' must be provided.", details.Detail);
    }

    [Fact]
    public async Task Patch_MissingClientId_Returns401()
    {
        SetClientId(null);
        var request = new PatchJobRequest { ScheduledAt = baseScheduledDate.AddHours(3) };

        var result = await _controller.Patch(Guid.NewGuid(), request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Unable to determine client identity.", details.Detail);
    }

    [Fact]
    public async Task Patch_JobNotFound_Returns404()
    {
        SetClientId(ClientId);
        var id = Guid.NewGuid();
        var request = new PatchJobRequest { ScheduledAt = baseScheduledDate.AddHours(3) };

        _jobServiceMock.Setup(s => s.GetAsync(id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        var result = await _controller.Patch(id, request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Patch_JobNotScheduled_Returns409()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob(status: JobStatus.Completed);
        var request = new PatchJobRequest { ScheduledAt = baseScheduledDate.AddHours(3) };

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var result = await _controller.Patch(job.Id, request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Only jobs with status 'Scheduled' can be modified.", details.Detail);
    }

    [Fact]
    public async Task Patch_WithinModificationThreshold_Returns409()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob();
        var request = new PatchJobRequest { ScheduledAt = baseScheduledDate.AddHours(3) };

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(job.ScheduledAt))
            .Returns(true);

        var result = await _controller.Patch(job.Id, request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Cannot modify a job within 1 minute of its scheduled execution.", details.Detail);
    }

    [Fact]
    public async Task Patch_DataValidationFailure_Returns400()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob(scheduledAt: baseScheduledDate.AddHours(2));
        var request = new PatchJobRequest { Data = ToJsonElement(new { }) };

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(job.ScheduledAt))
            .Returns(false);
        _jobServiceMock.Setup(s => s.ValidateJobModule(job.JobModuleId, It.IsAny<JsonElement>()))
            .Returns("Invalid JSON for Weather Report module.");

        var result = await _controller.Patch(job.Id, request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Invalid JSON for Weather Report module.", details.Detail);
    }

    [Fact]
    public async Task Patch_NewScheduledAtWithinThreshold_Returns400()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob(scheduledAt: baseScheduledDate.AddHours(2));
        var newScheduledAt = baseScheduledDate.AddSeconds(30);
        var request = new PatchJobRequest { ScheduledAt = newScheduledAt };

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(job.ScheduledAt))
            .Returns(false);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(newScheduledAt))
            .Returns(true);

        var result = await _controller.Patch(job.Id, request, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Updated ScheduledAt must be at least 1 minute in the future.", details.Detail);
    }

    #endregion

    #region Delete

    [Fact]
    public async Task Delete_HappyPath_ReturnsNoContent()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob(scheduledAt: baseScheduledDate.AddHours(2));

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(job.ScheduledAt))
            .Returns(false);

        var result = await _controller.Delete(job.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _jobServiceMock.Verify(s => s.CancelAsync(job, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_MissingClientId_Returns401()
    {
        SetClientId(null);

        var result = await _controller.Delete(Guid.NewGuid(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Unable to determine client identity.", details.Detail);
    }

    [Fact]
    public async Task Delete_JobNotFound_Returns404()
    {
        SetClientId(ClientId);
        var id = Guid.NewGuid();

        _jobServiceMock.Setup(s => s.GetAsync(id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        var result = await _controller.Delete(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_JobNotScheduled_Returns409()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob(status: JobStatus.Completed);

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var result = await _controller.Delete(job.Id, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Only jobs with status 'Scheduled' can be cancelled.", details.Detail);
    }

    [Fact]
    public async Task Delete_WithinModificationThreshold_Returns409()
    {
        SetClientId(ClientId);
        var job = CreateWeatherJob();

        _jobServiceMock.Setup(s => s.GetAsync(job.Id, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobServiceMock.Setup(s => s.IsWithinModificationThreshold(job.ScheduledAt))
            .Returns(true);

        var result = await _controller.Delete(job.Id, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Cannot cancel a job within 1 minute of its scheduled execution.", details.Detail);
    }

    #endregion
}
