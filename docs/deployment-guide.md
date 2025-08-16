# 部署指南

## 概述

本文档详细说明如何将 BidOne Integration Platform 部署到 Azure 云平台，包括开发环境、测试环境和生产环境的部署步骤。

## 前置要求

### 工具和软件
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) >= 2.40.0
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Git](https://git-scm.com/downloads)
- [PowerShell 7+](https://github.com/PowerShell/PowerShell) (推荐)

### Azure 权限要求
- Azure 订阅的 Owner 或 Contributor 权限
- Azure AD 应用注册权限
- 能够创建服务主体的权限

## 快速开始

### 1. 克隆项目并初始化

```bash
# 克隆项目
git clone https://github.com/your-org/BidOne-Integration-Demo.git
cd BidOne-Integration-Demo

# 运行初始化脚本
./scripts/setup-dev-env.sh
```

### 2. 配置 Azure 环境

```bash
# 登录 Azure
az login

# 选择目标订阅
az account set --subscription "Your-Subscription-ID"

# 创建资源组
az group create --name bidone-demo-rg --location eastus
```

### 3. 一键部署

```bash
# 执行一键部署脚本
./scripts/deploy-to-azure.sh --environment production --resource-group bidone-demo-rg
```

## 详细部署步骤

### 步骤 1: 基础设施部署

#### 1.1 配置部署参数

创建环境特定的参数文件：

```json
// infra/parameters.prod.json
{
    "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
    "contentVersion": "1.0.0.0",
    "parameters": {
        "environmentName": {
            "value": "prod"
        },
        "location": {
            "value": "eastus"
        },
        "serviceBusNamespaceName": {
            "value": "bidone-sb-prod"
        },
        "sqlServerName": {
            "value": "bidone-sql-prod"
        },
        "cosmosDbAccountName": {
            "value": "bidone-cosmos-prod"
        },
        "containerRegistryName": {
            "value": "bidonecr"
        },
        "keyVaultName": {
            "value": "bidone-kv-prod"
        }
    }
}
```

#### 1.2 部署 Azure 基础设施

```bash
# 部署基础设施
az deployment group create \
    --resource-group bidone-demo-rg \
    --template-file infra/main.bicep \
    --parameters infra/parameters.prod.json \
    --name bidone-infra-deployment
```

### 步骤 2: 容器镜像构建和推送

#### 2.1 构建 Docker 镜像

```bash
# 构建 External Order API
docker build -t bidone/external-order-api:latest \
    -f src/ExternalOrderApi/Dockerfile .

# 构建 Internal System API  
docker build -t bidone/internal-system-api:latest \
    -f src/InternalSystemApi/Dockerfile .
```

#### 2.2 推送到 Azure Container Registry

```bash
# 获取 ACR 登录服务器
ACR_LOGIN_SERVER=$(az acr show --name bidonecr --query loginServer --output tsv)

# 登录 ACR
az acr login --name bidonecr

# 标记和推送镜像
docker tag bidone/external-order-api:latest $ACR_LOGIN_SERVER/bidone/external-order-api:latest
docker push $ACR_LOGIN_SERVER/bidone/external-order-api:latest

docker tag bidone/internal-system-api:latest $ACR_LOGIN_SERVER/bidone/internal-system-api:latest
docker push $ACR_LOGIN_SERVER/bidone/internal-system-api:latest
```

### 步骤 3: Azure Functions 部署

#### 3.1 构建和发布 Function App

```bash
# 进入 Function 项目目录
cd src/OrderIntegrationFunction

# 恢复依赖
dotnet restore

# 构建项目
dotnet build --configuration Release

# 发布项目
dotnet publish --configuration Release --output ./publish
```

#### 3.2 部署到 Azure Functions

```bash
# 部署 Function App
func azure functionapp publish bidone-functions-prod --csharp
```

### 步骤 4: Azure Logic Apps 配置

#### 4.1 部署 Logic App 定义

```bash
# 部署 Logic App
az logic workflow create \
    --resource-group bidone-demo-rg \
    --location eastus \
    --name bidone-order-workflow \
    --definition infra/logic-app-definition.json
```

#### 4.2 配置连接字符串

```bash
# 获取 Service Bus 连接字符串
SERVICE_BUS_CS=$(az servicebus namespace authorization-rule keys list \
    --resource-group bidone-demo-rg \
    --namespace-name bidone-sb-prod \
    --name RootManageSharedAccessKey \
    --query primaryConnectionString \
    --output tsv)

# 更新 Logic App 连接
az logic workflow update \
    --resource-group bidone-demo-rg \
    --name bidone-order-workflow \
    --set properties.parameters.serviceBusConnectionString.value="$SERVICE_BUS_CS"
```

### 步骤 5: Container Apps 部署

#### 5.1 创建 Container Apps 环境

```bash
# 创建 Container Apps 环境
az containerapp env create \
    --name bidone-env-prod \
    --resource-group bidone-demo-rg \
    --location eastus
```

#### 5.2 部署应用容器

```bash
# 部署 External Order API
az containerapp create \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --environment bidone-env-prod \
    --image $ACR_LOGIN_SERVER/bidone/external-order-api:latest \
    --target-port 80 \
    --ingress external \
    --min-replicas 2 \
    --max-replicas 10 \
    --cpu 1.0 \
    --memory 2Gi \
    --registry-server $ACR_LOGIN_SERVER

# 部署 Internal System API
az containerapp create \
    --name internal-system-api \
    --resource-group bidone-demo-rg \
    --environment bidone-env-prod \
    --image $ACR_LOGIN_SERVER/bidone/internal-system-api:latest \
    --target-port 80 \
    --ingress external \
    --min-replicas 2 \
    --max-replicas 10 \
    --cpu 1.0 \
    --memory 2Gi \
    --registry-server $ACR_LOGIN_SERVER
```

### 步骤 6: API Management 配置

#### 6.1 导入 API 定义

```bash
# 导入 External Order API
az apim api import \
    --resource-group bidone-demo-rg \
    --service-name bidone-apim-prod \
    --api-id external-order-api \
    --path "/external/orders" \
    --display-name "External Order API" \
    --protocols https \
    --service-url "https://external-order-api.bidone-env-prod.eastus.azurecontainerapps.io"

# 导入 Internal System API
az apim api import \
    --resource-group bidone-demo-rg \
    --service-name bidone-apim-prod \
    --api-id internal-system-api \
    --path "/internal/orders" \
    --display-name "Internal System API" \
    --protocols https \
    --service-url "https://internal-system-api.bidone-env-prod.eastus.azurecontainerapps.io"
```

#### 6.2 配置安全策略

```xml
<!-- API Management 策略示例 -->
<policies>
    <inbound>
        <base />
        <validate-jwt header-name="Authorization" failed-validation-httpcode="401">
            <openid-config url="https://login.microsoftonline.com/{tenant-id}/v2.0/.well-known/openid_configuration" />
            <audiences>
                <audience>api://bidone-integration-api</audience>
            </audiences>
        </validate-jwt>
        <rate-limit-by-key calls="100" renewal-period="60" counter-key="@(context.Request.IpAddress)" />
    </inbound>
    <backend>
        <base />
    </backend>
    <outbound>
        <base />
    </outbound>
    <on-error>
        <base />
    </on-error>
</policies>
```

## 环境配置

### 开发环境 (Development)

```bash
# 开发环境部署
./scripts/deploy-to-azure.sh --environment development --resource-group bidone-demo-dev-rg

# 特点：
# - 单实例部署
# - 共享数据库
# - 简化的监控配置
# - 开发友好的日志级别
```

### 测试环境 (Staging)

```bash
# 测试环境部署
./scripts/deploy-to-azure.sh --environment staging --resource-group bidone-demo-staging-rg

# 特点：
# - 生产级配置
# - 完整的监控和告警
# - 自动化测试集成
# - 数据脱敏
```

### 生产环境 (Production)

```bash
# 生产环境部署
./scripts/deploy-to-azure.sh --environment production --resource-group bidone-demo-prod-rg

# 特点：
# - 高可用配置
# - 多区域部署
# - 完整的安全配置
# - 备份和灾难恢复
```

## 配置管理

### 环境变量配置

#### Application Settings (Container Apps)

```bash
# 配置 External Order API 环境变量
az containerapp update \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --set-env-vars \
        ASPNETCORE_ENVIRONMENT=Production \
        ServiceBus__ConnectionString="@Microsoft.KeyVault(SecretUri=https://bidone-kv-prod.vault.azure.net/secrets/ServiceBusConnectionString/)" \
        ApplicationInsights__ConnectionString="@Microsoft.KeyVault(SecretUri=https://bidone-kv-prod.vault.azure.net/secrets/AppInsightsConnectionString/)"
```

#### Key Vault 密钥配置

```bash
# 存储敏感配置到 Key Vault
az keyvault secret set \
    --vault-name bidone-kv-prod \
    --name ServiceBusConnectionString \
    --value "$SERVICE_BUS_CS"

az keyvault secret set \
    --vault-name bidone-kv-prod \
    --name SqlConnectionString \
    --value "$SQL_CONNECTION_STRING"

az keyvault secret set \
    --vault-name bidone-kv-prod \
    --name CosmosDbConnectionString \
    --value "$COSMOS_CONNECTION_STRING"
```

## 监控和日志配置

### Application Insights 配置

```bash
# 获取 Application Insights 连接字符串
APP_INSIGHTS_CS=$(az monitor app-insights component show \
    --app bidone-insights-prod \
    --resource-group bidone-demo-rg \
    --query connectionString \
    --output tsv)

# 配置应用程序监控
az containerapp update \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --set-env-vars ApplicationInsights__ConnectionString="$APP_INSIGHTS_CS"
```

### 日志分析配置

```bash
# 启用容器日志收集
az monitor log-analytics workspace create \
    --resource-group bidone-demo-rg \
    --workspace-name bidone-logs-prod

# 配置日志转发
az containerapp env update \
    --name bidone-env-prod \
    --resource-group bidone-demo-rg \
    --logs-destination log-analytics \
    --logs-workspace-id "/subscriptions/{subscription-id}/resourceGroups/bidone-demo-rg/providers/Microsoft.OperationalInsights/workspaces/bidone-logs-prod"
```

## 数据库初始化

### SQL Database 初始化

```bash
# 运行数据库迁移
dotnet ef database update --project src/InternalSystemApi --connection "$SQL_CONNECTION_STRING"

# 或使用 SQL 脚本
sqlcmd -S bidone-sql-prod.database.windows.net -d BidOneDB -U sqladmin -P "$SQL_PASSWORD" -i scripts/init-database.sql
```

### Cosmos DB 初始化

```bash
# 创建数据库和容器
az cosmosdb sql database create \
    --account-name bidone-cosmos-prod \
    --resource-group bidone-demo-rg \
    --name BidOneDB

az cosmosdb sql container create \
    --account-name bidone-cosmos-prod \
    --resource-group bidone-demo-rg \
    --database-name BidOneDB \
    --name Products \
    --partition-key-path "/category" \
    --throughput 400
```

## 安全配置

### Azure AD 应用注册

```bash
# 创建应用注册
az ad app create \
    --display-name "BidOne Integration API" \
    --identifier-uris "api://bidone-integration-api" \
    --app-roles '[{
        "allowedMemberTypes": ["Application"],
        "description": "Access to BidOne Integration API",
        "displayName": "API Access",
        "isEnabled": true,
        "value": "API.Access"
    }]'
```

### 网络安全配置

```bash
# 创建网络安全组
az network nsg create \
    --resource-group bidone-demo-rg \
    --name bidone-nsg-prod

# 配置安全规则
az network nsg rule create \
    --resource-group bidone-demo-rg \
    --nsg-name bidone-nsg-prod \
    --name AllowHTTPS \
    --protocol Tcp \
    --priority 100 \
    --destination-port-range 443 \
    --access Allow
```

## 验证部署

### 健康检查

```bash
# 检查 API 健康状态
curl -f https://bidone-apim-prod.azure-api.net/external/orders/health
curl -f https://bidone-apim-prod.azure-api.net/internal/orders/health

# 检查 Function App 状态
az functionapp show \
    --name bidone-functions-prod \
    --resource-group bidone-demo-rg \
    --query state

# 检查 Logic App 状态
az logic workflow show \
    --resource-group bidone-demo-rg \
    --name bidone-order-workflow \
    --query state
```

### 端到端测试

```bash
# 运行集成测试
./scripts/run-integration-tests.sh --environment production

# 发送测试订单
curl -X POST https://bidone-apim-prod.azure-api.net/external/orders \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $ACCESS_TOKEN" \
    -d '{
        "customerId": "test-customer-001",
        "items": [
            {
                "productId": "PROD-001",
                "quantity": 10,
                "unitPrice": 25.50
            }
        ],
        "deliveryDate": "2024-01-15T10:00:00Z"
    }'
```

## 故障排除

### 常见问题和解决方案

#### 1. 容器启动失败

```bash
# 查看容器日志
az containerapp logs show \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --follow

# 检查环境变量配置
az containerapp show \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --query properties.template.containers[0].env
```

#### 2. Function App 部署问题

```bash
# 检查 Function App 日志
az functionapp log tail \
    --name bidone-functions-prod \
    --resource-group bidone-demo-rg

# 重新同步触发器
az functionapp function sync-function-triggers \
    --name bidone-functions-prod \
    --resource-group bidone-demo-rg
```

#### 3. Logic App 执行失败

```bash
# 查看 Logic App 运行历史
az logic workflow run list \
    --resource-group bidone-demo-rg \
    --workflow-name bidone-order-workflow

# 查看具体运行详情
az logic workflow run show \
    --resource-group bidone-demo-rg \
    --workflow-name bidone-order-workflow \
    --run-name {run-id}
```

### 日志查询示例

#### Application Insights (KQL 查询)

```kusto
// 查看最近1小时的错误
exceptions
| where timestamp > ago(1h)
| where cloud_RoleName in ("external-order-api", "internal-system-api")
| project timestamp, message, operation_Name, severityLevel
| order by timestamp desc

// 查看 API 性能指标
requests
| where timestamp > ago(1h)
| where cloud_RoleName == "external-order-api"
| summarize avg(duration), count() by bin(timestamp, 5m)
| render timechart
```

#### Container Apps 日志

```bash
# 查看应用程序日志
az monitor log-analytics query \
    --workspace bidone-logs-prod \
    --analytics-query "
        ContainerAppConsoleLogs_CL
        | where ContainerName_s == 'external-order-api'
        | where TimeGenerated > ago(1h)
        | project TimeGenerated, Log_s
        | order by TimeGenerated desc
    "
```

## 性能优化

### 扩缩容配置

```bash
# 配置自动扩缩容规则
az containerapp update \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --min-replicas 2 \
    --max-replicas 20 \
    --scale-rule-name http-requests \
    --scale-rule-type http \
    --scale-rule-http-concurrent-requests 100
```

### 缓存配置

```bash
# 创建 Redis 缓存
az redis create \
    --resource-group bidone-demo-rg \
    --name bidone-cache-prod \
    --location eastus \
    --sku Standard \
    --vm-size c1

# 获取 Redis 连接字符串
REDIS_CS=$(az redis list-keys \
    --resource-group bidone-demo-rg \
    --name bidone-cache-prod \
    --query primaryKey \
    --output tsv)
```

## 备份和恢复

### 数据库备份

```bash
# SQL Database 自动备份已启用，配置长期保留
az sql db ltr-policy set \
    --resource-group bidone-demo-rg \
    --server bidone-sql-prod \
    --database BidOneDB \
    --weekly-retention P4W \
    --monthly-retention P12M \
    --yearly-retention P7Y
```

### 应用配置备份

```bash
# 导出 API Management 配置
az apim backup \
    --resource-group bidone-demo-rg \
    --name bidone-apim-prod \
    --backup-name apim-backup-$(date +%Y%m%d) \
    --storage-account-name bidonebackupstorage \
    --storage-account-container backups
```

## 成本优化

### 资源使用监控

```bash
# 启用成本管理告警
az consumption budget create \
    --resource-group bidone-demo-rg \
    --budget-name bidone-monthly-budget \
    --amount 10 \
    --time-grain Monthly \
    --category Cost \
    --notifications amount=8 operator=GreaterThan \
        contact-emails="ricky.jobs.nz@gmail.com" \
        contact-roles="Owner,Contributor"
```

### 开发环境自动停机

```bash
# 配置开发环境自动停机 (使用 Azure Automation)
az automation schedule create \
    --resource-group bidone-demo-rg \
    --automation-account-name bidone-automation \
    --name "stop-dev-resources" \
    --frequency Day \
    --interval 1 \
    --start-time "2024-01-01T18:00:00+00:00"
```

---

## 下一步

完成部署后，建议进行以下操作：

1. 配置监控和告警规则
2. 设置备份和灾难恢复计划
3. 进行安全审计和渗透测试
4. 优化性能和成本
5. 建立运维文档和流程

如需帮助，请参考 [故障排除文档](troubleshooting.md) 或联系技术支持团队。