output "communication_service_id" {
  value = azurerm_communication_service.main.id
}

output "communication_service_name" {
  value = azurerm_communication_service.main.name
}

output "primary_connection_string" {
  value     = azurerm_communication_service.main.primary_connection_string
  sensitive = true
}

output "endpoint" {
  value = "https://${azurerm_communication_service.main.name}.unitedstates.communication.azure.com"
}

output "sender_address" {
  value = "${azurerm_email_communication_service_domain_sender_username.noreply.name}@${azurerm_email_communication_service_domain.azure_managed.mail_from_sender_domain}"
}
