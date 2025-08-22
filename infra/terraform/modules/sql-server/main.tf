terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

# SQL Server
resource "azurerm_mssql_server" "main" {
  name                         = var.sql_server_name
  resource_group_name          = var.resource_group_name
  location                     = var.location
  version                      = "12.0"
  administrator_login          = var.administrator_login
  administrator_login_password = var.administrator_login_password

  # Security settings
  minimum_tls_version               = "1.2"
  public_network_access_enabled     = true
  outbound_network_restriction_enabled = false

  # Azure AD authentication
  azuread_administrator {
    login_username = var.azuread_admin_login
    object_id      = var.azuread_admin_object_id
    tenant_id      = var.azuread_admin_tenant_id
  }

  tags = var.tags
}

# SQL Database
resource "azurerm_mssql_database" "main" {
  name           = var.database_name
  server_id      = azurerm_mssql_server.main.id
  collation      = "SQL_Latin1_General_CP1_CI_AS"
  license_type   = "LicenseIncluded"
  max_size_gb    = var.max_size_gb
  sku_name       = var.sku_name
  zone_redundant = var.zone_redundant
  read_scale     = var.read_scale

  # Auto-pause configuration for serverless SKUs
  auto_pause_delay_in_minutes = var.auto_pause_delay_in_minutes

  # Backup settings
  short_term_retention_policy {
    retention_days = 7
  }

  long_term_retention_policy {
    weekly_retention  = "P1W"
    monthly_retention = "P1M"
    yearly_retention  = "P1Y"
    week_of_year      = 1
  }

  tags = var.tags
}

# Firewall rules
resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Additional firewall rules for specific IP ranges
resource "azurerm_mssql_firewall_rule" "additional_rules" {
  for_each = var.firewall_rules

  name             = each.key
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = each.value.start_ip
  end_ip_address   = each.value.end_ip
}

# Security Alert Policy
resource "azurerm_mssql_server_security_alert_policy" "main" {
  resource_group_name = var.resource_group_name
  server_name         = azurerm_mssql_server.main.name
  state               = "Enabled"

  disabled_alerts = []
  retention_days  = 30

  email_account_admins = true
  email_addresses      = var.security_alert_email_addresses
}

# Vulnerability Assessment
resource "azurerm_mssql_server_vulnerability_assessment" "main" {
  count = var.enable_vulnerability_assessment ? 1 : 0

  server_security_alert_policy_id = azurerm_mssql_server_security_alert_policy.main.id
  storage_container_path          = var.vulnerability_assessment_storage_path
  storage_account_access_key      = var.vulnerability_assessment_storage_key

  recurring_scans {
    enabled                   = true
    email_subscription_admins = true
    emails                    = var.security_alert_email_addresses
  }
}

# Auditing
resource "azurerm_mssql_database_extended_auditing_policy" "main" {
  database_id                             = azurerm_mssql_database.main.id
  storage_endpoint                        = var.audit_storage_endpoint
  storage_account_access_key              = var.audit_storage_account_key
  storage_account_access_key_is_secondary = false
  retention_in_days                       = var.audit_retention_days

  log_monitoring_enabled = true
}

# Transparent Data Encryption
resource "azurerm_mssql_database_transparent_data_encryption" "main" {
  database_id = azurerm_mssql_database.main.id
}