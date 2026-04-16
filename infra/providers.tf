terraform {
  required_version = ">= 1.6.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

provider "azurerm" {
  features {
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
  }

  storage_use_azuread = true

  resource_provider_registrations = "core"
  resource_providers_to_register = [
    "Microsoft.Communication",
    "Microsoft.Insights",
    "Microsoft.OperationalInsights",
    "Microsoft.ServiceBus",
    "Microsoft.Sql",
    "Microsoft.Storage",
    "Microsoft.Web",
    "Microsoft.Network",
  ]
}
