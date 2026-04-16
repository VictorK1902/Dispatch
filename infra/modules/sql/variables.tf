variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "project" { type = string }
variable "tags" { type = map(string) }
variable "admin_login" {
  description = "Azure AD admin UPN for the SQL server."
  type        = string
}
variable "admin_object_id" {
  description = "Object ID of the Azure AD admin."
  type        = string
}
variable "tenant_id" {
  description = "Entra tenant ID."
  type        = string
}
