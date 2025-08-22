# IaC 快速参考卡片

## 🚀 快速开始

### 本地验证
```bash
# 完整验证
./scripts/validate-iac.sh

# 特定工具
./scripts/validate-iac.sh -t terraform -e dev
./scripts/validate-iac.sh -t bicep -e prod

# 跳过成本检查
./scripts/validate-iac.sh -c
```

### 部署
```bash
# 使用统一脚本
./infra/deploy.sh -t terraform -e dev -g rg-bidone-dev
./infra/deploy.sh -t bicep -e prod -g rg-bidone-prod

# 干运行
./infra/deploy.sh -t terraform -e staging -g rg-bidone-staging --dry-run
```

### 测试
```bash
# 基础设施测试
./infra/test-infrastructure.sh -g rg-bidone-dev -e dev

# 成本估算
cd infra/terraform && infracost breakdown --path .
```

## 🔧 工具命令

### Terraform
```bash
terraform fmt -recursive          # 格式化
terraform validate                # 语法验证
terraform plan                   # 规划
terraform apply                  # 应用
tflint --config=.tflint.hcl      # 代码检查
```

### Bicep
```bash
az bicep build --file main.bicep  # 编译
az deployment group validate      # 验证模板
az deployment group create        # 部署
```

### 安全扫描
```bash
checkov --config-file .checkov.yml    # 安全扫描
tfsec --config-file .tfsec.yml        # Terraform安全
```

## 📋 必需检查清单

### 代码提交前
- [ ] 代码格式化正确
- [ ] 所有验证通过
- [ ] 安全扫描无高风险问题
- [ ] 包含必需标签
- [ ] 命名约定正确
- [ ] 成本估算合理

### 部署前
- [ ] 代码审查通过
- [ ] 测试环境验证
- [ ] 备份策略确认
- [ ] 回滚计划准备

### 部署后
- [ ] 基础设施测试通过
- [ ] 监控配置正确
- [ ] 文档已更新

## 🏷️ 标签模板

```hcl
locals {
  common_tags = {
    Environment = var.environment_name
    Project     = "BidOne-Integration-Demo"
    ManagedBy   = "Terraform"
    Owner       = "DevTeam"
    CostCenter  = "Engineering"
  }
}
```

## 🔒 安全配置模板

### Storage Account
```hcl
resource "azurerm_storage_account" "secure" {
  enable_https_traffic_only = true
  min_tls_version          = "TLS1_2"
  allow_nested_items_to_be_public = false
  
  network_rules {
    default_action = "Deny"
    ip_rules       = var.allowed_ips
  }
}
```

### Key Vault
```hcl
resource "azurerm_key_vault" "secure" {
  soft_delete_retention_days = 90
  purge_protection_enabled   = true
  enable_rbac_authorization  = true
}
```

### SQL Server
```hcl
resource "azurerm_mssql_server" "secure" {
  minimum_tls_version = "1.2"
  public_network_access_enabled = false
  
  azuread_administrator {
    login_username = var.sql_aad_admin_login
    object_id      = var.sql_aad_admin_object_id
  }
}
```

## 🚨 常见错误

### 命名错误
```bash
# ❌ 错误
resource_name = "MyResource"
variable_name = "myVariable"

# ✅ 正确  
resource_name = "my_resource"
variable_name = "my_variable"
```

### 标签缺失
```hcl
# ❌ 错误
resource "azurerm_storage_account" "main" {
  # 缺少 tags
}

# ✅ 正确
resource "azurerm_storage_account" "main" {
  tags = local.common_tags
}
```

### 硬编码值
```hcl
# ❌ 错误
location = "East US"

# ✅ 正确
location = var.location
```

## 📞 获取帮助

### 文档
- [完整最佳实践指南](./iac-best-practices.md)
- [部署指南](./deployment-guide.md)
- [故障排除](./troubleshooting.md)

### 工具文档
- [Terraform文档](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs)
- [Bicep文档](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/)

### 联系方式
- 技术问题：创建GitHub Issue
- 紧急问题：联系DevOps团队
- 安全问题：联系安全团队