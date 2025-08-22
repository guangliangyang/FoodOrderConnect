variable "sql_server_name" {
  description = "The name of the SQL Server"
  type        = string
}

variable "resource_group_name" {
  description = "The name of the resource group"
  type        = string
}

variable "location" {
  description = "Azure region for deployment"
  type        = string
}

variable "administrator_login" {
  description = "The administrator login for the SQL Server"
  type        = string
}

variable "administrator_login_password" {
  description = "The administrator password for the SQL Server"
  type        = string
  sensitive   = true
}

variable "database_name" {
  description = "The name of the SQL Database"
  type        = string
  default     = "BidOneDB"
}

variable "sku_name" {
  description = "The SKU name for the SQL Database"
  type        = string
  default     = "S1"
}

variable "max_size_gb" {
  description = "The maximum size of the database in GB"
  type        = number
  default     = 100
}

variable "zone_redundant" {
  description = "Whether the database should be zone redundant"
  type        = bool
  default     = false
}

variable "read_scale" {
  description = "Whether read scale-out is enabled"
  type        = bool
  default     = false
}

variable "auto_pause_delay_in_minutes" {
  description = "Time in minutes after which database is automatically paused. A value of -1 means that automatic pause is disabled"
  type        = number
  default     = 60
}

variable "azuread_admin_login" {
  description = "The login username of the Azure AD administrator"
  type        = string
  default     = ""
}

variable "azuread_admin_object_id" {
  description = "The object ID of the Azure AD administrator"
  type        = string
  default     = ""
}

variable "azuread_admin_tenant_id" {
  description = "The tenant ID of the Azure AD administrator"
  type        = string
  default     = ""
}

variable "firewall_rules" {
  description = "Map of firewall rules to create"
  type = map(object({
    start_ip = string
    end_ip   = string
  }))
  default = {}
}

variable "security_alert_email_addresses" {
  description = "List of email addresses for security alerts"
  type        = list(string)
  default     = []
}

variable "enable_vulnerability_assessment" {
  description = "Whether to enable vulnerability assessment"
  type        = bool
  default     = false
}

variable "vulnerability_assessment_storage_path" {
  description = "Storage container path for vulnerability assessment"
  type        = string
  default     = ""
}

variable "vulnerability_assessment_storage_key" {
  description = "Storage account access key for vulnerability assessment"
  type        = string
  default     = ""
  sensitive   = true
}

variable "audit_storage_endpoint" {
  description = "Storage endpoint for audit logs"
  type        = string
  default     = ""
}

variable "audit_storage_account_key" {
  description = "Storage account key for audit logs"
  type        = string
  default     = ""
  sensitive   = true
}

variable "audit_retention_days" {
  description = "Number of days to retain audit logs"
  type        = number
  default     = 90
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
}