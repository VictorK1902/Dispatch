# ADR-001: Service Bus over Azure Storage Queue

- **Status:** Accepted
- **Date:** 2026-03-31

## Context

The Job Dispatch Service needs a message broker to hold scheduled job messages and deliver them to the Worker Function at a future point in time.

## Options Considered

1. **Azure Service Bus (Standard)** — fully-featured broker with scheduled delivery, DLQ, competing consumers, message sessions
2. **Azure Storage Queue** — lightweight, cheap, no native scheduled delivery

## Decision

Use **Azure Service Bus Standard**.

## Rationale

- **Scheduled message delivery** is a first-class feature (`ScheduledEnqueueTime`). Storage Queue has no equivalent — it would require a polling loop or a separate scheduler.
- **Dead Letter Queue** is built in. Failure handling is a core requirement; Storage Queue would need a custom DLQ implementation.
- **Competing consumers** are supported natively, enabling the Worker Function to scale out safely without duplicate processing.
- Cost difference is negligible at demo scale.

## Consequences

- Service Bus Standard tier is required (Basic does not support scheduled messages or DLQ).
- Adds a dependency on Service Bus SDK (`Azure.Messaging.ServiceBus`).
