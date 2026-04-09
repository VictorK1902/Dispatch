# Notes

Ensure the following app settings and connection strings are created via Azure portal.

```
{
  "IsEncrypted": false,
  "Values": {
    "ServiceBusConnection__fullyQualifiedNamespace": "<sb-namespace>",
    "AcsEndpoint": "<full-acs-endpoint>",
    "AcsSenderAddress": "<email-using-domain-linked-to-acs>",
    "AdminEmail": "<used-when-notif-sent-in-dlq-handler>",
    "AlphaVantageApiKey": "<required-for-stock-api>",
    "AlphaVantageApiUrl": "https://www.alphavantage.co",
    "WeatherApiUrl": "https://api.open-meteo.com"
  },
  "ConnectionStrings": {
    "DispatchConnection": "<no-secret-sql-connection-string-with-mi>"
  }
}
```