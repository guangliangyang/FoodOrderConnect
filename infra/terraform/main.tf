terraform {
  required_version = ">= 1.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }

  backend "azurerm" {
    # Backend configuration will be provided via command line or config file
    # Example:
    # storage_account_name = "yourstorageaccount"
    # container_name       = "tfstate"
    # key                  = "terraform.tfstate"
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy    = true
      recover_soft_deleted_key_vaults = true
    }
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
  }
}

# Data sources
data "azurerm_client_config" "current" {}
data "azurerm_subscription" "current" {}

# Local configuration
locals {
  # Import locals from shared configuration
  unique_suffix = var.unique_suffix != null ? var.unique_suffix : substr(md5(data.azurerm_client_config.current.subscription_id), 0, 6)
  env_config = var.environment_configs[var.environment_name]
  common_tags = merge({
    Environment = var.environment_name
    Project     = "BidOne-Integration-Demo"
    ManagedBy   = "Terraform"
  }, var.tags)
  
  # Resource naming convention
  resource_names = {
    service_bus_namespace_name     = "${var.project_name}-sb-${var.environment_name}-${local.unique_suffix}"
    container_registry_name        = "${var.project_name}cr${local.unique_suffix}"
    key_vault_name                = "${var.project_name}-kv-${var.environment_name}-${local.unique_suffix}"
    sql_server_name               = "${var.project_name}-sql-${var.environment_name}-${local.unique_suffix}"
    cosmos_db_account_name        = "${var.project_name}-cosmos-${var.environment_name}-${local.unique_suffix}"
    app_insights_name             = "${var.project_name}-insights-${var.environment_name}-${local.unique_suffix}"
    log_analytics_name            = "${var.project_name}-logs-${var.environment_name}-${local.unique_suffix}"
    container_apps_env_name       = "${var.project_name}-env-${var.environment_name}-${local.unique_suffix}"
    apim_service_name            = "${var.project_name}-apim-${var.environment_name}-${local.unique_suffix}"
    function_app_name            = "${var.project_name}-func-${var.environment_name}-${local.unique_suffix}"
    customer_comm_function_name   = "${var.project_name}-custcomm-${var.environment_name}-${local.unique_suffix}"
    storage_account_name         = "${var.project_name}st${var.environment_name}${local.unique_suffix}"
    logic_app_name               = "${var.project_name}-logic-${var.environment_name}-${local.unique_suffix}"
    event_grid_topic_name        = "${var.project_name}-events-${var.environment_name}-${local.unique_suffix}"
    redis_cache_name             = "${var.project_name}-redis-${var.environment_name}-${local.unique_suffix}"
    function_app_service_plan_name = "${var.project_name}-func-plan-${var.environment_name}-${local.unique_suffix}"
  }
}

# Resource Group (assuming it exists)
data "azurerm_resource_group" "main" {
  name = var.resource_group_name
}

# Storage Account for Function Apps and diagnostics
resource "azurerm_storage_account" "main" {
  name                     = local.resource_names.storage_account_name
  resource_group_name      = data.azurerm_resource_group.main.name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  
  blob_properties {
    cors_rule {
      allowed_headers    = ["*"]
      allowed_methods    = ["DELETE", "GET", "HEAD", "MERGE", "POST", "OPTIONS", "PUT"]
      allowed_origins    = ["*"]
      exposed_headers    = ["*"]
      max_age_in_seconds = 3600
    }
  }

  tags = local.common_tags
}

# Container Registry
resource "azurerm_container_registry" "main" {
  name                = local.resource_names.container_registry_name
  resource_group_name = data.azurerm_resource_group.main.name
  location            = var.location
  sku                 = local.env_config.container_registry_sku
  admin_enabled       = true

  tags = local.common_tags
}

# Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "main" {
  name                = local.resource_names.log_analytics_name
  location            = var.location
  resource_group_name = data.azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = local.env_config.log_analytics_retention

  tags = local.common_tags
}

# Application Insights
resource "azurerm_application_insights" "main" {
  name                = local.resource_names.app_insights_name
  location            = var.location
  resource_group_name = data.azurerm_resource_group.main.name
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"

  tags = local.common_tags
}

# Key Vault
resource "azurerm_key_vault" "main" {
  name                        = local.resource_names.key_vault_name
  location                    = var.location
  resource_group_name         = data.azurerm_resource_group.main.name
  enabled_for_disk_encryption = true
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  soft_delete_retention_days  = 7
  purge_protection_enabled    = false
  sku_name                    = "standard"

  access_policy {
    tenant_id = data.azurerm_client_config.current.tenant_id
    object_id = data.azurerm_client_config.current.object_id

    key_permissions = [
      "Get", "List", "Update", "Create", "Import", "Delete", "Recover", "Backup", "Restore"
    ]

    secret_permissions = [
      "Get", "List", "Set", "Delete", "Recover", "Backup", "Restore"
    ]

    certificate_permissions = [
      "Get", "List", "Update", "Create", "Import", "Delete", "Recover", "Backup", "Restore", "ManageContacts", "ManageIssuers", "GetIssuers", "ListIssuers", "SetIssuers", "DeleteIssuers"
    ]
  }

  tags = local.common_tags
}

# Store SQL admin password in Key Vault
resource "azurerm_key_vault_secret" "sql_admin_password" {
  name         = "sql-admin-password"
  value        = var.sql_admin_password
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault.main]
}

# Service Bus module
module "service_bus" {
  source = "./modules/service-bus"

  service_bus_namespace_name = local.resource_names.service_bus_namespace_name
  location                   = var.location
  resource_group_name        = data.azurerm_resource_group.main.name
  queue_names               = var.service_bus_queue_names
  zone_redundant            = local.env_config.service_bus_zone_redundant
  tags                      = local.common_tags
}

# SQL Server module
module "sql_server" {
  source = "./modules/sql-server"

  sql_server_name              = local.resource_names.sql_server_name
  resource_group_name          = data.azurerm_resource_group.main.name
  location                     = var.location
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password
  database_name               = "BidOneDB"
  sku_name                    = local.env_config.sql_sku_name
  max_size_gb                 = local.env_config.sql_max_size_gb
  zone_redundant              = local.env_config.sql_zone_redundant
  read_scale                  = local.env_config.sql_read_scale
  auto_pause_delay_in_minutes = local.env_config.sql_auto_pause_delay
  tags                        = local.common_tags

  # Security settings
  azuread_admin_login     = ""
  azuread_admin_object_id = ""
  azuread_admin_tenant_id = ""
  
  audit_storage_endpoint        = azurerm_storage_account.main.primary_blob_endpoint
  audit_storage_account_key     = azurerm_storage_account.main.primary_access_key
  audit_retention_days          = 90
}

# Cosmos DB module
module "cosmos_db" {
  source = "./modules/cosmos-db"

  cosmos_account_name  = local.resource_names.cosmos_db_account_name
  location            = var.location
  resource_group_name = data.azurerm_resource_group.main.name
  database_name       = "BidOneDB"
  
  # Environment-specific settings
  enable_free_tier                = local.env_config.cosmos_enable_free_tier
  backup_interval_in_minutes      = local.env_config.cosmos_backup_interval
  backup_retention_in_hours       = local.env_config.cosmos_backup_retention
  zone_redundant                  = local.env_config.sql_zone_redundant
  
  containers = var.cosmos_db_containers
  tags       = local.common_tags
}

# Redis module
module "redis" {
  source = "./modules/redis"

  redis_cache_name    = local.resource_names.redis_cache_name
  location           = var.location
  resource_group_name = data.azurerm_resource_group.main.name
  capacity           = local.env_config.redis_sku_capacity
  family             = local.env_config.redis_sku_family
  sku_name           = local.env_config.redis_sku_name
  tags               = local.common_tags
}