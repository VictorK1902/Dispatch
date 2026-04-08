# Overview

Azure SQL database backing the Job Dispatch Service. Two tables: `JobModule` and `Job`.

# Tables

## `JobModule`

Defines the predefined types of job the system can execute.

| Column | Type | Notes |
|--------|------|-------|
| Id | int (PK) | |
| Name | nvarchar(100) | e.g., `Weather Report`, `Stock Price Report` |
| Description | nvarchar(500) | |
| CreatedAt | datetimeoffset | |

## `Job`

Represents a scheduled job submitted by a client.

| Column | Type | Notes |
|--------|------|-------|
| Id | uniqueidentifier (PK) | Also used as `CorrelationId` on Service Bus message |
| ClientId | nvarchar(200) | Entra ID `appid` claim from JWT, stored as `ClientId` |
| JobModuleId | int | FK to `JobModule.Id` |
| Status | nvarchar(50) | `Scheduled`, `Completed`, `Failed`, `Cancelled` |
| ScheduledAt | datetimeoffset | When the job is scheduled to execute |
| DataPayload | nvarchar(max) | JSON serialized string format |
| CreatedAt | datetimeoffset | |
| UpdatedAt | datetimeoffset | |
| ServiceBusSequenceNumber | bigint (nullable) | Stored for cancellation/modification support |
| AcsMessageId | nvarchar(200) (nullable) | ACS tracking ID |

# Notes

- `Job.Id` doubles as the Service Bus `CorrelationId` to link queue messages back to SQL records
- Status transitions: `Scheduled` → `Completed` or `Failed`; `Scheduled` → `Cancelled`
