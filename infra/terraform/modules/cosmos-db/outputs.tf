output "account_id" {
  description = "The ID of the Cosmos DB account"
  value       = azurerm_cosmosdb_account.main.id
}

output "account_name" {
  description = "The name of the Cosmos DB account"
  value       = azurerm_cosmosdb_account.main.name
}

output "endpoint" {
  description = "The endpoint URL for the Cosmos DB account"
  value       = azurerm_cosmosdb_account.main.endpoint
}

output "read_endpoints" {
  description = "List of read endpoints for the Cosmos DB account"
  value       = azurerm_cosmosdb_account.main.read_endpoints
}

output "write_endpoints" {
  description = "List of write endpoints for the Cosmos DB account"
  value       = azurerm_cosmosdb_account.main.write_endpoints
}

output "primary_key" {
  description = "The primary key for the Cosmos DB account"
  value       = azurerm_cosmosdb_account.main.primary_key
  sensitive   = true
}

output "secondary_key" {
  description = "The secondary key for the Cosmos DB account"
  value       = azurerm_cosmosdb_account.main.secondary_key
  sensitive   = true
}

output "primary_readonly_key" {
  description = "The primary readonly key for the Cosmos DB account"
  value       = azurerm_cosmosdb_account.main.primary_readonly_key
  sensitive   = true
}

output "secondary_readonly_key" {
  description = "The secondary readonly key for the Cosmos DB account"
  value       = azurerm_cosmosdb_account.main.secondary_readonly_key
  sensitive   = true
}

output "connection_strings" {
  description = "List of connection strings for the Cosmos DB account"
  value       = azurerm_cosmosdb_account.main.connection_strings
  sensitive   = true
}

output "database_id" {
  description = "The ID of the Cosmos DB SQL database"
  value       = azurerm_cosmosdb_sql_database.main.id
}

output "database_name" {
  description = "The name of the Cosmos DB SQL database"
  value       = azurerm_cosmosdb_sql_database.main.name
}

output "container_ids" {
  description = "Map of container names to their IDs"
  value       = { for k, v in azurerm_cosmosdb_sql_container.containers : k => v.id }
}

output "container_names" {
  description = "List of created container names"
  value       = [for container in azurerm_cosmosdb_sql_container.containers : container.name]
}

# Connection string format for applications
output "primary_connection_string" {
  description = "Primary connection string for the Cosmos DB account"
  value       = "AccountEndpoint=${azurerm_cosmosdb_account.main.endpoint};AccountKey=${azurerm_cosmosdb_account.main.primary_key};"
  sensitive   = true
}

output "readonly_connection_string" {
  description = "Readonly connection string for the Cosmos DB account"
  value       = "AccountEndpoint=${azurerm_cosmosdb_account.main.endpoint};AccountKey=${azurerm_cosmosdb_account.main.primary_readonly_key};"
  sensitive   = true
}