variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "project" { type = string }
variable "tags" { type = map(string) }

variable "app_insights_connection_string" {
  type      = string
  sensitive = true
}
variable "entra_tenant_id" { type = string }
variable "entra_api_client_id" { type = string }
variable "servicebus_namespace_endpoint" { type = string }
variable "sql_connection_string" {
  type      = string
  sensitive = true
}
