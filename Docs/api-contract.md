# Overview

The Job Dispatch Service exposes a REST API for authorized clients to schedule and manage jobs.

- **Host:** Azure App Service
- **Auth:** Entra ID client credentials flow - valid JWT bearer token required
- **Base path:** `/api/v1`
- **Format:** JSON


# Authentication

All endpoints require:

```
Authorization: Bearer <jwt>
```

Token is obtained via Entra ID client credentials flow. The `appid` claim (from the JWT) identifies the calling client and is stored as `ClientId` on the job record.

# Endpoints

## POST /jobs

Schedule a new job.

### Request Body Schema

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| jobModuleId | integer | yes | Identifies which job module to run (see [Job Module Data Schemas](#job-module-data-schemas)) |
| scheduledAt | date-time | yes | ISO 8601 date-time with offset (e.g. `2026-04-08T14:00:00Z`) |
| data | object | yes | Module-specific payload (schema varies by `jobModuleId`) |

### Response Schema (`201 Created`)

| Field | Type | Description |
|-------|------|-------------|
| id | uuid | Unique job identifier |
| jobModuleId | integer | Job module identifier |
| status | string | Always `"Scheduled"` for newly created jobs |
| scheduledAt | date-time | Scheduled execution time (UTC) |
| data | object | Module-specific payload |
| createdAt | date-time | Timestamp of creation |
| updatedAt | date-time | Timestamp of last update |

### Sample Request

```json
{
  "jobModuleId": 1,
  "scheduledAt": "2026-04-08T14:00:00Z",
  "data": {
    "latitude": 47.6062,
    "longitude": -122.3321,
    "day": "2026-04-08T00:00:00",
    "forecastDays": 3,
    "sendTo": "test@example.com"
  }
}
```

### Sample Response

```json
{
  "id": "30bcf999-5463-4ade-94de-62cb49b9b305",
  "jobModuleId": 1,
  "status": "Scheduled",
  "scheduledAt": "2026-04-08T14:00:00+00:00",
  "data": {
    "latitude": 47.6062,
    "longitude": -122.3321,
    "day": "2026-04-08T00:00:00",
    "forecastDays": 3,
    "sendTo": "test@example.com"
  },
  "createdAt": "2026-04-08T04:50:08.522019+00:00",
  "updatedAt": "2026-04-08T04:50:08.522019+00:00"
}
```

## GET /jobs/{jobId}

Retrieve status and details for a job.

### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| jobId | uuid | The job's unique identifier |

### Response Schema (`200 OK`)

| Field | Type | Description |
|-------|------|-------------|
| id | uuid | Unique job identifier |
| jobModuleId | integer | Job module identifier |
| status | string | One of: `Scheduled`, `Completed`, `Cancelled`, `Failed` |
| scheduledAt | date-time | Scheduled execution time (UTC) |
| data | object | Module-specific payload |
| createdAt | date-time | Timestamp of creation |
| updatedAt | date-time | Timestamp of last update |

### Sample Response

```json
{
  "id": "30bcf999-5463-4ade-94de-62cb49b9b305",
  "jobModuleId": 1,
  "status": "Scheduled",
  "scheduledAt": "2026-04-08T14:00:00+00:00",
  "data": {
    "latitude": 47.6062,
    "longitude": -122.3321,
    "day": "2026-04-08T00:00:00",
    "forecastDays": 3,
    "sendTo": "test@example.com"
  },
  "createdAt": "2026-04-08T04:50:08.522019+00:00",
  "updatedAt": "2026-04-08T04:50:08.522019+00:00"
}
```

## PUT /jobs/{jobId}

Modify a job's scheduled time and module-specific data. Only allowed if the job is still `Scheduled` and outside the modification threshold (>1 minute before execution). `jobModuleId` cannot be modified.

### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| jobId | uuid | The job's unique identifier |

### Request Body Schema

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| scheduledAt | date-time | yes | ISO 8601 date-time with offset; must be >1 minute in the future |
| data | object | yes | Updated module-specific payload (full replacement) |

### Response Schema (`200 OK`)

Same as [GET /jobs/{jobId}](#get-jobsjobid) response.

### Sample Request

```json
{
  "scheduledAt": "2026-04-08T05:11:00Z",
  "data": {
    "latitude": 47.6062,
    "longitude": -122.3321,
    "day": "2026-04-09T00:00:00",
    "forecastDays": 1,
    "sendTo": "test@example.com"
  }
}
```

### Sample Response

```json
{
  "id": "30bcf999-5463-4ade-94de-62cb49b9b305",
  "jobModuleId": 1,
  "status": "Scheduled",
  "scheduledAt": "2026-04-08T05:11:00+00:00",
  "data": {
    "latitude": 47.6062,
    "longitude": -122.3321,
    "day": "2026-04-09T00:00:00",
    "forecastDays": 1,
    "sendTo": "test@example.com"
  },
  "createdAt": "2026-04-08T04:50:08.522019+00:00",
  "updatedAt": "2026-04-08T05:02:31.738410+00:00"
}
```

## PATCH /jobs/{jobId}

Partially update a job. Only the provided fields are changed; omitted fields retain their current values. Only allowed if the job is still `Scheduled` and outside the modification threshold (>1 minute before execution). `jobModuleId` cannot be modified.

### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| jobId | uuid | The job's unique identifier |

### Request Body Schema

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| scheduledAt | date-time | no | New scheduled time; must be >1 minute in the future |
| data | object | no | Updated module-specific payload (full replacement of `data` only) |

At least one of `scheduledAt` or `data` must be provided. Partial update of `data` is not supported.

### Response Schema (`200 OK`)

Same as [GET /jobs/{jobId}](#get-jobsjobid) response.

### Sample Request — reschedule only

```json
{
  "scheduledAt": "2026-04-10T12:00:00Z"
}
```

### Sample Request — update data only

```json
{
  "data": {
    "latitude": 40.7128,
    "longitude": -74.0060,
    "day": "2026-04-10T00:00:00",
    "forecastDays": 7,
    "sendTo": "test@example.com"
  }
}
```

### Sample Response

```json
{
  "id": "30bcf999-5463-4ade-94de-62cb49b9b305",
  "jobModuleId": 1,
  "status": "Scheduled",
  "scheduledAt": "2026-04-10T12:00:00+00:00",
  "data": {
    "latitude": 40.7128,
    "longitude": -74.0060,
    "day": "2026-04-10T00:00:00",
    "forecastDays": 7,
    "sendTo": "test@example.com"
  },
  "createdAt": "2026-04-08T04:50:08.522019+00:00",
  "updatedAt": "2026-04-09T15:30:00.000000+00:00"
}
```

## DELETE /jobs/{jobId}

Cancel a scheduled job. Only allowed if the job is still `Scheduled` and outside the modification threshold.

### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| jobId | uuid | The job's unique identifier |

### Response

`204 No Content` — no body.

# Error Responses

All errors use the [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) Problem Details format.

| Status | Meaning |
|--------|---------|
| 400 | Validation error (e.g., `scheduledAt` in the past, invalid module data) |
| 401 | Missing or invalid JWT |
| 403 | Client not authorized for this job |
| 404 | Job not found |
| 409 | Job cannot be modified (within threshold or wrong status) |
| 500 | Internal server error |

# Job Module Data Schemas

The `data` property in request/response payloads varies by `jobModuleId`. All fields within `data` are required.

## WeatherReport (`jobModuleId: 1`)

| Field | Type | Description |
|-------|------|-------------|
| latitude | number | Geographic latitude |
| longitude | number | Geographic longitude |
| day | date-time | Target date for the forecast |
| forecastDays | integer | Forecast range — must be 1, 3, or 7 |
| sendTo | string | Recipient email address |

## StockPriceReport (`jobModuleId: 2`)

| Field | Type | Description |
|-------|------|-------------|
| symbol | string | Ticker symbol (e.g. MSFT) |
| sendTo | string | Recipient email address |

# Notes

- Clients can only view/modify jobs they created (JWT `appid` must match the job's `ClientId`).
- `scheduledAt` must include a UTC offset (e.g. `Z`, `+00:00`, `+05:30`). Bare datetimes without an offset are rejected.
- `status` values: `Scheduled`, `Completed`, `Cancelled`, `Failed`.

# Known Edge Cases

## Partial Failure — SQL and Service Bus are not transactional

Every write endpoint coordinates between SQL and Service Bus without a shared transaction. If one succeeds and the other fails, the system can enter an inconsistent state. The API returns `500` in all such cases.

**POST /jobs** — SQL write succeeds, Service Bus enqueue fails. The job is stuck in `Scheduled` with no corresponding message — it will never execute.

**PUT /jobs/{jobId}** and **PATCH /jobs/{jobId}** — Both perform a cancel-and-re-enqueue (Service Bus messages cannot be modified in place). If the cancel succeeds but the re-enqueue fails, the job record reflects the updated schedule but has no message in the queue. If the SQL update succeeds but the cancel fails, the original message is still live and may execute on the old schedule.

**DELETE /jobs/{jobId}** — SQL is updated to `Cancelled` but `CancelScheduledMessageAsync` fails. The message is still live and will be delivered to the Worker. Mitigation: the Worker should check job status before executing and skip `Cancelled` jobs.

**Production-grade fix:** outbox pattern — write a pending outbox entry to SQL in the same transaction; a background process publishes to Service Bus. **Current scope:** best effort — client should retry on `500`, and inconsistent records can be detected via `GET /jobs/{jobId}`.
