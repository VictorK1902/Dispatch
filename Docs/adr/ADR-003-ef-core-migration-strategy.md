# ADR-003: EF Core Migration Strategy

- **Status:** Accepted
- **Date:** 2026-04-10

## Context

The Data project uses EF Core with Azure SQL. We need a strategy for creating and applying schema migrations. The data access layer is a standalone class library (`Data.csproj`) referenced by multiple consumer apps (Worker Function, API).

`dotnet ef` requires a startup project that can construct the `DbContext` at design time. Using a consumer app (e.g., the Azure Function) as the startup project is fragile — the Functions host has complex startup behavior that EF tooling wasn't designed for.

## Options Considered

1. **`IDesignTimeDbContextFactory` in the Data project** — A factory class in the Data library that constructs the `DbContext` using an environment variable for the connection string. EF tooling discovers it automatically. No additional projects needed.

2. **Dedicated Migrator console app** — A thin console project (`Data.Migrator`) that references the Data library and registers the `DbContext`. Used as the `--startup-project` for EF commands. Provides full `IConfiguration` and DI support.

3. **SQL script generation in CI/CD (enterprise preferred)** — Migrations are authored locally, but never applied via `dotnet ef database update` in production. Instead, the pipeline generates an idempotent SQL script (`dotnet ef migrations script --idempotent`), which is reviewed and executed by a privileged service connection. The app runtime identity has no DDL permissions.

## Decision

Use **Option 1 (`IDesignTimeDbContextFactory`)** with manual application via `dotnet ef database update`.

## Rationale

- Simplest option for a solo-developer project at current scale.
- No extra projects to maintain — the factory lives in the Data library alongside the `DbContext`.
- Connection string is supplied via environment variable (`DispatchConnection`), keeping secrets out of source.
- The factory is only used by EF tooling at design/migration time; runtime DI registration in consumer apps is unaffected.

## Migration Workflow

```bash
# Set connection string for the session
export DispatchConnection="Server=<server>.database.windows.net;Database=DispatchDb;..."

# Add a migration
dotnet ef migrations add <MigrationName> --project Data --startup-project Data

# Apply to Azure SQL
dotnet ef database update --project Data --startup-project Data
```

## CI/CD Migration Decision (2026-04-10)

When adding GitHub Actions CI/CD, the following database migration pipelines were considered:

1. **Auto-apply (`dotnet ef database update` in deploy workflow)** — rejected. Destructive migrations (column/table drops) would execute without human review.
2. **Generate SQL script on every push, upload as artifact** — rejected. Noisy in a team setting — most pushes don't touch Data, creating irrelevant artifacts. Path-filtered generation adds complexity for marginal benefit.
3. **Keep migrations manual** — accepted. Migrations are infrequent, the `IDesignTimeDbContextFactory` workflow is already established, and manual review before applying schema changes is the safest default. This matches how many teams handle database changes regardless of pipeline maturity.

If the project scales to multiple contributors or frequent schema changes, revisit Option 2 with path-filtered triggers (`Data/` changes only) and a manual approval gate before apply.

## Future Considerations

- **Dedicated Migrator project:** If the design-time factory becomes insufficient (e.g., needing seed data via DI, or multiple DbContexts), introduce a `Data.Migrator` console app.

## Consequences

- Developers must set the `DispatchConnection` environment variable before running EF commands locally.
- Migrations are applied directly via `dotnet ef database update`, meaning the local machine needs network access to Azure SQL.
- The runtime identity currently has DDL permissions — acceptable for development, not for production.
