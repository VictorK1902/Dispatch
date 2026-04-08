# Purpose

The Job Dispatch Service allows authorized clients to schedule future jobs via a REST API. The backend executes those jobs on time and sends email notifications.

# Components

| Component | Azure Service | Responsibility |
|-----------|--------------|----------------|
| Job Scheduling API | App Service | Accept and validate job requests |
| Storage | Azure SQL | Persist job records and module definitions |
| Message Broker | Service Bus | Hold scheduled (thin) messages; trigger workers to run job at the right time |
| Worker | Azure Function (SB trigger) | Execute job module logic; send success notifications |
| DLQ Handler | Azure Function (DLQ trigger) | Handle failed messages; mark jobs failed; send failure notifications |
| Email | Azure Communication Services | Send outbound email for both success and failure cases |

# High-Level Data Flow

> TODO: Insert C4 Container diagram here

## Happy Path
1. Authorized client submits a POST /jobs request with a scheduled time
2. API validates the request, writes a `job` record to Azure SQL (status: `Scheduled`)
3. API enqueues a Service Bus message with `ScheduledEnqueueTime` set to the job's scheduled time
4. At the scheduled time, Service Bus delivers the message to the Worker Function
5. Worker executes the job module logic (e.g., fetch weather → compose email)
6. Worker sends email via ACS
7. Worker updates job status to `Completed` and store AcsMessageId

## Failure Path
1. Worker throws an exception; Service Bus retries per its retry policy
2. After max delivery count, the message moves to the Dead Letter Queue (DLQ)
3. DLQ Handler Function triggers, reads the failed message
4. DLQ Handler updates job status to `Failed` in SQL
5. DLQ Handler sends a failure notification email via ACS

# Auth & Identity

| Boundary | Mechanism |
|----------|-----------|
| External client → API | Entra ID client credentials flow with JWT bearer token |
| API → Service Bus | Managed Identity (system-assigned) |
| Function → Azure SQL | Managed Identity (user-assigned) |
| Function → ACS | Managed Identity (user-assigned) |

# Observability

- Application Insights attached to both App Service and Azure Functions
- Structured logging throughout

# Solution Structure

| Project | Type | Responsibility | References |
|---------|------|----------------|------------|
| `Api/` | ASP.NET Core Web API | Accept and validate job requests, enqueue to Service Bus | Data, Contracts |
| `Worker/` | Azure Function (Service Bus trigger) | Execute job module logic, send notifications | Data, Contracts |
| `Data/` | Class library | EF Core DbContext, entities, migrations | — |
| `Contracts/` | Class library | Shared job module input models (e.g., `WeatherReportInput`, `StockPriceReportInput`) | — |

# Out of Scope

- Custom job logic beyond predefined modules
- Multi-region deployment
