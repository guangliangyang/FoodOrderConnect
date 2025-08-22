# Resource Group
output "resource_group_name" {
  description = "The name of the resource group"
  value       = data.azurerm_resource_group.main.name
}

output "location" {
  description = "The Azure region where resources are deployed"
  value       = var.location
}

# Storage Account
output "storage_account_name" {
  description = "The name of the storage account"
  value       = azurerm_storage_account.main.name
}

output "storage_account_primary_connection_string" {
  description = "The primary connection string for the storage account"
  value       = azurerm_storage_account.main.primary_connection_string
  sensitive   = true
}

# Container Registry
output "container_registry_name" {
  description = "The name of the container registry"
  value       = azurerm_container_registry.main.name
}

output "container_registry_login_server" {
  description = "The login server for the container registry"
  value       = azurerm_container_registry.main.login_server
}

output "container_registry_admin_username" {
  description = "The admin username for the container registry"
  value       = azurerm_container_registry.main.admin_username
}

output "container_registry_admin_password" {
  description = "The admin password for the container registry"
  value       = azurerm_container_registry.main.admin_password
  sensitive   = true
}

# Log Analytics and Application Insights
output "log_analytics_workspace_id" {
  description = "The ID of the Log Analytics workspace"
  value       = azurerm_log_analytics_workspace.main.id
}

output "application_insights_instrumentation_key" {
  description = "The instrumentation key for Application Insights"
  value       = azurerm_application_insights.main.instrumentation_key
  sensitive   = true
}

output "application_insights_connection_string" {
  description = "The connection string for Application Insights"
  value       = azurerm_application_insights.main.connection_string
  sensitive   = true
}

# Key Vault
output "key_vault_id" {
  description = "The ID of the Key Vault"
  value       = azurerm_key_vault.main.id
}

output "key_vault_uri" {
  description = "The URI of the Key Vault"
  value       = azurerm_key_vault.main.vault_uri
}

# Service Bus outputs
output "service_bus_namespace_name" {
  description = "The name of the Service Bus namespace"
  value       = module.service_bus.namespace_name
}

output "service_bus_primary_connection_string" {
  description = "The primary connection string for the Service Bus namespace"
  value       = module.service_bus.primary_connection_string
  sensitive   = true
}

output "service_bus_listen_send_connection_string" {
  description = "Connection string for listen and send operations"
  value       = module.service_bus.listen_send_connection_string
  sensitive   = true
}

output "service_bus_queue_names" {
  description = "List of created Service Bus queue names"
  value       = module.service_bus.queue_names
}

# SQL Server outputs
output "sql_server_name" {
  description = "The name of the SQL Server"
  value       = module.sql_server.server_name
}

output "sql_server_fqdn" {
  description = "The fully qualified domain name of the SQL Server"
  value       = module.sql_server.server_fqdn
}

output "sql_database_name" {
  description = "The name of the SQL Database"
  value       = module.sql_server.database_name
}

output "sql_connection_string" {
  description = "ADO.NET connection string for the database"
  value       = module.sql_server.connection_string
  sensitive   = true
}

# Cosmos DB outputs
output "cosmos_db_account_name" {
  description = "The name of the Cosmos DB account"
  value       = module.cosmos_db.account_name
}

output "cosmos_db_endpoint" {
  description = "The endpoint URL for the Cosmos DB account"
  value       = module.cosmos_db.endpoint
}

output "cosmos_db_primary_connection_string" {
  description = "Primary connection string for the Cosmos DB account"
  value       = module.cosmos_db.primary_connection_string
  sensitive   = true
}

output "cosmos_db_database_name" {
  description = "The name of the Cosmos DB SQL database"
  value       = module.cosmos_db.database_name
}

output "cosmos_db_container_names" {
  description = "List of created Cosmos DB container names"
  value       = module.cosmos_db.container_names
}

# Redis outputs
output "redis_cache_name" {
  description = "The name of the Redis Cache"
  value       = module.redis.name
}

output "redis_hostname" {
  description = "The hostname of the Redis instance"
  value       = module.redis.hostname
}

output "redis_ssl_port" {
  description = "The SSL port of the Redis instance"
  value       = module.redis.ssl_port
}

output "redis_primary_connection_string" {
  description = "The primary connection string for the Redis Cache"
  value       = module.redis.primary_connection_string
  sensitive   = true
}

# Common resource names for reference
output "resource_names" {
  description = "Map of all resource names"
  value = {
    resource_group_name         = data.azurerm_resource_group.main.name
    storage_account_name       = azurerm_storage_account.main.name
    container_registry_name    = azurerm_container_registry.main.name
    log_analytics_name         = azurerm_log_analytics_workspace.main.name
    app_insights_name          = azurerm_application_insights.main.name
    key_vault_name            = azurerm_key_vault.main.name
    service_bus_namespace_name = module.service_bus.namespace_name
    sql_server_name           = module.sql_server.server_name
    sql_database_name         = module.sql_server.database_name
    cosmos_db_account_name    = module.cosmos_db.account_name
    redis_cache_name          = module.redis.name
  }
}