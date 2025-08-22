# Development Environment Configuration
environment_name    = "dev"
location           = "East US"
resource_group_name = "rg-bidone-dev"

# Project settings
project_name = "bidone"

# SQL Server settings
sql_admin_login = "sqladmin"
# sql_admin_password should be provided via command line, environment variable, or Azure Key Vault

# Common tags
tags = {
  Environment = "dev"
  Project     = "BidOne-Integration-Demo"
  Owner       = "DevTeam"
  CostCenter  = "Engineering"
}

# Service Bus queues
service_bus_queue_names = [
  "order-received",
  "order-validated",
  "order-enriched",
  "order-processing",
  "order-confirmed",
  "order-failed",
  "high-value-errors"
]

# Event Grid event types
event_grid_event_types = [
  "BidOne.Dashboard.OrderMetrics",
  "BidOne.Dashboard.PerformanceAlert",
  "BidOne.Dashboard.SystemHealth"
]

# Cosmos DB containers
cosmos_db_containers = [
  {
    name               = "Products"
    partition_key_path = "/category"
    throughput         = 400
  },
  {
    name               = "CustomerProfiles"
    partition_key_path = "/customerId"
    throughput         = 400
  },
  {
    name               = "OrderEvents"
    partition_key_path = "/orderId"
    throughput         = 400
  }
]