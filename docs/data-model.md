# Overview

Azure SQL database backing the Job Dispatch Service. Three tables: `JobModule`, `Job`, and `Notification`.

# Tables

## `JobModule`

Defines the predefined types of job the system can execute.

| Column | Type | Notes |
|--------|------|-------|
| Id | int (PK) | |
| Name | nvarchar(100) | e.g., `Weather Check`, `SP500 Price Reminder` |
| Description | nvarchar(500) | |
| CreatedAt | datetime2 | |

## `Job`

Represents a scheduled job submitted by a client.

| Column | Type | Notes |
|--------|------|-------|
| Id | uniqueidentifier (PK) | Also used as `CorrelationId` on Service Bus message |
| ClientId | nvarchar(200) | Entra ID `appid` claim from JWT |
| JobModuleId | int | FK to `JobModule.Id` |
| Status | nvarchar(50) | `Scheduled`, `Running`, `Completed`, `Failed`, `Cancelled` |
| ScheduledAt | datetime2 | When the job is scheduled to execute |
| DataPayload | nvarchar(max) | JSON serialized string format |
| CreatedAt | datetime2 | |
| UpdatedAt | datetime2 | |
| ServiceBusSequenceNumber | bigint (nullable) | Stored for cancellation/modification support |

## `Notification`

Records outbound emails sent for a job (success or failure).

| Column | Type | Notes |
|--------|------|-------|
| Id | uniqueidentifier (PK) | |
| JobId | uniqueidentifier | FK to `Job.Id` |
| Type | nvarchar(50) | `Success`, `Failure` |
| RecipientEmail | nvarchar(200) | |
| SentAt | datetime2 | |
| AcsMessageId | nvarchar(200) | ACS tracking ID |

# Notes

- All datetime columns use UTC
- `Job.Id` doubles as the Service Bus `CorrelationId` to link queue messages back to SQL records
- Status transitions: `Scheduled` → `Running` → `Completed` or `Failed`; `Scheduled` → `Cancelled`
