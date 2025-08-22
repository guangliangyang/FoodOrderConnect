# Infrastructure as Code (IaC) 开发最佳实践

## 概述

本文档为BidOne项目的基础设施即代码(IaC)开发提供全面的最佳实践指南。我们支持Bicep和Terraform两种工具，确保代码质量、安全性和可维护性。

## 🎯 质量保证目标

- **代码质量**: 100%格式化正确，通过所有静态分析
- **安全性**: 零高风险安全问题，所有配置符合安全基准
- **可靠性**: 基础设施部署成功率 > 99%
- **可维护性**: 代码可读性高，文档完整
- **合规性**: 所有资源必须包含必需标签

## 🛠️ 开发工具链

### 必需工具

#### 基础工具
```bash
# Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
az bicep install

# Python 和安全扫描工具
pip3 install checkov pre-commit
```

#### Terraform工具链
```bash
# Terraform
wget https://releases.hashicorp.com/terraform/latest/terraform_linux_amd64.zip
unzip terraform_linux_amd64.zip && sudo mv terraform /usr/local/bin/

# TFLint
curl -s https://raw.githubusercontent.com/terraform-linters/tflint/master/install_linux.sh | bash

# tfsec
wget -O tfsec https://github.com/aquasecurity/tfsec/releases/latest/download/tfsec-linux-amd64
chmod +x tfsec && sudo mv tfsec /usr/local/bin/

# Infracost (可选，用于成本估算)
curl -fsSL https://raw.githubusercontent.com/infracost/infracost/master/scripts/install.sh | sh
```

#### IDE扩展 (推荐)
- **VS Code**: 
  - Azure Resource Manager Tools
  - Terraform
  - HashiCorp Terraform
  - Bicep

## 📋 代码标准

### 命名约定

#### Azure资源命名
```bash
# 标准格式: {project}-{service}-{environment}-{suffix}
# 示例:
bidone-sql-dev-a1b2c3       # SQL Server
bidone-kv-prod-x9y8z7       # Key Vault
bidone-sb-staging-m4n5p6    # Service Bus

# 存储账户 (无连字符)
bidonestdeva1b2c3           # Storage Account
```

#### 代码命名
```hcl
# Terraform - snake_case
resource "azurerm_storage_account" "main" {
  name = local.resource_names.storage_account_name
}

variable "environment_name" {
  description = "Environment name"
  type        = string
}
```

```bicep
// Bicep - camelCase
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: resourceNames.storageAccountName
}

param environmentName string
```

### 文件组织

#### Terraform目录结构
```
terraform/
├── main.tf              # 主配置文件
├── variables.tf          # 变量定义
├── outputs.tf           # 输出定义
├── modules/             # 可重用模块
│   ├── service-bus/
│   ├── sql-server/
│   └── cosmos-db/
├── environments/        # 环境配置
│   ├── dev.tfvars
│   ├── staging.tfvars
│   └── prod.tfvars
└── shared/              # 共享配置
    ├── variables.tf
    ├── locals.tf
    └── outputs.tf
```

#### Bicep目录结构
```
bicep/
├── main.bicep           # 主模板
├── parameters/          # 参数文件
│   ├── dev.json
│   ├── staging.json
│   └── prod.json
├── modules/             # 模块
│   ├── storage.bicep
│   ├── database.bicep
│   └── networking.bicep
└── policies/            # 策略定义
    ├── global-policy.xml
    └── api-policies/
```

### 代码质量标准

#### 1. 格式化要求
```bash
# Terraform
terraform fmt -recursive

# Bicep会自动格式化，但确保缩进一致
```

#### 2. 变量定义标准
```hcl
# Terraform变量必须包含描述和类型
variable "environment_name" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment_name)
    error_message = "Environment name must be one of: dev, staging, prod."
  }
}

# 敏感变量必须标记
variable "sql_admin_password" {
  description = "SQL Server administrator password"
  type        = string
  sensitive   = true
}
```

#### 3. 资源配置标准
```hcl
# 所有资源必须包含tags
resource "azurerm_storage_account" "main" {
  name                = local.resource_names.storage_account_name
  resource_group_name = var.resource_group_name
  location           = var.location
  
  # 必需的安全配置
  account_tier             = "Standard"
  account_replication_type = "LRS"
  enable_https_traffic_only = true
  min_tls_version         = "TLS1_2"
  
  # 必需标签
  tags = local.common_tags
}
```

## 🔒 安全最佳实践

### 1. 密钥管理
```hcl
# ✅ 正确：使用Key Vault存储敏感信息
resource "azurerm_key_vault_secret" "sql_password" {
  name         = "sql-admin-password"
  value        = var.sql_admin_password
  key_vault_id = azurerm_key_vault.main.id
}

# ❌ 错误：明文存储密码
resource "azurerm_sql_server" "main" {
  administrator_login_password = "PlaintextPassword123!"  # 不要这样做
}
```

### 2. 网络安全
```hcl
# ✅ 正确：限制网络访问
resource "azurerm_storage_account" "main" {
  enable_https_traffic_only = true
  min_tls_version          = "TLS1_2"
  
  network_rules {
    default_action = "Deny"
    ip_rules       = var.allowed_ip_ranges
  }
}

# ✅ 正确：使用私有端点
resource "azurerm_private_endpoint" "sql" {
  name                = "${local.resource_names.sql_server_name}-pe"
  resource_group_name = var.resource_group_name
  location           = var.location
  subnet_id          = var.private_subnet_id
}
```

### 3. 访问控制
```hcl
# ✅ 正确：最小权限原则
resource "azurerm_key_vault_access_policy" "app" {
  key_vault_id = azurerm_key_vault.main.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_user_assigned_identity.app.principal_id
  
  secret_permissions = [
    "Get",
    "List"
  ]
  # 不给予不必要的权限如 "Set", "Delete"
}
```

### 4. 备份和恢复
```hcl
# ✅ 正确：配置备份策略
resource "azurerm_mssql_database" "main" {
  name           = "BidOneDB"
  server_id      = azurerm_mssql_server.main.id
  
  short_term_retention_policy {
    retention_days = 7
  }
  
  long_term_retention_policy {
    weekly_retention  = "P1W"
    monthly_retention = "P1M"
    yearly_retention  = "P1Y"
    week_of_year      = 1
  }
}
```

## 🏷️ 标签策略

### 必需标签
所有资源必须包含以下标签：

```hcl
locals {
  common_tags = {
    Environment = var.environment_name    # dev/staging/prod
    Project     = "BidOne-Integration-Demo"
    ManagedBy   = "Terraform"            # 或 "Bicep"
    Owner       = "DevTeam"
    CostCenter  = "Engineering"
  }
}
```

### 环境特定标签
```hcl
# 生产环境额外标签
tags = merge(local.common_tags, {
  Backup      = "Required"
  Monitoring  = "Enhanced"
  Compliance  = "SOX"
})
```

## 🔍 代码验证流程

### 1. 本地验证
```bash
# 运行完整验证
./scripts/validate-iac.sh

# 验证特定工具
./scripts/validate-iac.sh -t terraform -e dev

# 跳过成本检查
./scripts/validate-iac.sh -c
```

### 2. 预提交钩子
```bash
# 安装预提交钩子
pip install pre-commit
pre-commit install

# 手动运行所有钩子
pre-commit run --all-files
```

### 3. CI/CD验证
每次Push或PR都会触发：
- 代码格式检查
- 静态安全扫描
- 语法验证
- 成本估算
- 合规性检查

## 🧪 测试策略

### 1. 单元测试 (静态验证)
```bash
# Terraform验证
terraform fmt -check
terraform validate
tflint

# Bicep验证
az bicep build --file main.bicep
```

### 2. 安全测试
```bash
# 运行安全扫描
checkov --config-file .checkov.yml
tfsec --config-file .tfsec.yml
```

### 3. 集成测试 (部署后)
```bash
# 运行基础设施测试
./infra/test-infrastructure.sh -g rg-bidone-dev -e dev
```

### 4. 合规性测试
```bash
# 验证标签
python3 scripts/validate-tags.py

# 验证命名约定
python3 scripts/check-naming.py
```

## 🚀 部署流程

### 1. 开发阶段
```bash
# 1. 编写代码
# 2. 本地验证
./scripts/validate-iac.sh -t terraform -e dev

# 3. 提交代码
git add .
git commit -m "feat: add new storage configuration"
git push origin feature/new-storage
```

### 2. 代码审查
- 自动CI/CD验证通过
- 同事代码审查
- 安全团队审核(生产环境)

### 3. 部署执行
```bash
# 使用统一部署脚本
./infra/deploy.sh -t terraform -e dev -g rg-bidone-dev

# 或使用GitHub Actions手动触发
# 选择环境和工具进行部署
```

## 💰 成本优化

### 1. 成本估算
```bash
# 本地成本估算
infracost breakdown --path infra/terraform \
  --terraform-var-file="environments/dev.tfvars"

# CI/CD自动成本检查
# 会在每次PR时运行并评论成本变化
```

### 2. 环境资源配置
```hcl
# 根据环境调整资源规格
variable "environment_configs" {
  description = "Environment-specific configurations"
  type = map(object({
    sql_sku_name    = string
    redis_capacity  = number
    # ...
  }))
  default = {
    dev = {
      sql_sku_name   = "S1"      # 低成本
      redis_capacity = 1
    }
    prod = {
      sql_sku_name   = "S3"      # 高性能
      redis_capacity = 3
    }
  }
}
```

### 3. 资源生命周期管理
```hcl
# 开发环境自动关闭
resource "azurerm_automation_schedule" "nightly_shutdown" {
  count               = var.environment_name == "dev" ? 1 : 0
  name                = "nightly-shutdown"
  resource_group_name = var.resource_group_name
  # ...
}
```

## 🛡️ 灾难恢复

### 1. 状态文件备份 (Terraform)
```hcl
terraform {
  backend "azurerm" {
    storage_account_name = "tfstatestorageaccount"
    container_name       = "tfstate"
    key                  = "terraform.tfstate"
    # 状态文件自动备份到Azure Storage
  }
}
```

### 2. 跨区域部署
```hcl
# 主区域
resource "azurerm_sql_server" "primary" {
  location = "East US"
  # ...
}

# 灾备区域
resource "azurerm_sql_server" "secondary" {
  count    = var.enable_dr ? 1 : 0
  location = "West US 2"
  # ...
}
```

## 📚 故障排除指南

### 常见问题和解决方案

#### 1. Terraform状态锁定
```bash
# 问题：状态文件被锁定
Error: Error acquiring the state lock

# 解决：强制解锁（谨慎使用）
terraform force-unlock LOCK_ID
```

#### 2. 资源命名冲突
```bash
# 问题：资源名称已存在
Error: storage account name already exists

# 解决：使用唯一后缀
locals {
  unique_suffix = substr(md5(data.azurerm_subscription.current.subscription_id), 0, 6)
}
```

#### 3. 权限不足
```bash
# 问题：没有创建资源的权限
Error: insufficient privileges

# 解决：检查Azure RBAC角色
az role assignment list --assignee $(az account show --query user.name -o tsv)
```

#### 4. 验证失败
```bash
# 问题：预提交钩子失败
# 解决：查看具体错误并修复
pre-commit run --all-files

# 跳过特定钩子（临时）
git commit --no-verify
```

## 📊 监控和度量

### 1. 部署度量
- 部署成功率
- 部署时间
- 错误率

### 2. 代码质量度量
- 代码覆盖率
- 安全问题数量
- 合规性分数

### 3. 成本度量
- 月度成本趋势
- 成本异常预警
- 资源利用率

## 🔗 相关资源

### 官方文档
- [Terraform Azure Provider](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs)
- [Azure Bicep Documentation](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
- [Azure Well-Architected Framework](https://docs.microsoft.com/en-us/azure/architecture/framework/)

### 工具文档
- [Checkov](https://www.checkov.io/)
- [tfsec](https://aquasecurity.github.io/tfsec/)
- [TFLint](https://github.com/terraform-linters/tflint)
- [Infracost](https://www.infracost.io/)

### 内部资源
- [项目README](../README.md)
- [部署指南](../docs/deployment-guide.md)
- [架构文档](../docs/architecture.md)
- [故障排除](../docs/troubleshooting.md)

## 🤝 贡献指南

### 1. 提交新功能
1. 创建功能分支
2. 编写代码和测试
3. 运行本地验证
4. 提交PR
5. 通过代码审查
6. 部署到测试环境验证

### 2. 报告问题
1. 使用GitHub Issues
2. 提供详细的错误信息
3. 包含重现步骤
4. 添加相关标签

### 3. 改进建议
1. 讨论新的最佳实践
2. 提出工具改进建议
3. 分享经验和教训

---

**记住：质量胜过速度。每一行IaC代码都要经过严格验证，确保安全、可靠、可维护。**