resource "azurerm_mssql_server" "main" {
  name                          = "sql-${var.project}-server"
  resource_group_name           = var.resource_group_name
  location                      = var.location
  version                       = "12.0"
  minimum_tls_version           = "1.2"
  public_network_access_enabled = true

  azuread_administrator {
    login_username              = var.admin_login
    object_id                   = var.admin_object_id
    tenant_id                   = var.tenant_id
    azuread_authentication_only = true
  }

  identity {
    type = "SystemAssigned"
  }

  tags = var.tags
}

resource "azurerm_mssql_database" "main" {
  name                        = "Dispatch"
  server_id                   = azurerm_mssql_server.main.id
  sku_name                    = "GP_S_Gen5_1"
  min_capacity                = 0.5
  auto_pause_delay_in_minutes = 60
  max_size_gb                 = 1
  zone_redundant              = false
  storage_account_type        = "Local"
  tags                        = var.tags

  # NOTE: Serverless free-tier (useFreeLimit / freeLimitExhaustionBehavior) is not
  # yet exposed by azurerm. Enable via the portal post-apply — see Docs/provisioning.md.

}

# Allow Azure services (App Service / Function App outbound IPs) to reach the server.
resource "azurerm_mssql_firewall_rule" "allow_azure" {
  name             = "AllowAllWindowsAzureIps"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}
