variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "project" { type = string }
variable "tags" { type = map(string) }

variable "storage_account_name" { type = string }
variable "storage_account_id" { type = string }
variable "app_insights_connection_string" {
  type      = string
  sensitive = true
}
variable "servicebus_namespace_endpoint" { type = string }
variable "sql_connection_string" {
  type      = string
  sensitive = true
}
variable "acs_endpoint" { type = string }
variable "acs_sender_address" { type = string }
variable "admin_email" { type = string }
variable "alpha_vantage_api_key" {
  type      = string
  sensitive = true
}
variable "alpha_vantage_api_url" { type = string }
variable "weather_api_url" { type = string }
