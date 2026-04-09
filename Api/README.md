# Notes

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
    "DispatchConnection": "<no-secret-sql-connection-string-with-mi>",
    "ServiceBus": "<sb-namespace>"
  },
  "ServiceBus": {
    "QueueName": "<job-queue-name>"
  }
}

```

# Retrieve OAuth 2.0 Access Token

Replace values appropriately.

```bash
curl -X POST https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token \
  -d "client_id={EntraClientAppClientId}" \
  -d "client_secret={EntraClientAppClientSecret}" \
  -d "scope={EntraApiAppExposedUri}/.default" \
  -d "grant_type=client_credentials"
```

Sample Response

```json
{
  "token_type": "Bearer",
  "expires_in": 3599,
  "ext_expires_in": 3599,
  "access_token": "<AccessToken>"
}
```

# Making API Call

Include the following header when making request to the API

```
Authorization: Bearer <AccessToken>
```