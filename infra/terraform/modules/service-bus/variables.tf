variable "service_bus_namespace_name" {
  description = "The name of the Service Bus namespace"
  type        = string
}

variable "location" {
  description = "Azure region for deployment"
  type        = string
}

variable "resource_group_name" {
  description = "The name of the resource group"
  type        = string
}

variable "queue_names" {
  description = "List of queue names to create"
  type        = list(string)
}

variable "zone_redundant" {
  description = "Whether the Service Bus namespace should be zone redundant"
  type        = bool
  default     = false
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
}