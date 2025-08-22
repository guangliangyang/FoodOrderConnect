output "namespace_id" {
  description = "The ID of the Service Bus namespace"
  value       = azurerm_servicebus_namespace.main.id
}

output "namespace_name" {
  description = "The name of the Service Bus namespace"
  value       = azurerm_servicebus_namespace.main.name
}

output "primary_connection_string" {
  description = "The primary connection string for the Service Bus namespace"
  value       = azurerm_servicebus_namespace.main.default_primary_connection_string
  sensitive   = true
}

output "secondary_connection_string" {
  description = "The secondary connection string for the Service Bus namespace"
  value       = azurerm_servicebus_namespace.main.default_secondary_connection_string
  sensitive   = true
}

output "listen_send_connection_string" {
  description = "Connection string for listen and send operations"
  value       = azurerm_servicebus_namespace_authorization_rule.listen_send.primary_connection_string
  sensitive   = true
}

output "manage_connection_string" {
  description = "Connection string for manage operations"
  value       = azurerm_servicebus_namespace_authorization_rule.manage.primary_connection_string
  sensitive   = true
}

output "queue_ids" {
  description = "Map of queue names to their IDs"
  value       = { for k, v in azurerm_servicebus_queue.queues : k => v.id }
}

output "queue_names" {
  description = "List of created queue names"
  value       = [for queue in azurerm_servicebus_queue.queues : queue.name]
}

# Specific connection strings for different operations
output "order_received_send_connection_string" {
  description = "Connection string for sending to order-received queue"
  value       = azurerm_servicebus_queue_authorization_rule.order_received_send.primary_connection_string
  sensitive   = true
}

output "order_received_listen_connection_string" {
  description = "Connection string for listening to order-received queue"
  value       = azurerm_servicebus_queue_authorization_rule.order_received_listen.primary_connection_string
  sensitive   = true
}

output "order_confirmed_send_connection_string" {
  description = "Connection string for sending to order-confirmed queue"
  value       = azurerm_servicebus_queue_authorization_rule.order_confirmed_send.primary_connection_string
  sensitive   = true
}

output "order_failed_send_connection_string" {
  description = "Connection string for sending to order-failed queue"
  value       = azurerm_servicebus_queue_authorization_rule.order_failed_send.primary_connection_string
  sensitive   = true
}