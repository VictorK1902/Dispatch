Ensure the following app settings and connection strings are created via Azure portal. Ensure double underscore `__` is used for nested config (i.e. `AzureAd__TenantId`).

```
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant-id-of-the-api-app>",
    "ClientId": "<client-id-of-the-api-app>",
    "Audience": "<exposed-app-id-uri>"
  },
  "ConnectionStrings": {
    "DispatchConnection": "<no-secret-sql-connection-string>",
    "ServiceBus": "<sb-namespace>"
  },
  "ServiceBus": {
    "QueueName": "<job-queue-name>"
  }
}

```