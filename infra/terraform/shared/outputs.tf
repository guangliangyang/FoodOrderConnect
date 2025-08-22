output "resource_group_name" {
  description = "The name of the resource group"
  value       = data.azurerm_resource_group.main.name
}

output "service_bus_namespace_name" {
  description = "The name of the Service Bus namespace"
  value       = local.resource_names.service_bus_namespace_name
}

output "sql_server_name" {
  description = "The name of the SQL Server"
  value       = local.resource_names.sql_server_name
}

output "sql_database_name" {
  description = "The name of the SQL database"
  value       = "BidOneDB"
}

output "cosmos_db_account_name" {
  description = "The name of the Cosmos DB account"
  value       = local.resource_names.cosmos_db_account_name
}

output "container_registry_name" {
  description = "The name of the container registry"
  value       = local.resource_names.container_registry_name
}

output "key_vault_name" {
  description = "The name of the Key Vault"
  value       = local.resource_names.key_vault_name
}

output "app_insights_name" {
  description = "The name of Application Insights"
  value       = local.resource_names.app_insights_name
}

output "log_analytics_workspace_name" {
  description = "The name of the Log Analytics workspace"
  value       = local.resource_names.log_analytics_name
}

output "container_apps_environment_name" {
  description = "The name of the Container Apps environment"
  value       = local.resource_names.container_apps_env_name
}

output "api_management_service_name" {
  description = "The name of the API Management service"
  value       = local.resource_names.apim_service_name
}

output "function_app_name" {
  description = "The name of the Function App"
  value       = local.resource_names.function_app_name
}

output "redis_cache_name" {
  description = "The name of the Redis cache"
  value       = local.resource_names.redis_cache_name
}

output "logic_app_name" {
  description = "The name of the Logic App"
  value       = local.resource_names.logic_app_name
}

output "event_grid_topic_name" {
  description = "The name of the Event Grid topic"
  value       = local.resource_names.event_grid_topic_name
}

# Data source for resource group (assuming it already exists)
data "azurerm_resource_group" "main" {
  name = var.resource_group_name
}