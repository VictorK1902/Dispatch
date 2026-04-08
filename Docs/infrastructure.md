# Overview

All Azure resources for the Job Dispatch Service, defined as Infrastructure-as-Code (Bicep).

> TODO: Bicep files will live under `/infra`

# Resource Inventory

| Resource | SKU / Tier | Notes |
|----------|-----------|-------|
| App Service Plan | B1 | Hosts the Job Scheduling API |
| App Service | - | Public Job Scheduling API |
| Azure SQL Server | - | Logical server; parent of the SQL Database |
| Azure SQL Database | Serverless / Standard | Cost-efficient for demo |
| Service Bus Namespace | Standard | Supports scheduled messages, DLQ, competing consumers |
| Service Bus Queue | - | `jobs-queue`; main job queue |
| Azure Function Plan (Worker) | Flex Consumption | One plan per Function App |
| Azure Function App (Worker) | - | Service Bus trigger |
| Azure Communication Services | Free tier | Email sending (100/day) |
| Application Insights | - | Observability for API and Functions |
| Log Analytics Workspace | - | Backing store for App Insights |
| User-Assigned MI (Worker) | - | `uami-dispatch-worker`; sole identity for Worker Function |

# Managed Identity Role Assignments

Each Function App uses a single user-assigned MI for all access (Storage, App Insights, Service Bus, SQL, ACS). System-assigned MI is disabled on Function Apps. App Service uses a system-assigned MI. SQL roles are not Azure RBAC — they are granted via T-SQL post-deployment (`CREATE USER [<identity-name>] FROM EXTERNAL PROVIDER`).

| Identity | Resource | Role |
|----------|----------|------|
| App Service (system-assigned) | Service Bus | Azure Service Bus Data Sender |
| App Service (system-assigned) | Azure SQL | db_datareader, db_datawriter |
| `uami-dispatch-worker` (user-assigned) | Storage Account | Storage Blob Data Owner (auto-assigned) |
| `uami-dispatch-worker` (user-assigned) | Storage Blob Container | Storage Blob Data Contributor (auto-assigned) |
| `uami-dispatch-worker` (user-assigned) | Application Insights | Monitoring Metrics Publisher (auto-assigned) |
| `uami-dispatch-worker` (user-assigned) | Service Bus | Azure Service Bus Data Receiver |
| `uami-dispatch-worker` (user-assigned) | Azure SQL | db_datareader, db_datawriter (T-SQL) |
| `uami-dispatch-worker` (user-assigned) | Communication Service | Communication and Email Service Owner |

# Entra ID App Registrations

| Registration | Purpose |
|-------------|---------|
| `dispatch-api` | Represents the API; defines the `Jobs.ReadWrite` app role |
| `dispatch-client-test` | Test client; granted `Jobs.ReadWrite` with admin consent |
| `dispatch-client-{name}` | One per additional authorized client; granted the app role |

# Notes

- All resources in a single resource group: `rg-dispatch`
- Location: `centralus`
- Naming convention: `{resource-type}-dispatch-{qualifier}` (e.g., `func-dispatch-worker`, `uami-dispatch-worker`)
- IaC: Bicep preferred over ARM; Terraform is an alternative
