# ADR-004: Direct Service Bus Enqueue over Outbox Pattern

- **Status:** Accepted
- **Date:** 2026-04-06

## Context

The API coordinates writes between two independent systems — Azure SQL and Azure Service Bus. These systems do not share a transaction boundary, which creates a partial failure risk: a SQL write can succeed while the subsequent Service Bus operation fails, leaving the system in an inconsistent state. This affects POST (enqueue), PUT (cancel + re-enqueue), and DELETE (cancel).

## Options Considered

1. **Direct enqueue** — the API calls Service Bus immediately after writing to SQL. On failure, return `500` and rely on client retry.
2. **Outbox pattern** — the API writes the job record and an outbox entry in a single SQL transaction. A background process polls the outbox table and publishes messages to Service Bus, then marks the entry as processed. The API never calls Service Bus directly.

## Decision

Use **direct enqueue** for current scope.

## Rationale

- The outbox pattern eliminates the partial failure window entirely — a single SQL transaction guarantees both the job record and the outbox entry exist or neither does. The background publisher retries until Service Bus delivery succeeds, making the system eventually consistent.
- However, the outbox pattern adds significant implementation overhead: an `Outbox` table, a background polling process (e.g., a timer-triggered Function), idempotent publishing logic, and cleanup of processed entries.
- For a portfolio project at demo scale, direct enqueue with `500` + client retry is an acceptable tradeoff. The failure modes are documented in the API contract.

## Consequences

- Partial failures are possible on all write endpoints (POST, PUT, DELETE). These are documented in `api-contract.md` under "Known Edge Cases."
- The Worker should check job status before executing to handle stale messages (e.g., a `Cancelled` job whose Service Bus cancel failed).
- If this system moved to production, the outbox pattern would be the recommended path to guarantee delivery.
