output "id" {
  description = "The ID of the Redis Cache"
  value       = azurerm_redis_cache.main.id
}

output "name" {
  description = "The name of the Redis Cache"
  value       = azurerm_redis_cache.main.name
}

output "hostname" {
  description = "The hostname of the Redis instance"
  value       = azurerm_redis_cache.main.hostname
}

output "ssl_port" {
  description = "The SSL port of the Redis instance"
  value       = azurerm_redis_cache.main.ssl_port
}

output "port" {
  description = "The non-SSL port of the Redis instance"
  value       = azurerm_redis_cache.main.port
}

output "primary_access_key" {
  description = "The primary access key for the Redis Cache"
  value       = azurerm_redis_cache.main.primary_access_key
  sensitive   = true
}

output "secondary_access_key" {
  description = "The secondary access key for the Redis Cache"
  value       = azurerm_redis_cache.main.secondary_access_key
  sensitive   = true
}

output "primary_connection_string" {
  description = "The primary connection string for the Redis Cache"
  value       = azurerm_redis_cache.main.primary_connection_string
  sensitive   = true
}

output "secondary_connection_string" {
  description = "The secondary connection string for the Redis Cache"
  value       = azurerm_redis_cache.main.secondary_connection_string
  sensitive   = true
}

output "redis_version" {
  description = "The version of Redis running on the cache"
  value       = azurerm_redis_cache.main.redis_version
}

output "private_static_ip_address" {
  description = "The static IP address assigned to the Redis Cache when hosted inside the Virtual Network"
  value       = azurerm_redis_cache.main.private_static_ip_address
}