output "server_id" {
  value = azurerm_mssql_server.main.id
}

output "server_name" {
  value = azurerm_mssql_server.main.name
}

output "server_fqdn" {
  value = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "database_name" {
  value = azurerm_mssql_database.main.name
}

output "connection_string" {
  description = "ADO.NET-style connection string using Active Directory Default auth (for managed identity)."
  value       = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.main.name};Encrypt=True;TrustServerCertificate=False;Authentication=Active Directory Default;"
  sensitive   = true
}
