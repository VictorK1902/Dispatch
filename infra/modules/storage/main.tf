# Function App runtime storage account.
# Name must be globally unique, lowercase, 3-24 chars, no dashes.
resource "azurerm_storage_account" "main" {
  name                             = "storage${var.project}"
  resource_group_name              = var.resource_group_name
  location                         = var.location
  account_tier                     = "Standard"
  account_replication_type         = "LRS"
  min_tls_version                  = "TLS1_2"
  allow_nested_items_to_be_public  = false
  tags                             = var.tags
  shared_access_key_enabled        = false
  default_to_oauth_authentication  = true
  cross_tenant_replication_enabled = false
}
