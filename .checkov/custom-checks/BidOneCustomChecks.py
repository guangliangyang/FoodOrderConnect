#!/usr/bin/env python3
"""
Custom Checkov checks for BidOne project
These checks enforce project-specific security and compliance requirements
"""

from checkov.common.models.enums import CheckResult, TRUE_BRANCH
from checkov.terraform.checks.resource.base_resource_check import BaseResourceCheck
from checkov.common.models.consts import ANY_VALUE
import re


class AzureResourceNamingConvention(BaseResourceCheck):
    """
    Ensure Azure resources follow BidOne naming convention
    Format: {project}-{service}-{environment}-{uniqueid}
    """
    
    def __init__(self):
        name = "Ensure Azure resources follow BidOne naming convention"
        id = "CKV_AZURE_BIDONE_001"
        supported_resources = [
            "azurerm_resource_group",
            "azurerm_storage_account",
            "azurerm_key_vault",
            "azurerm_sql_server",
            "azurerm_servicebus_namespace",
            "azurerm_cosmosdb_account",
            "azurerm_redis_cache"
        ]
        categories = ["CONVENTION"]
        super().__init__(name=name, id=id, categories=categories, supported_resources=supported_resources)

    def scan_resource_conf(self, conf):
        """
        Scan resource configuration for naming convention compliance
        """
        if 'name' in conf:
            resource_name = conf['name'][0]
            
            # Skip if name is a variable reference
            if isinstance(resource_name, str) and resource_name.startswith('${'):
                return CheckResult.PASSED
            
            # Check naming pattern: project-service-environment-suffix
            # Allow some flexibility for storage accounts (no hyphens allowed)
            if any(resource_type in self.supported_resources[1:2] for resource_type in self.supported_resources):
                # Storage account naming is more restrictive
                pattern = r'^[a-z0-9]{3,24}$'
                if re.match(pattern, resource_name):
                    return CheckResult.PASSED
            else:
                # Standard naming convention
                pattern = r'^[a-zA-Z0-9]+-[a-zA-Z0-9]+-[a-zA-Z0-9]+-[a-zA-Z0-9]+$'
                if re.match(pattern, resource_name):
                    return CheckResult.PASSED
            
            return CheckResult.FAILED
        
        return CheckResult.PASSED


class AzureResourceMandatoryTags(BaseResourceCheck):
    """
    Ensure Azure resources have mandatory tags for BidOne project
    """
    
    def __init__(self):
        name = "Ensure Azure resources have mandatory BidOne tags"
        id = "CKV_AZURE_BIDONE_002"
        supported_resources = [
            "azurerm_resource_group",
            "azurerm_storage_account",
            "azurerm_key_vault",
            "azurerm_sql_server",
            "azurerm_mssql_database",
            "azurerm_servicebus_namespace",
            "azurerm_cosmosdb_account",
            "azurerm_redis_cache",
            "azurerm_application_insights",
            "azurerm_log_analytics_workspace"
        ]
        categories = ["GENERAL_SECURITY"]
        super().__init__(name=name, id=id, categories=categories, supported_resources=supported_resources)

    def scan_resource_conf(self, conf):
        """
        Scan resource configuration for mandatory tags
        """
        required_tags = ["Environment", "Project", "ManagedBy"]
        
        if 'tags' in conf:
            tags = conf['tags'][0]
            
            # Skip if tags is a variable reference
            if isinstance(tags, str) and tags.startswith('${'):
                return CheckResult.PASSED
            
            if isinstance(tags, dict):
                # Check if all required tags are present
                for required_tag in required_tags:
                    if required_tag not in tags:
                        return CheckResult.FAILED
                
                # Validate Project tag value
                if 'Project' in tags and tags['Project'] != 'BidOne-Integration-Demo':
                    return CheckResult.FAILED
                
                return CheckResult.PASSED
        
        return CheckResult.FAILED


class AzureSQLDatabaseBackupRequired(BaseResourceCheck):
    """
    Ensure Azure SQL Database has proper backup configuration
    """
    
    def __init__(self):
        name = "Ensure Azure SQL Database has backup configuration"
        id = "CKV_AZURE_BIDONE_003"
        supported_resources = ["azurerm_mssql_database"]
        categories = ["BACKUP"]
        super().__init__(name=name, id=id, categories=categories, supported_resources=supported_resources)

    def scan_resource_conf(self, conf):
        """
        Scan for backup policy configuration
        """
        # Check for backup policies
        if 'short_term_retention_policy' in conf:
            retention_policy = conf['short_term_retention_policy'][0]
            if isinstance(retention_policy, dict):
                retention_days = retention_policy.get('retention_days', [0])[0]
                if retention_days >= 7:  # Minimum 7 days retention
                    return CheckResult.PASSED
        
        return CheckResult.FAILED


class AzureStorageAccountSecureConfiguration(BaseResourceCheck):
    """
    Ensure Azure Storage Account has secure configuration for BidOne
    """
    
    def __init__(self):
        name = "Ensure Azure Storage Account has secure BidOne configuration"
        id = "CKV_AZURE_BIDONE_004"
        supported_resources = ["azurerm_storage_account"]
        categories = ["STORAGE"]
        super().__init__(name=name, id=id, categories=categories, supported_resources=supported_resources)

    def scan_resource_conf(self, conf):
        """
        Scan storage account for secure configuration
        """
        # Check account tier
        if 'account_tier' in conf:
            account_tier = conf['account_tier'][0]
            if account_tier != 'Standard':
                return CheckResult.FAILED
        
        # Check replication type
        if 'account_replication_type' in conf:
            replication_type = conf['account_replication_type'][0]
            allowed_replication = ['LRS', 'GRS', 'RAGRS', 'ZRS']
            if replication_type not in allowed_replication:
                return CheckResult.FAILED
        
        # Check for HTTPS-only traffic
        if 'enable_https_traffic_only' in conf:
            https_only = conf['enable_https_traffic_only'][0]
            if not https_only:
                return CheckResult.FAILED
        
        return CheckResult.PASSED


class AzureKeyVaultSecureConfiguration(BaseResourceCheck):
    """
    Ensure Azure Key Vault has secure configuration for BidOne
    """
    
    def __init__(self):
        name = "Ensure Azure Key Vault has secure BidOne configuration"
        id = "CKV_AZURE_BIDONE_005"
        supported_resources = ["azurerm_key_vault"]
        categories = ["SECRETS"]
        super().__init__(name=name, id=id, categories=categories, supported_resources=supported_resources)

    def scan_resource_conf(self, conf):
        """
        Scan Key Vault for secure configuration
        """
        # Check for soft delete
        if 'soft_delete_retention_days' in conf:
            retention_days = conf['soft_delete_retention_days'][0]
            if retention_days < 7:  # Minimum 7 days
                return CheckResult.FAILED
        else:
            return CheckResult.FAILED
        
        # Check SKU
        if 'sku_name' in conf:
            sku_name = conf['sku_name'][0]
            if sku_name not in ['standard', 'premium']:
                return CheckResult.FAILED
        
        return CheckResult.PASSED


# Register all custom checks
check = AzureResourceNamingConvention()
check = AzureResourceMandatoryTags()
check = AzureSQLDatabaseBackupRequired()
check = AzureStorageAccountSecureConfiguration()
check = AzureKeyVaultSecureConfiguration()