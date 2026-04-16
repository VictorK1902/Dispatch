# Remote state backend.
# The storage account, container, and RG below are created manually before
# `terraform init` — see Docs/provisioning.md.
terraform {
  backend "azurerm" {
    resource_group_name  = "rg-dispatch-tfstate"
    storage_account_name = "stdispatchtfstate"
    container_name       = "tfstate"
    key                  = "prod.terraform.tfstate"
    use_azuread_auth     = true
  }
}
