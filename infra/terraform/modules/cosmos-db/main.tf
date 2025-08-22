terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

# Cosmos DB Account
resource "azurerm_cosmosdb_account" "main" {
  name                = var.cosmos_account_name
  location            = var.location
  resource_group_name = var.resource_group_name
  offer_type          = "Standard"
  kind                = "GlobalDocumentDB"

  # Consistency policy
  consistency_policy {
    consistency_level       = var.consistency_level
    max_interval_in_seconds = var.max_interval_in_seconds
    max_staleness_prefix    = var.max_staleness_prefix
  }

  # Geo locations
  geo_location {
    location          = var.location
    failover_priority = 0
    zone_redundant    = var.zone_redundant
  }

  dynamic "geo_location" {
    for_each = var.additional_geo_locations
    content {
      location          = geo_location.value.location
      failover_priority = geo_location.value.failover_priority
      zone_redundant    = geo_location.value.zone_redundant
    }
  }

  # Capabilities
  dynamic "capabilities" {
    for_each = var.capabilities
    content {
      name = capabilities.value
    }
  }

  # Backup policy
  backup {
    type                = var.backup_type
    interval_in_minutes = var.backup_interval_in_minutes
    retention_in_hours  = var.backup_retention_in_hours
    storage_redundancy  = var.backup_storage_redundancy
  }

  # Free tier
  enable_free_tier = var.enable_free_tier

  # Security
  enable_automatic_failover = var.enable_automatic_failover
  enable_multiple_write_locations = var.enable_multiple_write_locations

  # Network restrictions
  public_network_access_enabled = var.public_network_access_enabled
  is_virtual_network_filter_enabled = var.enable_virtual_network_filter

  dynamic "virtual_network_rule" {
    for_each = var.virtual_network_rules
    content {
      id                                   = virtual_network_rule.value.subnet_id
      ignore_missing_vnet_service_endpoint = virtual_network_rule.value.ignore_missing_endpoint
    }
  }

  # IP range filter
  ip_range_filter = var.ip_range_filter

  tags = var.tags
}

# Cosmos DB SQL Database
resource "azurerm_cosmosdb_sql_database" "main" {
  name                = var.database_name
  resource_group_name = var.resource_group_name
  account_name        = azurerm_cosmosdb_account.main.name
  
  # Autoscale or manual throughput
  dynamic "autoscale_settings" {
    for_each = var.enable_autoscale ? [1] : []
    content {
      max_throughput = var.max_throughput
    }
  }
  
  throughput = var.enable_autoscale ? null : var.throughput
}

# Cosmos DB SQL Containers
resource "azurerm_cosmosdb_sql_container" "containers" {
  for_each = { for container in var.containers : container.name => container }

  name                  = each.value.name
  resource_group_name   = var.resource_group_name
  account_name          = azurerm_cosmosdb_account.main.name
  database_name         = azurerm_cosmosdb_sql_database.main.name
  partition_key_path    = each.value.partition_key_path
  partition_key_version = each.value.partition_key_version

  # Autoscale or manual throughput
  dynamic "autoscale_settings" {
    for_each = each.value.enable_autoscale ? [1] : []
    content {
      max_throughput = each.value.max_throughput
    }
  }
  
  throughput = each.value.enable_autoscale ? null : each.value.throughput

  # Indexing policy
  indexing_policy {
    indexing_mode = "consistent"

    dynamic "included_path" {
      for_each = each.value.included_paths
      content {
        path = included_path.value
      }
    }

    dynamic "excluded_path" {
      for_each = each.value.excluded_paths
      content {
        path = excluded_path.value
      }
    }

    dynamic "composite_index" {
      for_each = each.value.composite_indexes
      content {
        dynamic "index" {
          for_each = composite_index.value
          content {
            path  = index.value.path
            order = index.value.order
          }
        }
      }
    }
  }

  # Unique key policy
  dynamic "unique_key" {
    for_each = each.value.unique_key_paths
    content {
      paths = unique_key.value
    }
  }

  # TTL
  default_ttl = each.value.default_ttl

  # Conflict resolution policy
  conflict_resolution_policy {
    mode                     = each.value.conflict_resolution_mode
    conflict_resolution_path = each.value.conflict_resolution_path
    conflict_resolution_procedure = each.value.conflict_resolution_procedure
  }
}

# Stored procedures, triggers, and user-defined functions can be added here as needed