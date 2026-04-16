variable "resource_group_name" { type = string }
variable "project" { type = string }
variable "tags" { type = map(string) }
variable "suffix" {
  description = "Random suffix appended to ACS + Email Communication Service names to avoid global collisions."
  type        = string
}
variable "sender_display_name" {
  description = "Display name shown on outgoing emails."
  type        = string
}
