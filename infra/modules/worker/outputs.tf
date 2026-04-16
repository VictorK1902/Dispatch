output "name" {
  value = azurerm_function_app_flex_consumption.worker.name
}

output "identity_id" {
  value = azurerm_user_assigned_identity.worker.id
}

output "identity_name" {
  value = azurerm_user_assigned_identity.worker.name
}

output "identity_principal_id" {
  value = azurerm_user_assigned_identity.worker.principal_id
}
