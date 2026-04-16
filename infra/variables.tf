variable "location" {
  description = "Azure region for all regional resources."
  type        = string
  default     = "centralus"
}

variable "resource_group_name" {
  description = "Resource group that will hold all Dispatch resources."
  type        = string
  default     = "rg-dispatch"
}

variable "project" {
  description = "Short project identifier used in resource names."
  type        = string
  default     = "dispatch"
}

variable "tags" {
  description = "Tags applied to every resource."
  type        = map(string)
  default = {
    project     = "dispatch"
    environment = "prod"
    managed_by  = "terraform"
  }
}

variable "sql_admin_login" {
  description = "Azure AD admin login (UPN) for the SQL server."
  type        = string
}

variable "sql_admin_object_id" {
  description = "Object ID of the Azure AD admin for the SQL server."
  type        = string
}

variable "entra_tenant_id" {
  description = "Entra tenant ID the API authenticates against."
  type        = string
}

variable "entra_api_client_id" {
  description = "Client (application) ID of the dispatch-api Entra app registration."
  type        = string
}

variable "acs_sender_display_name" {
  description = "From-display name used when sending email via ACS."
  type        = string
  default     = "Dispatch Notifications"
}

variable "admin_email" {
  description = "Admin recipient email for worker notifications."
  type        = string
}

variable "alpha_vantage_api_key" {
  description = "API key for Alpha Vantage market data."
  type        = string
  sensitive   = true
}

variable "alpha_vantage_api_url" {
  description = "Base URL for Alpha Vantage API."
  type        = string
  default     = "https://www.alphavantage.co"
}

variable "weather_api_url" {
  description = "Base URL for Open-Meteo weather API."
  type        = string
  default     = "https://api.open-meteo.com"
}
