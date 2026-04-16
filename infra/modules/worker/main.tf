resource "azurerm_user_assigned_identity" "worker" {
  name                = "uami-${var.project}-worker"
  resource_group_name = var.resource_group_name
  location            = var.location
  tags                = var.tags
}

resource "azurerm_service_plan" "worker" {
  name                = "plan-${var.project}-worker"
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = "Linux"
  sku_name            = "FC1"
  tags                = var.tags
}

resource "azurerm_function_app_flex_consumption" "worker" {
  name                = "func-${var.project}-worker"
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.worker.id
  tags                = var.tags

  storage_container_type            = "blobContainer"
  storage_container_endpoint        = "https://${var.storage_account_name}.blob.core.windows.net/${azurerm_storage_container.deployments.name}"
  storage_authentication_type       = "UserAssignedIdentity"
  storage_user_assigned_identity_id = azurerm_user_assigned_identity.worker.id

  runtime_name    = "dotnet-isolated"
  runtime_version = "10.0"

  instance_memory_in_mb = 512

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.worker.id]
  }

  site_config {
    ip_restriction_default_action     = "Deny"
    scm_ip_restriction_default_action = "Allow"
  }

  app_settings = {
    APPLICATIONINSIGHTS_CONNECTION_STRING     = var.app_insights_connection_string
    APPLICATIONINSIGHTS_AUTHENTICATION_STRING = "ClientId=${azurerm_user_assigned_identity.worker.client_id};Authorization=AAD"
    AZURE_CLIENT_ID                           = azurerm_user_assigned_identity.worker.client_id

    "AzureWebJobsStorage__blobServiceUri"  = "https://${var.storage_account_name}.blob.core.windows.net"
    "AzureWebJobsStorage__queueServiceUri" = "https://${var.storage_account_name}.queue.core.windows.net"
    "AzureWebJobsStorage__tableServiceUri" = "https://${var.storage_account_name}.table.core.windows.net"
    "AzureWebJobsStorage__clientId"        = azurerm_user_assigned_identity.worker.client_id
    "AzureWebJobsStorage__credential"      = "managedidentity"

    "ServiceBusConnection__fullyQualifiedNamespace" = var.servicebus_namespace_endpoint
    "ServiceBusConnection__clientId"                = azurerm_user_assigned_identity.worker.client_id
    "ServiceBusConnection__credential"              = "managedidentity"

    AcsEndpoint        = var.acs_endpoint
    AcsSenderAddress   = var.acs_sender_address
    AdminEmail         = var.admin_email
    AlphaVantageApiKey = var.alpha_vantage_api_key
    AlphaVantageApiUrl = var.alpha_vantage_api_url
    WeatherApiUrl      = var.weather_api_url
  }

  connection_string {
    name  = "DispatchConnection"
    type  = "SQLAzure"
    value = var.sql_connection_string
  }

  lifecycle {
    ignore_changes = [
      tags["hidden-link: /app-insights-resource-id"],
      app_settings["APPLICATIONINSIGHTS_CONNECTION_STRING"],
      app_settings["AzureWebJobsStorage"],
      site_config[0].application_insights_connection_string,
    ]
  }
}

resource "azurerm_storage_container" "deployments" {
  name                  = "deployments"
  storage_account_id    = var.storage_account_id
  container_access_type = "private"
}
