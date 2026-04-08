# 1. Job Scheduled and Executed

```mermaid
sequenceDiagram
    participant Client
    participant API as API (App Service)
    participant SQL as Azure SQL
    participant SB as Service Bus
    participant Worker as Worker Function

    Client->>API: POST /jobs
    API->>SQL: INSERT job (status: Scheduled)
    SQL-->>API: job.id
    API->>SB: Enqueue message (ScheduledEnqueueTime, CorrelationId = job.id)
    SB-->>API: job.sequenceNumber
    API->>SQL: UPDATE job (sequenceNumber)
    API-->>Client: 201 Created

    Note over SB: Time passes — scheduled time arrives

    SB->>Worker: Deliver message
    Note over Worker: Execute module logic
    Worker->>SQL: UPDATE job (status: Completed, AcsMessageId)
    Worker->>SB: Complete message
```

# 2. Failure Path — Worker Fails, DLQ Handler Fires

```mermaid
sequenceDiagram
    participant SB as Service Bus
    participant Worker as Worker Function
    participant DLQ as Service Bus DLQ
    participant DLQHandler as DLQ Handler Function
    participant ACS
    participant SQL as Azure SQL

    loop Until Retries Exhausted
      SB->>Worker: Deliver message
      Note over Worker: Exception thrown
    end    

    DLQ->>DLQHandler: Deliver DLQ message
    SB->>DLQ: Move message to Dead Letter Queue
    DLQHandler->>ACS: Send failure notification email
    ACS-->>DLQHandler: OK
    DLQHandler->>SQL: UPDATE job (status: Failed, AcsMessageId)
    DLQHandler->>DLQ: Complete DLQ message
```

# 3. Job Modification Before Threshold

```mermaid
sequenceDiagram
    participant Client
    participant API as API (App Service)
    participant SQL as Azure SQL
    participant SB as Service Bus

    Client->>API: PUT /jobs/{id}
    API->>SQL: GET job
    SQL-->>API: job (status: Scheduled, >1 min before execution)
    API->>SB: Cancel scheduled message (by sequence number)
    API->>SB: Enqueue new message (updated ScheduledEnqueueTime)
    API->>SQL: UPDATE job (scheduledAt, service_bus_sequence_number)
    API-->>Client: 200 OK
```

# 4. Job Cancellation Before Threshold

```mermaid
sequenceDiagram
    participant Client
    participant API as API (App Service)
    participant SQL as Azure SQL
    participant SB as Service Bus

    Client->>API: DELETE /jobs/{id}
    API->>SQL: GET job
    SQL-->>API: job (status: Scheduled, >1 min before execution)
    API->>SB: Cancel scheduled message (by sequence number)
    API->>SQL: UPDATE job (status: Cancelled)
    API-->>Client: 204 No Content
```

# 5. Job Modification After Threshold

```mermaid
sequenceDiagram
    participant Client
    participant API as API (App Service)
    participant SQL as Azure SQL

    Client->>API: PUT /jobs/{id}
    API->>SQL: GET job
    SQL-->>API: job (not modifiable — within threshold or invalid status)
    API-->>Client: 409 Conflict
```

# 6. Job Cancellation After Threshold

```mermaid
sequenceDiagram
    participant Client
    participant API as API (App Service)
    participant SQL as Azure SQL

    Client->>API: DELETE /jobs/{id}
    API->>SQL: GET job
    SQL-->>API: job (not modifiable — within threshold or invalid status)
    API-->>Client: 409 Conflict
```