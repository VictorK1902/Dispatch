output "namespace_id" {
  value = azurerm_servicebus_namespace.main.id
}

output "namespace_name" {
  value = azurerm_servicebus_namespace.main.name
}

output "namespace_endpoint" {
  value = "${azurerm_servicebus_namespace.main.name}.servicebus.windows.net"
}

