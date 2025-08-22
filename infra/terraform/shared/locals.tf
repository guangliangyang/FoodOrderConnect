locals {
  # Generate unique suffix if not provided
  unique_suffix = var.unique_suffix != null ? var.unique_suffix : substr(md5(data.azurerm_client_config.current.subscription_id), 0, 6)
  
  # Environment configuration
  env_config = var.environment_configs[var.environment_name]
  
  # Common tags
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

# Data sources
data "azurerm_client_config" "current" {}

data "azurerm_subscription" "current" {}