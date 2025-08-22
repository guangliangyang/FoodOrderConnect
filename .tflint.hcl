# TFLint configuration for Terraform code quality
# https://github.com/terraform-linters/tflint

config {
  module = true
  force = false
  disabled_by_default = false
}

# Enable Azure provider plugin
plugin "azurerm" {
  enabled = true
  version = "0.25.1"
  source  = "github.com/terraform-linters/tflint-ruleset-azurerm"
}

# Terraform core rules
rule "terraform_comment_syntax" {
  enabled = true
}

rule "terraform_deprecated_interpolation" {
  enabled = true
}

rule "terraform_deprecated_index" {
  enabled = true
}

rule "terraform_unused_declarations" {
  enabled = true
}

rule "terraform_unused_required_providers" {
  enabled = true
}

rule "terraform_required_version" {
  enabled = true
}

rule "terraform_required_providers" {
  enabled = true
}

rule "terraform_naming_convention" {
  enabled = true
  format  = "snake_case"
}

rule "terraform_typed_variables" {
  enabled = true
}

rule "terraform_module_pinned_source" {
  enabled = true
}

rule "terraform_standard_module_structure" {
  enabled = true
}

# Azure-specific rules
rule "azurerm_resource_missing_tags" {
  enabled = true
  tags = ["Environment", "Project", "ManagedBy"]
}

rule "azurerm_sql_database_backup_required" {
  enabled = true
}

rule "azurerm_storage_account_secure_transfer" {
  enabled = true
}

rule "azurerm_key_vault_purge_protection" {
  enabled = true
}

rule "azurerm_cosmosdb_account_backup_enabled" {
  enabled = true
}