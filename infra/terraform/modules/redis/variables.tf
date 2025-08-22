variable "redis_cache_name" {
  description = "The name of the Redis cache"
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

variable "capacity" {
  description = "The size of the Redis cache to deploy"
  type        = number
  default     = 1
}

variable "family" {
  description = "The SKU family to use"
  type        = string
  default     = "C"
  validation {
    condition     = contains(["C", "P"], var.family)
    error_message = "Family must be either C (Basic/Standard) or P (Premium)."
  }
}

variable "sku_name" {
  description = "The SKU of Redis to use"
  type        = string
  default     = "Standard"
  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.sku_name)
    error_message = "SKU name must be one of: Basic, Standard, Premium."
  }
}

variable "enable_non_ssl_port" {
  description = "Enable the non-SSL port (6379)"
  type        = bool
  default     = false
}

variable "minimum_tls_version" {
  description = "The minimum TLS version"
  type        = string
  default     = "1.2"
  validation {
    condition     = contains(["1.0", "1.1", "1.2"], var.minimum_tls_version)
    error_message = "Minimum TLS version must be one of: 1.0, 1.1, 1.2."
  }
}

variable "enable_authentication" {
  description = "Enable Redis authentication"
  type        = bool
  default     = true
}

variable "maxmemory_reserved" {
  description = "Value in megabytes reserved for non-cache usage"
  type        = number
  default     = null
}

variable "maxmemory_delta" {
  description = "Value in megabytes reserved for non-cache usage per shard"
  type        = number
  default     = null
}

variable "maxmemory_policy" {
  description = "How Redis will select what to remove when maxmemory is reached"
  type        = string
  default     = "allkeys-lru"
}

variable "maxfragmentationmemory_reserved" {
  description = "Value in megabytes reserved to accommodate for memory fragmentation"
  type        = number
  default     = null
}

variable "rdb_backup_enabled" {
  description = "Is Backup Enabled for the Redis Cache?"
  type        = bool
  default     = false
}

variable "rdb_backup_frequency" {
  description = "The Backup Frequency in Minutes"
  type        = number
  default     = 60
}

variable "rdb_backup_max_snapshot_count" {
  description = "The maximum number of snapshots to create as a backup"
  type        = number
  default     = 1
}

variable "rdb_storage_connection_string" {
  description = "The Connection String to the Storage Account"
  type        = string
  default     = ""
  sensitive   = true
}

variable "notify_keyspace_events" {
  description = "Keyspace notifications allows clients to subscribe to Pub/Sub channels"
  type        = string
  default     = ""
}

variable "aof_backup_enabled" {
  description = "Enable or disable AOF persistence for this Redis Cache"
  type        = bool
  default     = false
}

variable "aof_storage_connection_string_0" {
  description = "First Storage Account connection string for AOF persistence"
  type        = string
  default     = ""
  sensitive   = true
}

variable "aof_storage_connection_string_1" {
  description = "Second Storage Account connection string for AOF persistence"
  type        = string
  default     = ""
  sensitive   = true
}

variable "public_network_access_enabled" {
  description = "Whether public network access is allowed for this Redis Cache"
  type        = bool
  default     = true
}

variable "subnet_id" {
  description = "The ID of the Subnet within which the Redis Cache should be deployed"
  type        = string
  default     = null
}

variable "zones" {
  description = "List of Availability Zones in which this Redis Cache should be located"
  type        = list(string)
  default     = null
}

variable "shard_count" {
  description = "Number of shards to create on a cluster mode cache"
  type        = number
  default     = null
}

variable "replicas_per_master" {
  description = "Number of replicas to create per master for this Redis Cache"
  type        = number
  default     = null
}

variable "replicas_per_primary" {
  description = "Number of replicas to create per primary for this Redis Cache"
  type        = number
  default     = null
}

variable "identity_type" {
  description = "The type of identity used for the Redis Cache"
  type        = string
  default     = "SystemAssigned"
}

variable "firewall_rules" {
  description = "Map of firewall rules to create"
  type = map(object({
    start_ip = string
    end_ip   = string
  }))
  default = {}
}

variable "linked_server_id" {
  description = "The ID of the linked Redis cache"
  type        = string
  default     = ""
}

variable "linked_server_name" {
  description = "The name of the linked Redis cache"
  type        = string
  default     = ""
}

variable "linked_server_location" {
  description = "The location of the linked Redis cache"
  type        = string
  default     = ""
}

variable "linked_server_role" {
  description = "The role of the linked Redis cache"
  type        = string
  default     = "Secondary"
  validation {
    condition     = contains(["Primary", "Secondary"], var.linked_server_role)
    error_message = "Linked server role must be either Primary or Secondary."
  }
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
}