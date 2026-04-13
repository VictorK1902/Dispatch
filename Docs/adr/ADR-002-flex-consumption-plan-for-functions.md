# ADR-002: Flex Consumption Plan for Azure Functions

- **Status:** Accepted
- **Date:** 2026-03-31

## Context

The Worker and DLQ Handler are Azure Functions triggered by Service Bus. A hosting plan must be chosen.

## Options Considered

1. **Flex Consumption plan** — pay-per-execution, cold start that can be improved via pre-provisioned (always-ready) instances, scales to zero, can scale up to 1000 instances, unlimited timeout, one app per plan
2. **Premium plan** — pre-warmed instances, no cold start, 20-100 instances, up to 100 apps per plan, unlimited timeout, higher cost
3. **App Service plan** — always-on, fixed cost, manual scaling

## Decision

Use **Flex Consumption plan**.

## Rationale

- Cold start is irrelevant for a queue-triggered scenario, since there is no end-user waiting on a synchronous response.
- Scale-to-zero is desirable for a portfolio demo (zero cost when idle).
- Unlimited timeout removes any execution duration constraint for job modules.
- Premium plan cost is not justified without a cold-start sensitivity requirement.

## Consequences

- Scale-out is automatic; no tuning required at this stage.
- One plan - One Function App — Two Functions required for Worker and DLQ Handler.
