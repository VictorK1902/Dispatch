# Overview

The Job Dispatch Service exposes a REST API for authorized clients to schedule and manage jobs.

- **Host:** Azure App Service
- **Auth:** Entra ID client credentials flow - valid JWT bearer token required
- **Base path:** `/api/v1`
- **Format:** JSON

> TODO: Export full OpenAPI 3.0 spec to `openapi.yaml` alongside this file

# Authentication

All endpoints require:

```
Authorization: Bearer <jwt>
```

Token is obtained via Entra ID client credentials flow. The `appid` claim identifies the calling client and is stored on the job record.

# Endpoints

## POST /jobs
Schedule a new job.

**Request body:**
```json
{
  "jobModuleId": 1,
  "scheduledAt": "2026-04-15T14:00:00Z",
  "data":{
    // Module specific properties
  }
}
```

**Response `201 Created`:**
```json
{
  "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Scheduled",
  "scheduledAt": "2026-04-15T14:00:00Z"
}
```

## GET /jobs/{jobId}
Retrieve status and details for a job.

**Response `200 OK`:**
```json
{
  "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "jobModuleId": 1,
  "status": "Scheduled",
  "scheduledAt": "2026-04-15T14:00:00Z",
  "createdAt": "2026-03-31T10:00:00Z",
  "data":{
    // Module specific properties
  }
}
```

## PUT /jobs/{jobId}
Modify a job's scheduled time and any job module specific property. Only allowed if the job is still `Scheduled` and outside the modification threshold (e.g., >10 minutes before execution).

**Request body:**
```json
{
  "scheduledAt": "2026-04-15T16:00:00Z",
  "data":{
    // Module specific properties
  }
}
```

**Response `200 OK`:**
```json
{
  "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Scheduled",
  "scheduledAt": "2026-04-15T16:00:00Z",
  "data":{
    // Module specific properties
  }
}
```

## DELETE /jobs/{jobId}
Cancel a scheduled job. Only allowed if the job is still `Scheduled` and outside the modification threshold.

**Response `204 No Content`**

# Error Responses

| Status | Meaning |
|--------|---------|
| 400 | Validation error (e.g., scheduledAt in the past) |
| 401 | Missing or invalid JWT |
| 403 | Client not authorized |
| 404 | Job not found |
| 409 | Job cannot be modified (within threshold or wrong status) |
| 500 | Internal server error |

# Notes

- Clients can only view/modify jobs they created (`appid` claim must match)
- `scheduledAt` must be UTC

# Known Edge Cases

## POST /jobs — Partial Failure (SQL succeeds, Service Bus enqueue fails)
If the job record is written to SQL but the subsequent Service Bus enqueue fails, the job will be stuck in `Scheduled` status indefinitely with no corresponding message in the queue — it will never execute. The API returns `500` in this case. The production-grade fix is the outbox pattern (write a pending outbox entry to SQL in the same transaction; a background process publishes to Service Bus). For current scope: best effort — client should retry on `500`, and stuck `Scheduled` records can be detected via `GET /jobs/{jobId}`.
