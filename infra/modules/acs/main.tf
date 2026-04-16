# ACS and Email Communication Service are global-only resources by design.
resource "azurerm_email_communication_service" "main" {
  name                = "email-${var.project}-${var.suffix}"
  resource_group_name = var.resource_group_name
  data_location       = "United States"
  tags                = var.tags
}

resource "azurerm_email_communication_service_domain" "azure_managed" {
  name              = "AzureManagedDomain"
  email_service_id  = azurerm_email_communication_service.main.id
  domain_management = "AzureManaged"
  tags              = var.tags
}

resource "azurerm_communication_service" "main" {
  name                = "acs-${var.project}-${var.suffix}"
  resource_group_name = var.resource_group_name
  data_location       = "United States"
  tags                = var.tags
}

resource "azurerm_communication_service_email_domain_association" "main" {
  communication_service_id = azurerm_communication_service.main.id
  email_service_domain_id  = azurerm_email_communication_service_domain.azure_managed.id
}

resource "azurerm_email_communication_service_domain_sender_username" "noreply" {
  name                    = "noreply"
  email_service_domain_id = azurerm_email_communication_service_domain.azure_managed.id
  display_name            = var.sender_display_name
}
