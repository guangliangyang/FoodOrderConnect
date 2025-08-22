terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

# Redis Cache
resource "azurerm_redis_cache" "main" {
  name                = var.redis_cache_name
  location            = var.location
  resource_group_name = var.resource_group_name
  capacity            = var.capacity
  family              = var.family
  sku_name            = var.sku_name
  enable_non_ssl_port = var.enable_non_ssl_port
  minimum_tls_version = var.minimum_tls_version

  # Redis configuration
  redis_configuration {
    enable_authentication           = var.enable_authentication
    maxmemory_reserved             = var.maxmemory_reserved
    maxmemory_delta                = var.maxmemory_delta
    maxmemory_policy               = var.maxmemory_policy
    maxfragmentationmemory_reserved = var.maxfragmentationmemory_reserved
    rdb_backup_enabled             = var.rdb_backup_enabled
    rdb_backup_frequency           = var.rdb_backup_frequency
    rdb_backup_max_snapshot_count  = var.rdb_backup_max_snapshot_count
    rdb_storage_connection_string  = var.rdb_storage_connection_string
    notify_keyspace_events         = var.notify_keyspace_events
    aof_backup_enabled             = var.aof_backup_enabled
    aof_storage_connection_string_0 = var.aof_storage_connection_string_0
    aof_storage_connection_string_1 = var.aof_storage_connection_string_1
  }

  # Network and security
  public_network_access_enabled = var.public_network_access_enabled
  
  # Subnet configuration for Premium SKU
  subnet_id = var.subnet_id

  # Zones (Premium SKU only)
  zones = var.zones

  # Shard count (Premium SKU only)
  shard_count = var.shard_count

  # Replicas per master (Premium SKU only)
  replicas_per_master = var.replicas_per_master

  # Replicas per primary (Premium SKU only)
  replicas_per_primary = var.replicas_per_primary

  # Private endpoint (Premium SKU only)
  identity {
    type = var.identity_type
  }

  tags = var.tags
}

# Redis Firewall Rules
resource "azurerm_redis_firewall_rule" "rules" {
  for_each = var.firewall_rules

  name                = each.key
  redis_cache_name    = azurerm_redis_cache.main.name
  resource_group_name = var.resource_group_name
  start_ip            = each.value.start_ip
  end_ip              = each.value.end_ip
}

# Linked Server (Premium SKU only)
resource "azurerm_redis_linked_server" "linked" {
  count = var.linked_server_id != "" ? 1 : 0

  target_redis_cache_name     = var.linked_server_name
  resource_group_name         = var.resource_group_name
  linked_redis_cache_id       = var.linked_server_id
  linked_redis_cache_location = var.linked_server_location
  server_role                 = var.linked_server_role
}