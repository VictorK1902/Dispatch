# API (system-assigned MI) → Service Bus sender
resource "azurerm_role_assignment" "api_sb_sender" {
  scope                = var.servicebus_namespace_id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = var.api_principal_id
}

# API → App Insights metrics publisher
resource "azurerm_role_assignment" "api_appinsights" {
  scope                = var.api_app_insights_id
  role_definition_name = "Monitoring Metrics Publisher"
  principal_id         = var.api_principal_id
}

# Worker UAMI → Service Bus receiver
resource "azurerm_role_assignment" "worker_sb_receiver" {
  scope                = var.servicebus_namespace_id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = var.worker_principal_id
}

# Worker UAMI → Storage (runtime + deployment container)
resource "azurerm_role_assignment" "worker_storage_owner" {
  scope                = var.storage_account_id
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = var.worker_principal_id
}

# Worker UAMI → Storage (queues — Functions runtime lease management)
resource "azurerm_role_assignment" "worker_storage_queue" {
  scope                = var.storage_account_id
  role_definition_name = "Storage Queue Data Contributor"
  principal_id         = var.worker_principal_id
}

# Worker UAMI → Storage (tables — Functions runtime timers/checkpoints)
resource "azurerm_role_assignment" "worker_storage_table" {
  scope                = var.storage_account_id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = var.worker_principal_id
}

# Worker UAMI → Storage (account-level ops — Functions runtime needs this for key enumeration)
resource "azurerm_role_assignment" "worker_storage_account" {
  scope                = var.storage_account_id
  role_definition_name = "Storage Account Contributor"
  principal_id         = var.worker_principal_id
}

# Worker UAMI → App Insights
resource "azurerm_role_assignment" "worker_appinsights" {
  scope                = var.worker_app_insights_id
  role_definition_name = "Monitoring Metrics Publisher"
  principal_id         = var.worker_principal_id
}

# Worker UAMI → ACS (email send)
resource "azurerm_role_assignment" "worker_acs" {
  scope                = var.acs_id
  role_definition_name = "Communication and Email Service Owner"
  principal_id         = var.worker_principal_id
}
