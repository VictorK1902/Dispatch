output "name" {
  value = azurerm_linux_web_app.main.name
}

output "default_hostname" {
  value = azurerm_linux_web_app.main.default_hostname
}

output "principal_id" {
  description = "System-assigned MI principal ID."
  value       = azurerm_linux_web_app.main.identity[0].principal_id
}
