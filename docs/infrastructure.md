# Overview

All Azure resources for the Job Dispatch Service, defined as Infrastructure-as-Code (Bicep).

> TODO: Bicep files will live under `/infra`

# Resource Inventory

| Resource | SKU / Tier | Notes |
|----------|-----------|-------|
| App Service Plan | B1 | Hosts the Job Scheduling API |
| App Service | - | Public Job Scheduling API |
| Azure SQL Server | - | Logical server; parent of the SQL Database |
| Azure SQL Database | Serverless / Standard S0 | Cost-efficient for demo |
| Service Bus Namespace | Standard | Supports scheduled messages, DLQ, competing consumers |
| Service Bus Queue | - | `jobs-queue`; main job queue |
| Azure Function Plan (Worker) | Flex Consumption | One plan per Function App |
| Azure Function App (Worker) | - | Service Bus trigger |
| Azure Function Plan (DLQ Handler) | Flex Consumption | One plan per Function App |
| Azure Function App (DLQ Handler) | - | Service Bus DLQ trigger |
| Azure Communication Services | Free tier | Email sending (100/day) |
| Key Vault | Standard | Stores ACS connection string |
| Application Insights | - | Observability for API and Functions |
| Log Analytics Workspace | - | Backing store for App Insights |
| User-Assigned MI (Worker) | - | `uami-dispatch-worker`; application identity for Worker Function |
| User-Assigned MI (DLQ Handler) | - | `uami-dispatch-dlq`; application identity for DLQ Handler Function |

# Managed Identity Role Assignments

User-assigned MIs are provisioned via Bicep (`Microsoft.ManagedIdentity/userAssignedIdentities`) and assigned to each resource. App Service uses a system-assigned MI. SQL roles are not Azure RBAC — they are granted via T-SQL post-deployment. ACS does not support a scoped sender role; its connection string is stored in Key Vault and referenced via app settings.

| Identity | Resource | Role |
|----------|----------|------|
| App Service (system-assigned) | Service Bus | Azure Service Bus Data Sender |
| App Service (system-assigned) | Azure SQL | db_datareader, db_datawriter |
| App Service (system-assigned) | Key Vault | Key Vault Secrets User |
| `uami-dispatch-worker` (user-assigned) | Service Bus | Azure Service Bus Data Receiver |
| `uami-dispatch-worker` (user-assigned) | Azure SQL | db_datareader, db_datawriter |
| `uami-dispatch-worker` (user-assigned) | Key Vault | Key Vault Secrets User |
| `uami-dispatch-dlq` (user-assigned) | Service Bus | Azure Service Bus Data Receiver |
| `uami-dispatch-dlq` (user-assigned) | Azure SQL | db_datareader, db_datawriter |
| `uami-dispatch-dlq` (user-assigned) | Key Vault | Key Vault Secrets User |

# Entra ID App Registrations

| Registration | Purpose |
|-------------|---------|
| `dispatch-api` | Represents the API; defines the `access_as_application` scope |
| `dispatch-client-{name}` | One per authorized client; granted access to the API scope |

# Notes

- All resources in a single resource group: `rg-dispatch`
- Location: `centralus`
- Naming convention: `{resource-type}-dispatch-{qualifier}` (e.g., `func-dispatch-worker`, `func-dispatch-worker-uami`)
- IaC: Bicep preferred over ARM; Terraform is an alternative
