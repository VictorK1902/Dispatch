resource "azurerm_service_plan" "main" {
  name                = "plan-${var.project}-api"
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = "Linux"
  sku_name            = "B1"
  tags                = var.tags
}

resource "azurerm_linux_web_app" "main" {
  name                = "api-${var.project}"
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.main.id
  https_only          = true
  tags                = var.tags

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version = "10.0"
    }
    always_on           = true
    ftps_state          = "Disabled"
    http2_enabled       = true
    minimum_tls_version = "1.2"
  }

  app_settings = {
    APPLICATIONINSIGHTS_CONNECTION_STRING      = var.app_insights_connection_string
    ApplicationInsightsAgent_EXTENSION_VERSION = "~3"
    XDT_MicrosoftApplicationInsights_Mode      = "Recommended"
    ASPNETCORE_ENVIRONMENT                     = "Production"

    "AzureAd__TenantId" = var.entra_tenant_id
    "AzureAd__ClientId" = var.entra_api_client_id
    "AzureAd__Audience" = "api://${var.entra_api_client_id}"
    "AzureAd__Instance" = "https://login.microsoftonline.com/"

    "ServiceBus__QueueName" = "jobs-queue"
  }

  connection_string {
    name  = "DispatchConnection"
    type  = "SQLAzure"
    value = var.sql_connection_string
  }

  connection_string {
    name  = "ServiceBus"
    type  = "Custom"
    value = var.servicebus_namespace_endpoint
  }

  lifecycle {
    ignore_changes = [tags["hidden-link: /app-insights-resource-id"]]
  }
}
