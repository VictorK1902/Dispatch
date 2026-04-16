resource "azurerm_servicebus_namespace" "main" {
  name                = "sbns-${var.project}"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "Standard"
  local_auth_enabled  = false
  tags                = var.tags
}

resource "azurerm_servicebus_queue" "jobs" {
  name         = "jobs-queue"
  namespace_id = azurerm_servicebus_namespace.main.id

  dead_lettering_on_message_expiration = true
  max_delivery_count                   = 10
  lock_duration                        = "PT1M"
  default_message_ttl                  = "P14D"
}
