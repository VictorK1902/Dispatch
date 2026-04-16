resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location
  tags     = var.tags
}

resource "random_string" "acs_suffix" {
  length  = 4
  numeric = true
  lower   = true
  upper   = false
  special = false

  keepers = {
    resource_group = azurerm_resource_group.main.name
  }
}

module "observability" {
  source              = "./modules/network-observability"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  project             = var.project
  tags                = var.tags
}

module "storage" {
  source              = "./modules/storage"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  project             = var.project
  tags                = var.tags
}

module "servicebus" {
  source              = "./modules/servicebus"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  project             = var.project
  tags                = var.tags
}

module "sql" {
  source              = "./modules/sql"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  project             = var.project
  tags                = var.tags
  admin_login         = var.sql_admin_login
  admin_object_id     = var.sql_admin_object_id
  tenant_id           = var.entra_tenant_id
}

module "acs" {
  source              = "./modules/acs"
  resource_group_name = azurerm_resource_group.main.name
  project             = var.project
  suffix              = random_string.acs_suffix.result
  tags                = var.tags
  sender_display_name = var.acs_sender_display_name
}

module "api" {
  source              = "./modules/api"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  project             = var.project
  tags                = var.tags

  app_insights_connection_string = module.observability.api_app_insights_connection_string
  entra_tenant_id                = var.entra_tenant_id
  entra_api_client_id            = var.entra_api_client_id
  servicebus_namespace_endpoint  = module.servicebus.namespace_endpoint
  sql_connection_string          = module.sql.connection_string
}

module "worker" {
  source              = "./modules/worker"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  project             = var.project
  tags                = var.tags

  storage_account_name           = module.storage.account_name
  storage_account_id             = module.storage.account_id
  app_insights_connection_string = module.observability.worker_app_insights_connection_string
  servicebus_namespace_endpoint  = module.servicebus.namespace_endpoint
  sql_connection_string          = module.sql.connection_string
  acs_endpoint                   = module.acs.endpoint
  acs_sender_address             = module.acs.sender_address
  admin_email                    = var.admin_email
  alpha_vantage_api_key          = var.alpha_vantage_api_key
  alpha_vantage_api_url          = var.alpha_vantage_api_url
  weather_api_url                = var.weather_api_url
}

module "rbac" {
  source = "./modules/rbac"

  api_principal_id    = module.api.principal_id
  worker_identity_id  = module.worker.identity_id
  worker_principal_id = module.worker.identity_principal_id

  servicebus_namespace_id = module.servicebus.namespace_id
  storage_account_id      = module.storage.account_id
  acs_id                  = module.acs.communication_service_id
  api_app_insights_id     = module.observability.api_app_insights_id
  worker_app_insights_id  = module.observability.worker_app_insights_id
}
