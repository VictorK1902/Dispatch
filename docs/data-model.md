# Overview

Azure SQL database backing the Job Dispatch Service. Three tables: `job_module`, `job`, and `notification`.

# Tables

## `job_module`

Defines the predefined types of jobs the system can execute.

| Column | Type | Notes |
|--------|------|-------|
| id | int (PK) | |
| name | nvarchar(100) | e.g., `Weather Check`, `SP500 Price Reminder` |
| description | nvarchar(500) | |
| created_at | datetime2 | |

## `job`

Represents a scheduled job submitted by a client.

| Column | Type | Notes |
|--------|------|-------|
| id | uniqueidentifier (PK) | Also used as `CorrelationId` on Service Bus message |
| client_id | nvarchar(200) | Entra ID `appid` claim from JWT |
| job_module_id | int | FK to `job_module.id` |
| status | nvarchar(50) | `Scheduled`, `Running`, `Completed`, `Failed`, `Cancelled` |
| scheduled_at | datetime2 | When the job is scheduled to execute |
| data_payload | nvarchar(max) | JSON serialized string format |
| created_at | datetime2 | |
| updated_at | datetime2 | |
| service_bus_sequence_number | bigint (nullable) | Stored for cancellation/modification support |

## `notification`

Records outbound emails sent for a job (success or failure).

| Column | Type | Notes |
|--------|------|-------|
| id | uniqueidentifier (PK) | |
| job_id | uniqueidentifier | FK to `job.id` |
| type | nvarchar(50) | `Success`, `Failure` |
| recipient_email | nvarchar(200) | |
| sent_at | datetime2 | |
| acs_message_id | nvarchar(200) | ACS tracking ID |

# Notes

- All datetime columns use UTC
- `job.id` doubles as the Service Bus `CorrelationId` to link queue messages back to SQL records
- Status transitions: `Scheduled` → `Running` → `Completed` or `Failed`; `Scheduled` → `Cancelled`
