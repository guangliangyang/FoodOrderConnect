variable "cosmos_account_name" {
  description = "The name of the Cosmos DB account"
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

variable "database_name" {
  description = "The name of the Cosmos DB SQL database"
  type        = string
  default     = "BidOneDB"
}

variable "consistency_level" {
  description = "The consistency level for the Cosmos DB account"
  type        = string
  default     = "Session"
  validation {
    condition     = contains(["Eventual", "Session", "BoundedStaleness", "Strong", "ConsistentPrefix"], var.consistency_level)
    error_message = "Consistency level must be one of: Eventual, Session, BoundedStaleness, Strong, ConsistentPrefix."
  }
}

variable "max_interval_in_seconds" {
  description = "The time window for staleness when using BoundedStaleness consistency"
  type        = number
  default     = 10
}

variable "max_staleness_prefix" {
  description = "The staleness prefix when using BoundedStaleness consistency"
  type        = number
  default     = 200
}

variable "zone_redundant" {
  description = "Whether the primary region should be zone redundant"
  type        = bool
  default     = false
}

variable "additional_geo_locations" {
  description = "Additional geo locations for the Cosmos DB account"
  type = list(object({
    location          = string
    failover_priority = number
    zone_redundant    = bool
  }))
  default = []
}

variable "capabilities" {
  description = "List of capabilities to enable on the Cosmos DB account"
  type        = list(string)
  default     = []
}

variable "backup_type" {
  description = "The type of backup for the Cosmos DB account"
  type        = string
  default     = "Periodic"
  validation {
    condition     = contains(["Periodic", "Continuous"], var.backup_type)
    error_message = "Backup type must be either Periodic or Continuous."
  }
}

variable "backup_interval_in_minutes" {
  description = "The backup interval in minutes for periodic backup"
  type        = number
  default     = 240
}

variable "backup_retention_in_hours" {
  description = "The backup retention in hours for periodic backup"
  type        = number
  default     = 720
}

variable "backup_storage_redundancy" {
  description = "The storage redundancy for backup"
  type        = string
  default     = "Geo"
  validation {
    condition     = contains(["Geo", "Local", "Zone"], var.backup_storage_redundancy)
    error_message = "Backup storage redundancy must be one of: Geo, Local, Zone."
  }
}

variable "enable_free_tier" {
  description = "Whether to enable the free tier"
  type        = bool
  default     = false
}

variable "enable_automatic_failover" {
  description = "Whether to enable automatic failover"
  type        = bool
  default     = true
}

variable "enable_multiple_write_locations" {
  description = "Whether to enable multiple write locations"
  type        = bool
  default     = false
}

variable "public_network_access_enabled" {
  description = "Whether public network access is allowed"
  type        = bool
  default     = true
}

variable "enable_virtual_network_filter" {
  description = "Whether to enable virtual network filtering"
  type        = bool
  default     = false
}

variable "virtual_network_rules" {
  description = "List of virtual network rules"
  type = list(object({
    subnet_id              = string
    ignore_missing_endpoint = bool
  }))
  default = []
}

variable "ip_range_filter" {
  description = "IP range filter for the Cosmos DB account"
  type        = string
  default     = ""
}

variable "enable_autoscale" {
  description = "Whether to enable autoscale for the database"
  type        = bool
  default     = false
}

variable "throughput" {
  description = "The throughput for the database (when autoscale is disabled)"
  type        = number
  default     = 400
}

variable "max_throughput" {
  description = "The maximum throughput for autoscale"
  type        = number
  default     = 4000
}

variable "containers" {
  description = "List of containers to create"
  type = list(object({
    name                    = string
    partition_key_path      = string
    partition_key_version   = optional(number, 1)
    enable_autoscale       = optional(bool, false)
    throughput             = optional(number, 400)
    max_throughput         = optional(number, 4000)
    included_paths         = optional(list(string), ["/*"])
    excluded_paths         = optional(list(string), [])
    composite_indexes      = optional(list(list(object({
      path  = string
      order = string
    }))), [])
    unique_key_paths       = optional(list(list(string)), [])
    default_ttl           = optional(number, null)
    conflict_resolution_mode = optional(string, "LastWriterWins")
    conflict_resolution_path = optional(string, "/_ts")
    conflict_resolution_procedure = optional(string, "")
  }))
  default = []
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
}