# Provisioning the Dispatch Infrastructure

All Azure resources for Dispatch are managed as Terraform under [infra/](../infra/). This guide covers the one-time bootstrap, the standard apply workflow, and post-deploy steps that aren't automated.

## Prerequisites

- Azure subscription where you have **Owner** at the resource-group scope (needed for RBAC role assignments).
- Tooling: Azure CLI, Terraform `>= 1.6`, `sqlcmd` (for post-deploy T-SQL grants; `brew install sqlcmd` on macOS).
- `az login` to the target subscription:
  ```bash
  az login
  az account set --subscription "<subscription-id>"
  ```

## 1. One-time bootstrap (manual)

Terraform can't manage its own state backend or the Entra app registrations it depends on. Create them once:

### 1a. Remote state storage account

The backend in [infra/backend.tf](../infra/backend.tf) points at a fixed RG, storage account, and container:

```bash
LOCATION=centralus
TFSTATE_RG=rg-dispatch-tfstate
TFSTATE_SA=stdispatchtfstate     # globally unique; rename if taken
TFSTATE_CONTAINER=tfstate

az group create -n "$TFSTATE_RG" -l "$LOCATION"

az storage account create \
  -g "$TFSTATE_RG" -n "$TFSTATE_SA" -l "$LOCATION" \
  --sku Standard_LRS --kind StorageV2 \
  --min-tls-version TLS1_2 \
  --allow-blob-public-access false

az storage container create \
  --account-name "$TFSTATE_SA" \
  -n "$TFSTATE_CONTAINER" \
  --auth-mode login
```

Grant your user **Storage Blob Data Contributor** on the account so `use_azuread_auth` works:

```bash
SUB=$(az account show --query id -o tsv)
ME=$(az ad signed-in-user show --query id -o tsv)
az role assignment create \
  --role "Storage Blob Data Contributor" \
  --assignee-object-id "$ME" --assignee-principal-type User \
  --scope "/subscriptions/$SUB/resourceGroups/$TFSTATE_RG/providers/Microsoft.Storage/storageAccounts/$TFSTATE_SA"
```

If you change the names above, update [infra/backend.tf](../infra/backend.tf) to match.

### 1b. Entra app registrations

Automation needs `Application Administrator` at tenant scope to create these. If `terraform destroy` is run (to clean up resources and start new for example), these apps should remain:

| Registration | Purpose |
|---|---|
| `dispatch-api` | Exposes the `Job.ReadWrite` app role; the audience the API validates tokens against. |
| `dispatch-client-test` | Test client; granted `Job.ReadWrite` with admin consent. |

Capture the `dispatch-api` **Application (client) ID** and the **Tenant ID** — both go into `terraform.tfvars`.

### 1c. SQL Azure AD admin

Decide which user/group is the SQL AAD admin (you, typically). Capture their UPN and object ID:

```bash
az ad signed-in-user show --query "{login:userPrincipalName,id:id}"
```

For guest/external identities the UPN will include `#EXT#`. These values go into `sql_admin_login` and `sql_admin_object_id`.

## 2. Configure variables

Copy [infra/terraform.tfvars.example](../infra/terraform.tfvars.example) to `infra/terraform.tfvars` and fill in the required values: `sql_admin_login`, `sql_admin_object_id`, `entra_tenant_id`, `entra_api_client_id`, `admin_email`, `alpha_vantage_api_key`, `alpha_vantage_api_url`, `weather_api_url`, `acs_sender_display_name`. `location` currently defaults to `centralus`.

## 3. Init / plan / apply

```bash
cd infra
terraform init
terraform fmt -check
terraform validate
terraform plan -out tfplan
terraform apply tfplan
```

The apply creates `rg-dispatch` and all child resources (identities, SQL, Service Bus, ACS, storage, Function Apps, RBAC role assignments). Database-level T-SQL grants are a separate manual step — see §4.

> **Azure Resource Provider registration.** [infra/providers.tf](../infra/providers.tf) opts out of the default "register every known RP" behavior (which hangs on cold subscriptions) and explicitly registers only the 8 namespaces Dispatch uses. If you add a resource from a new namespace, extend `resource_providers_to_register`.

## 4. Post-deploy (required)

- **Add a temporary SQL firewall rule and grant MI identities database access.** The caller must be the SQL AAD admin (step 1c). Add your current IP, run the grants, then remove the rule:
  ```bash
  # Add firewall rule for your IP
  MY_IP=$(curl -s https://api.ipify.org)
  az sql server firewall-rule create \
    -g rg-dispatch -s sql-dispatch-server \
    -n temp-local --start-ip-address "$MY_IP" --end-ip-address "$MY_IP"

  # Grant managed identities db_datareader + db_datawriter
  sqlcmd -S sql-dispatch-server.database.windows.net -d Dispatch -G -Q "
    IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'api-dispatch')
      CREATE USER [api-dispatch] FROM EXTERNAL PROVIDER;
    ALTER ROLE db_datareader ADD MEMBER [api-dispatch];
    ALTER ROLE db_datawriter ADD MEMBER [api-dispatch];
    IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'uami-dispatch-worker')
      CREATE USER [uami-dispatch-worker] FROM EXTERNAL PROVIDER;
    ALTER ROLE db_datareader ADD MEMBER [uami-dispatch-worker];
    ALTER ROLE db_datawriter ADD MEMBER [uami-dispatch-worker];
  "

  # Remove temporary firewall rule
  az sql server firewall-rule delete \
    -g rg-dispatch -s sql-dispatch-server -n temp-local
  ```
  > In a team environment, these grants would run from a CI/CD pipeline using an Azure-hosted agent (already allowed by the `AllowAllWindowsAzureIps` firewall rule) or a VNET-integrated runner.

- **Grant yourself Blob Data Contributor on the new storage account.** The provider's `storage_use_azuread = true` setting makes data-plane calls go via AAD; without this grant your machine can't read/write blobs on the freshly created `storagedispatch`:
  ```bash
  SUB=$(az account show --query id -o tsv)
  ME=$(az ad signed-in-user show --query id -o tsv)
  az role assignment create \
    --role "Storage Blob Data Contributor" \
    --assignee-object-id "$ME" --assignee-principal-type User \
    --scope "/subscriptions/$SUB/resourceGroups/rg-dispatch/providers/Microsoft.Storage/storageAccounts/storagedispatch"
  ```
- **SQL serverless compute + storage.** Serverless config (GP_S_Gen5_1, min 0.5 vCores, auto-pause at 60 min) is managed by Terraform. `max_size_gb` is set to 1 GB for demo purposes. Adjust this as needed.
- **Entra app role assignments.** Assign `dispatch-client-test` (and any additional clients) to the `Job.ReadWrite` app role on `dispatch-api` in the portal.
- **EF Core database migration.** Terraform provisions the `Dispatch` database but does not create the schema. Apply EF Core migrations to create the tables (requires the temp SQL firewall rule from above if running locally). You'll need to run these whenever db schema changes require pushing to remote:
  ```bash
  # Set connection string for the session
  export DispatchConnection="Server=sql-dispatch-server.database.windows.net;Database=Dispatch;..."

  # Apply migrations to Azure SQL
  dotnet ef database update --project Data --startup-project Data
  ```

## 5. Post-deploy (informational)

- **Api / Function code deployments** are handled separately (via CI/CD or manually), not via Terraform. Refer to the `deploy (functions)` task and `deploy (api)` task in [task.json](../.vscode/tasks.json) for more details. You can run those tasks locally to deploy the api and the function app.
- **Email sender domain**. Terraform provisions the Azure-managed domain on the Email Communication Service plus a `noreply` sender username. The full sender address is exposed as the `acs_sender_address` output.
- **Azure-injected settings drift.** Azure auto-adds tags (e.g. `hidden-link: /app-insights-resource-id` on the API) and shuffles App Insights connection strings between `site_config` and `app_settings` on the Worker. Without `lifecycle { ignore_changes }`, these show up as phantom diffs on every plan. Both the API and Worker modules include `lifecycle` blocks to suppress this.
- **`AzureWebJobsStorage` injection.** If the storage account's `allowSharedKeyAccess` is toggled via the CLI or portal, Azure may auto-inject a bare `AzureWebJobsStorage` connection string (with an empty `AccountKey`) into the Function App's app settings. This overrides the identity-based `AzureWebJobsStorage__*` settings and causes `AuthenticationFailed` errors. Fix: delete the injected setting and avoid toggling storage account settings outside of Terraform.

## 6. Naming notes

- ACS and Email Communication Service are **global-only** Azure resource types. `location = "global"` is hardcoded in [modules/acs](../infra/modules/acs/), with `data_location = "United States"` for residency. Both names get a 4-character random suffix to dodge global name collisions.
- Every other resource uses the convention `{resource-type}-dispatch-{qualifier}` in the region from `var.location`.
- **App Service / Function App hostnames** (`api-dispatch.azurewebsites.net`, `func-dispatch-worker.azurewebsites.net`) must be globally unique. The current names use clean, human-readable values without a random suffix — this works for a demo project but could collide if someone else has claimed the same name. In a team/production setup, append a random suffix (as done for ACS) or front with a custom domain where the default hostname doesn't matter.

## 7. Teardown

```bash
cd infra
terraform destroy
```

Notes:
- **Entra app registrations and the bootstrap state account are not affected**. They survive teardowns by design.
- **ACS + Email Communication Service soft-delete for ~30 days.**. A subsequent apply with the same names will fail with "name already exists". The `random_string.acs_suffix` resource regenerates on re-apply (its `keepers` depend on the RG), so you'll get fresh names automatically. To purge sooner:
  ```bash
  az communication list-deleted -o table
  az rest --method delete --url "https://management.azure.com/subscriptions/$(az account show --query id -o tsv)/providers/Microsoft.Communication/locations/global/deletedCommunicationServices/<name>?api-version=2023-04-01"
  ```
- **Storage account names enter a short reservation window** after delete. If `terraform apply` immediately after `destroy` fails on name availability, wait 15–30 min and retry.
