terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

# Service Bus Namespace
resource "azurerm_servicebus_namespace" "main" {
  name                = var.service_bus_namespace_name
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "Standard"
  zone_redundant      = var.zone_redundant

  tags = var.tags
}

# Service Bus Queues
resource "azurerm_servicebus_queue" "queues" {
  for_each = toset(var.queue_names)

  name         = each.value
  namespace_id = azurerm_servicebus_namespace.main.id

  # Queue configuration based on queue name
  enable_partitioning                = each.value == "high-value-errors" ? false : true
  max_delivery_count                = each.value == "high-value-errors" ? 10 : 5
  default_message_ttl               = each.value == "high-value-errors" ? "P14D" : "P1D"
  lock_duration                     = "PT1M"
  duplicate_detection_history_time_window = "PT10M"
  
  # Dead lettering configuration
  dead_lettering_on_message_expiration = true
  requires_duplicate_detection         = true
  requires_session                     = false
  
  # Size limits
  max_size_in_megabytes = each.value == "high-value-errors" ? 5120 : 1024
}

# Authorization Rules for namespace level access
resource "azurerm_servicebus_namespace_authorization_rule" "listen_send" {
  name         = "ListenSend"
  namespace_id = azurerm_servicebus_namespace.main.id

  listen = true
  send   = true
  manage = false
}

resource "azurerm_servicebus_namespace_authorization_rule" "manage" {
  name         = "Manage"
  namespace_id = azurerm_servicebus_namespace.main.id

  listen = true
  send   = true
  manage = true
}

# Queue-specific authorization rules for fine-grained access
resource "azurerm_servicebus_queue_authorization_rule" "order_received_send" {
  name     = "OrderReceivedSend"
  queue_id = azurerm_servicebus_queue.queues["order-received"].id

  listen = false
  send   = true
  manage = false
}

resource "azurerm_servicebus_queue_authorization_rule" "order_received_listen" {
  name     = "OrderReceivedListen"
  queue_id = azurerm_servicebus_queue.queues["order-received"].id

  listen = true
  send   = false
  manage = false
}

resource "azurerm_servicebus_queue_authorization_rule" "order_confirmed_send" {
  name     = "OrderConfirmedSend"
  queue_id = azurerm_servicebus_queue.queues["order-confirmed"].id

  listen = false
  send   = true
  manage = false
}

resource "azurerm_servicebus_queue_authorization_rule" "order_failed_send" {
  name     = "OrderFailedSend"
  queue_id = azurerm_servicebus_queue.queues["order-failed"].id

  listen = false
  send   = true
  manage = false
}