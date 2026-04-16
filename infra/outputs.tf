output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "api_default_hostname" {
  value = module.api.default_hostname
}

output "worker_name" {
  value = module.worker.name
}

output "sql_server_fqdn" {
  value = module.sql.server_fqdn
}

output "servicebus_namespace" {
  value = module.servicebus.namespace_name
}

output "acs_name" {
  value = module.acs.communication_service_name
}

output "acs_sender_address" {
  value = module.acs.sender_address
}
