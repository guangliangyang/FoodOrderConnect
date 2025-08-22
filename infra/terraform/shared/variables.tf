variable "resource_group_name" {
  description = "The name of the resource group where resources will be created"
  type        = string
}

variable "environment_name" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment_name)
    error_message = "Environment name must be one of: dev, staging, prod."
  }
}

variable "location" {
  description = "Azure region for deployment"
  type        = string
  default     = "East US"
}

variable "unique_suffix" {
  description = "Unique suffix for resource names"
  type        = string
  default     = null
}

variable "sql_admin_login" {
  description = "SQL Server administrator login"
  type        = string
  default     = "sqladmin"
}

variable "sql_admin_password" {
  description = "SQL Server administrator password"
  type        = string
  sensitive   = true
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "bidone"
}

variable "tags" {
  description = "Common tags to apply to all resources"
  type        = map(string)
  default     = {}
}

# Service Bus Configuration
variable "service_bus_queue_names" {
  description = "List of Service Bus queue names to create"
  type        = list(string)
  default = [
    "order-received",
    "order-validated",
    "order-enriched",
    "order-processing",  # New queue for bridge function
    "order-confirmed",
    "order-failed",
    "high-value-errors"
  ]
}

# Event Grid Configuration
variable "event_grid_event_types" {
  description = "List of Event Grid event types"
  type        = list(string)
  default = [
    "BidOne.Dashboard.OrderMetrics",
    "BidOne.Dashboard.PerformanceAlert",
    "BidOne.Dashboard.SystemHealth"
  ]
}

# Cosmos DB Configuration
variable "cosmos_db_containers" {
  description = "List of Cosmos DB containers to create"
  type = list(object({
    name          = string
    partition_key = string
  }))
  default = [
    {
      name          = "Products"
      partition_key = "/category"
    },
    {
      name          = "CustomerProfiles"
      partition_key = "/customerId"
    },
    {
      name          = "OrderEvents"
      partition_key = "/orderId"
    }
  ]
}

# Environment-specific configurations
variable "environment_configs" {
  description = "Environment-specific configuration settings"
  type = map(object({
    sql_sku_name                = string
    sql_sku_tier                = string
    sql_max_size_gb            = number
    sql_zone_redundant         = bool
    sql_read_scale             = bool
    sql_auto_pause_delay       = number
    apim_sku_name              = string
    apim_sku_capacity          = number
    function_app_sku_name      = string
    function_app_sku_tier      = string
    redis_sku_name             = string
    redis_sku_family           = string
    redis_sku_capacity         = number
    container_registry_sku     = string
    log_analytics_retention    = number
    cosmos_enable_free_tier    = bool
    cosmos_backup_interval     = number
    cosmos_backup_retention    = number
    service_bus_zone_redundant = bool
  }))
  default = {
    dev = {
      sql_sku_name                = "S1"
      sql_sku_tier                = "Standard"
      sql_max_size_gb            = 100
      sql_zone_redundant         = false
      sql_read_scale             = false
      sql_auto_pause_delay       = 60
      apim_sku_name              = "Developer"
      apim_sku_capacity          = 1
      function_app_sku_name      = "Y1"
      function_app_sku_tier      = "Dynamic"
      redis_sku_name             = "Basic"
      redis_sku_family           = "C"
      redis_sku_capacity         = 1
      container_registry_sku     = "Standard"
      log_analytics_retention    = 30
      cosmos_enable_free_tier    = true
      cosmos_backup_interval     = 1440
      cosmos_backup_retention    = 168
      service_bus_zone_redundant = false
    }
    staging = {
      sql_sku_name                = "S2"
      sql_sku_tier                = "Standard"
      sql_max_size_gb            = 200
      sql_zone_redundant         = false
      sql_read_scale             = false
      sql_auto_pause_delay       = -1
      apim_sku_name              = "Standard"
      apim_sku_capacity          = 1
      function_app_sku_name      = "EP1"
      function_app_sku_tier      = "ElasticPremium"
      redis_sku_name             = "Standard"
      redis_sku_family           = "C"
      redis_sku_capacity         = 1
      container_registry_sku     = "Standard"
      log_analytics_retention    = 60
      cosmos_enable_free_tier    = false
      cosmos_backup_interval     = 720
      cosmos_backup_retention    = 360
      service_bus_zone_redundant = false
    }
    prod = {
      sql_sku_name                = "S2"
      sql_sku_tier                = "Standard"
      sql_max_size_gb            = 250
      sql_zone_redundant         = true
      sql_read_scale             = true
      sql_auto_pause_delay       = -1
      apim_sku_name              = "Standard"
      apim_sku_capacity          = 2
      function_app_sku_name      = "EP1"
      function_app_sku_tier      = "ElasticPremium"
      redis_sku_name             = "Standard"
      redis_sku_family           = "C"
      redis_sku_capacity         = 2
      container_registry_sku     = "Premium"
      log_analytics_retention    = 90
      cosmos_enable_free_tier    = false
      cosmos_backup_interval     = 240
      cosmos_backup_retention    = 720
      service_bus_zone_redundant = true
    }
  }
}