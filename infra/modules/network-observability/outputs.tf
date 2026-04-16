output "api_app_insights_id" {
  value = azurerm_application_insights.api.id
}

output "worker_app_insights_id" {
  value = azurerm_application_insights.worker.id
}

output "api_app_insights_connection_string" {
  value     = azurerm_application_insights.api.connection_string
  sensitive = true
}

output "worker_app_insights_connection_string" {
  value     = azurerm_application_insights.worker.connection_string
  sensitive = true
}

output "log_analytics_workspace_id" {
  value = azurerm_log_analytics_workspace.main.id
}
