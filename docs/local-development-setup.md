# 本地开发环境搭建指南

## 📋 概述

本指南详细说明如何搭建BidOne Integration Platform的本地开发环境，通过Docker容器化技术完整模拟Azure生产环境，为开发者提供与生产环境高度一致的本地开发体验。

### 🎯 设计理念

- **生产一致性**: 最大程度模拟Azure云服务，确保开发环境与生产环境的一致性
- **开发友好**: 提供便捷的开发工具和调试能力，支持快速迭代
- **一键启动**: 通过自动化脚本实现环境的快速搭建和管理
- **可观测性**: 集成完整的监控和日志系统，便于开发调试

## 🏗️ 架构对比

### 生产环境 vs 本地环境映射

```mermaid
graph TB
    subgraph "☁️ Azure生产环境"
        subgraph "API网关层"
            APIM[API Management<br/>XML策略 + Azure AD]
        end
        subgraph "计算层"
            ACA[Container Apps<br/>微服务容器]
            FUNC[Azure Functions<br/>Serverless计算]
        end
        subgraph "数据层"
            SQL[Azure SQL Database<br/>关系型数据]
            COSMOS[Cosmos DB<br/>NoSQL文档数据]
            SB[Service Bus<br/>消息队列]
            STORAGE[Azure Storage<br/>文件存储]
        end
        subgraph "监控层"
            APPINS[Application Insights<br/>APM监控]
            EVENTGRID[Event Grid<br/>事件驱动]
        end
    end
    
    subgraph "🐳 本地开发环境"
        subgraph "网关层"
            NGINX[Nginx<br/>反向代理 + 基础认证]
        end
        subgraph "应用层"
            DOCKER_APPS[Docker容器<br/>API + Functions]
        end
        subgraph "数据模拟层"
            SQL_DOCKER[SQL Server容器<br/>完全兼容]
            COSMOS_EMU[Cosmos DB模拟器<br/>官方Linux模拟器]
            SB_EMU[Service Bus模拟器<br/>官方模拟器]
            AZURITE[Azurite<br/>Azure Storage模拟器]
        end
        subgraph "监控模拟层"
            OTEL[OpenTelemetry<br/>APM数据收集]
            JAEGER[Jaeger<br/>分布式追踪]
            PROMETHEUS[Prometheus<br/>指标收集]
            GRAFANA[Grafana<br/>可视化仪表盘]
        end
    end
    
    APIM -.-> |模拟| NGINX
    ACA -.-> |模拟| DOCKER_APPS
    FUNC -.-> |模拟| DOCKER_APPS
    SQL -.-> |模拟| SQL_DOCKER
    COSMOS -.-> |模拟| COSMOS_EMU
    SB -.-> |模拟| SB_EMU
    STORAGE -.-> |模拟| AZURITE
    APPINS -.-> |模拟| OTEL
    EVENTGRID -.-> |模拟| JAEGER
```

## 🔧 Azure服务本地模拟映射详解

### 核心服务映射表

| Azure服务 | 本地模拟方案 | 端口映射 | 兼容性 | 功能差异 |
|-----------|-------------|----------|--------|----------|
| **API Management** | Nginx反向代理 | 80, 443 | 基础功能 | 缺少XML策略引擎、开发者门户 |
| **Azure SQL Database** | SQL Server 2022容器 | 1433 | 100% | 完全兼容，支持所有T-SQL功能 |
| **Cosmos DB** | Cosmos DB Linux模拟器 | 8081 | 95% | 支持SQL API，部分功能受限 |
| **Service Bus** | Service Bus模拟器 | 5672 | 90% | 支持队列、主题、订阅 |
| **Azure Storage** | Azurite模拟器 | 10000-10002 | 95% | 支持Blob、Queue、Table |
| **Application Insights** | OpenTelemetry + Jaeger | 4317, 16686 | 85% | APM功能完整，缺少Azure集成 |
| **Container Apps** | Docker容器 | 5001, 5002 | 100% | 完全等价的容器运行时 |
| **Azure Functions** | 容器化Functions Runtime | 7071, 7072 | 95% | 支持大部分绑定和触发器 |
| **Event Grid** | 本地事件模拟 | N/A | 80% | 基础事件发布订阅 |

### 详细模拟方案

#### 1. API Management → Nginx

**生产环境功能**:
```xml
<!-- API Management XML策略 -->
<policies>
    <inbound>
        <validate-jwt header-name="Authorization" require-scheme="Bearer">
            <openid-config url="https://login.microsoftonline.com/common/.well-known/openid_configuration" />
        </validate-jwt>
        <rate-limit-by-key calls="100" renewal-period="60" />
    </inbound>
</policies>
```

**本地模拟实现**:
```nginx
# config/nginx.conf (推荐配置)
upstream external-api {
    server external-order-api:80;
}

server {
    listen 80;
    
    location /api/external/ {
        proxy_pass http://external-api/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        
        # 基础限流
        limit_req zone=api burst=20 nodelay;
    }
}
```

#### 2. Azure SQL Database → SQL Server容器

**连接字符串对比**:
```csharp
// 生产环境
"Server=bidone-sql-prod.database.windows.net;Database=BidOneDB;Authentication=Active Directory Default;"

// 开发环境
"Server=localhost,1433;Database=BidOneDB;User Id=sa;Password=BidOne123!;TrustServerCertificate=true"
```

#### 3. Cosmos DB → 官方Linux模拟器

**连接配置**:
```csharp
// 开发环境使用固定的模拟器Key
"AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
```

## 🚀 开发模式详解

项目支持两种开发模式，适应不同的开发场景和个人偏好：

### 模式1: 完全容器化环境

**适用场景**: 演示、集成测试、团队协作

```bash
# 一键启动完整环境
./docker-dev.sh start
```

**特点**:
- ✅ 所有服务均在容器中运行
- ✅ 环境一致性最佳
- ✅ 无需本地安装.NET SDK
- ❌ 代码修改需重新构建镜像
- ❌ 调试相对复杂

**服务列表**:
```
✅ Infrastructure: SQL Server, Redis, Cosmos DB, Service Bus, Azurite
✅ Applications: External API, Internal API, Functions, AI Functions  
✅ Monitoring: Prometheus, Grafana, Jaeger, OpenTelemetry
✅ Gateway: Nginx反向代理
```

### 模式2: 混合开发环境（推荐）

**适用场景**: 日常开发、调试、快速迭代

```bash
# 启动基础设施，应用在IDE中运行
./docker-dev.sh infra
```

**特点**:
- ✅ 基础设施容器化，稳定可靠
- ✅ 应用本地运行，支持热重载和断点调试
- ✅ 快速代码修改和测试
- ✅ IDE集成调试体验最佳
- ❌ 需要本地安装.NET 8.0 SDK

**然后在IDE或命令行中运行应用**:
```bash
# Terminal 1: External Order API
cd src/ExternalOrderApi
dotnet run  # → http://localhost:5001

# Terminal 2: Internal System API  
cd src/InternalSystemApi
dotnet run  # → http://localhost:5002

# Terminal 3: Order Integration Function
cd src/OrderIntegrationFunction
func start  # → http://localhost:7071

# Terminal 4: Customer Communication Function
cd src/CustomerCommunicationFunction
func start --port 7072  # → http://localhost:7072
```

## 📦 环境搭建详细步骤

### 系统要求

| 组件 | 最低要求 | 推荐配置 |
|------|----------|----------|
| **操作系统** | Windows 10/11, macOS 10.15+, Ubuntu 18.04+ | 最新版本 |
| **Docker Desktop** | 4.0+ | 最新版本 |
| **内存** | 8GB | 16GB+ |
| **磁盘空间** | 20GB | 50GB+ |
| **CPU** | 4核 | 8核+ |

### 开发工具（模式2需要）

| 工具 | 版本 | 用途 |
|------|------|------|
| **.NET SDK** | 8.0+ | API项目编译运行 |
| **Azure Functions Core Tools** | 4.0+ | Functions本地运行 |
| **Visual Studio 2022** 或 **VS Code** | 最新版 | 代码编辑和调试 |
| **Azure CLI** | 2.50+ | Azure部署（可选） |

### 一键环境搭建

#### 步骤1: 克隆项目
```bash
git clone <repository-url>
cd FoodOrderConnect
chmod +x docker-dev.sh  # Linux/macOS需要
```

#### 步骤2: 检查Docker环境
```bash
# 验证Docker是否正常运行
docker --version
docker-compose --version
docker info
```

#### 步骤3: 启动环境（选择一种模式）

**完全容器化模式**:
```bash
./docker-dev.sh start
```

**混合开发模式**:
```bash
./docker-dev.sh infra
```

#### 步骤4: 数据库初始化（重要！）

⚠️ **首次启动必须执行数据库初始化，否则API无法正常启动**

```bash
# 等待SQL Server容器完全启动（约30秒）
./docker-dev.sh status

# 初始化数据库结构
cd src/InternalSystemApi
dotnet ef migrations add InitialCreate    # 创建迁移文件（首次）
dotnet ef database update               # 应用数据库迁移
```

#### 步骤5: 验证环境
```bash
# 检查所有服务状态
./docker-dev.sh status

# 测试API健康检查
curl http://localhost:5001/health  # External Order API
curl http://localhost:5002/health  # Internal System API
```

## 🔐 认证和安全模拟

### 认证架构分层

项目采用**分层认证策略**，不同服务层有不同的认证实现：

| 服务层级 | 生产环境认证 | 开发环境模拟 | 安全级别 |
|---------|-------------|-------------|----------|
| **API Gateway层** | API Management + Azure AD | Nginx（无认证） | 🔓 开放 |
| **External Order API** | API Management策略验证 | 无认证（便于测试） | 🔓 开放 |  
| **Internal System API** | Azure AD + JWT | 应用层JWT模拟 | 🔒 受保护 |
| **Azure Functions** | Function Key + Azure AD | Function-level认证 | 🔐 中等保护 |

### External Order API - 开放访问模拟

**设计考虑**: 外部订单API在开发环境中完全开放，便于API测试和集成测试。

```bash
# 直接访问，无需认证
curl -X POST http://localhost:5001/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "customer-001",
    "items": [{"productId": "FOOD-001", "quantity": 2, "unitPrice": 25.00}]
  }'
```

### Internal System API - JWT本地模拟

**JWT配置详解**:
```csharp
// 开发环境JWT配置
{
  "Jwt": {
    "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "BidOne.InternalSystemApi", 
    "Audience": "BidOne.Clients"
  }
}
```

**JWT Token生成示例**（开发调试用）:
```csharp
// 简单的Token生成逻辑（仅供开发使用）
var tokenHandler = new JwtSecurityTokenHandler();
var key = Encoding.ASCII.GetBytes("YourSuperSecretKeyThatIsAtLeast32CharactersLong!");
var tokenDescriptor = new SecurityTokenDescriptor
{
    Subject = new ClaimsIdentity(new[] { new Claim("sub", "dev-user") }),
    Expires = DateTime.UtcNow.AddDays(1),
    Issuer = "BidOne.InternalSystemApi",
    Audience = "BidOne.Clients",
    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
};
var token = tokenHandler.CreateToken(tokenDescriptor);
return tokenHandler.WriteToken(token);
```

**API调用示例**:
```bash
# 需要有效的JWT Token
curl -X POST http://localhost:5002/api/orders \
  -H "Authorization: Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9..." \
  -H "Content-Type: application/json" \
  -d '{"orderId": "test-order"}'
```

### Azure Functions - Function Key认证

**本地Functions认证**:
```bash
# 使用默认的开发Function Key
curl -X POST "http://localhost:7071/api/OrderValidationFunction?code=<function-key>" \
  -H "Content-Type: application/json" \
  -d '{"orderId": "test"}'
```

## 🌐 服务访问地址

### 应用服务

| 服务 | 本地地址 | 用途 | 认证要求 |
|------|----------|------|----------|
| **External Order API** | http://localhost:5001 | 外部订单接收 | 无 |
| **Internal System API** | http://localhost:5002 | 内部系统集成 | JWT Token |
| **Order Integration Function** | http://localhost:7071 | 订单处理中间件 | Function Key |
| **AI Communication Function** | http://localhost:7072 | AI客户沟通 | Function Key |

### 基础设施服务

| 服务 | 本地地址 | 默认凭据 | 用途 |
|------|----------|----------|------|
| **SQL Server** | localhost:1433 | sa/BidOne123! | 主数据库 |
| **Redis** | localhost:6380 | 无密码 | 缓存服务 |
| **Cosmos DB模拟器** | https://localhost:8081/_explorer | 固定Key | NoSQL数据库 |
| **Azurite存储** | localhost:10000-10002 | 默认账户 | 文件存储模拟 |
| **Service Bus模拟器** | localhost:5672 | 默认Key | 消息队列 |

### 监控和管理

| 工具 | 本地地址 | 默认凭据 | 功能 |
|------|----------|----------|------|
| **Grafana仪表盘** | http://localhost:3000 | admin/admin123 | 业务监控仪表盘 |
| **Prometheus** | http://localhost:9090 | 无 | 指标收集查询 |
| **Jaeger UI** | http://localhost:16686 | 无 | 分布式追踪 |

### API文档和测试

| 服务 | Swagger UI | 健康检查 |
|------|------------|----------|
| **External Order API** | http://localhost:5001 | http://localhost:5001/health |
| **Internal System API** | http://localhost:5002 | http://localhost:5002/health |

## 🛠️ 开发工作流

### 代码修改和部署

#### 完全容器化模式
```bash
# 修改代码后重新构建特定服务
./docker-dev.sh rebuild external-order-api

# 重新构建所有应用服务
./docker-dev.sh rebuild-all

# 查看构建和运行日志
./docker-dev.sh logs external-order-api
```

#### 混合开发模式
```bash
# 代码修改后自动热重载（推荐）
cd src/ExternalOrderApi
dotnet watch run  # 文件修改自动重启

# 或手动重启
dotnet run
```

### 日志查看和调试

#### 查看容器服务日志
```bash
# 查看所有服务日志
./docker-dev.sh logs

# 查看特定服务日志
./docker-dev.sh logs external-order-api
./docker-dev.sh logs sqlserver
./docker-dev.sh logs prometheus

# 实时跟踪日志
./docker-dev.sh logs external-order-api -f
```

#### 应用级别调试
```bash
# 开启详细日志级别
export ASPNETCORE_ENVIRONMENT=Development
export Logging__LogLevel__Default=Debug

# 在IDE中设置断点调试
# Visual Studio: F5启动调试
# VS Code: Ctrl+F5启动调试
```

### 数据库管理

#### SQL Server数据库操作
```bash
# 连接数据库
docker exec -it bidone-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P BidOne123! -C

# 查看数据库
1> SELECT name FROM sys.databases;
2> GO

# 查看表结构
1> USE BidOneDB;
2> SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES;
3> GO
```

#### Entity Framework迁移
```bash
cd src/InternalSystemApi

# 创建新迁移
dotnet ef migrations add MigrationName

# 应用迁移
dotnet ef database update

# 回滚迁移
dotnet ef database update PreviousMigrationName

# 查看迁移历史
dotnet ef migrations list
```

#### Cosmos DB数据浏览
访问 https://localhost:8081/_explorer/index.html 使用内置的数据浏览器。

### 性能监控和调试

#### Grafana仪表盘使用
1. 访问 http://localhost:3000
2. 登录：admin/admin123
3. 查看预配置的业务仪表盘
4. 监控订单处理指标、API响应时间等

#### Jaeger分布式追踪
1. 访问 http://localhost:16686
2. 选择服务：external-order-api, internal-system-api
3. 查看请求追踪链，分析性能瓶颈

#### Prometheus指标查询
1. 访问 http://localhost:9090
2. 查询业务指标：
   - `bidone_orders_processed_total` - 订单处理总数
   - `bidone_api_request_duration_seconds` - API响应时间
   - `bidone_pending_orders_count` - 待处理订单数

## 🔧 故障排查指南

### 常见问题和解决方案

#### 1. 端口冲突问题

**症状**: 服务启动失败，提示端口被占用
```
Error: bind: address already in use
```

**解决方案**:
```bash
# 检查端口占用
./docker-dev.sh status  # 会自动检查端口冲突

# 手动检查特定端口
lsof -i :6380  # Redis端口
lsof -i :1433  # SQL Server端口

# 停止冲突服务或修改docker-compose.yml中的端口映射
```

#### 2. SQL Server连接失败

**症状**: API启动失败，数据库连接超时
```
Microsoft.Data.SqlClient.SqlException: A network-related or instance-specific error occurred
```

**解决方案**:
```bash
# 1. 检查SQL Server容器状态
docker ps | grep sqlserver

# 2. 查看SQL Server启动日志
./docker-dev.sh logs sqlserver

# 3. 等待SQL Server完全启动（首次启动需要较长时间）
# 4. 检查健康状态
./docker-dev.sh status

# 5. 手动测试连接
docker exec -it bidone-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P BidOne123! -C -Q "SELECT 1"
```

#### 3. Cosmos DB模拟器启动失败

**症状**: Cosmos DB容器无法启动或证书错误
```
curl: (60) SSL certificate problem: self signed certificate
```

**解决方案**:
```bash
# 1. 下载并信任模拟器证书
curl -k https://localhost:8081/_explorer/emulator.pem > cosmos_emulator.crt

# 2. 在应用中忽略SSL验证（仅开发环境）
# ConnectionString中已包含此配置

# 3. 重启Cosmos DB容器
docker restart bidone-cosmosdb
```

#### 4. Service Bus连接问题

**症状**: Functions无法连接到Service Bus
```
Microsoft.Azure.ServiceBus.ServiceBusException: No connection could be made
```

**解决方案**:
```bash
# 1. 检查Service Bus模拟器状态
./docker-dev.sh logs servicebus

# 2. 验证连接字符串格式
echo "Endpoint=sb://servicebus:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fake-key"

# 3. 重启Service Bus容器
docker restart bidone-servicebus
```

#### 5. Functions无法启动

**症状**: Azure Functions容器启动失败
```
Error: Functions host is not running
```

**解决方案**:
```bash
# 1. 检查Azurite存储模拟器
./docker-dev.sh logs azurite

# 2. 验证AzureWebJobsStorage连接字符串
# 3. 检查Functions运行时版本
./docker-dev.sh logs order-integration-function

# 4. 本地运行Functions进行调试
cd src/OrderIntegrationFunction
func start --verbose
```

### 服务依赖诊断

```bash
# 完整的服务健康检查
./docker-dev.sh status

# 检查服务间网络连接
docker network ls | grep bidone
docker exec -it bidone-external-api ping bidone-sqlserver
```

### 清理和重置

```bash
# 轻量级重置（保留镜像）
./docker-dev.sh reset

# 完全清理（删除应用镜像）
./docker-dev.sh cleanup

# 核弹级清理（删除所有镜像，包括基础镜像）
./docker-dev.sh cleanup --force
```

## 📊 配置文件详解

### Docker Compose核心配置

#### 网络配置
```yaml
networks:
  bidone-network:
    driver: bridge  # 所有服务在同一网络中互相通信
```

#### 卷管理
```yaml
volumes:
  sqlserver-data:      # SQL Server数据持久化
  cosmosdb-data:       # Cosmos DB数据持久化  
  redis-data:          # Redis数据持久化
  prometheus-data:     # Prometheus指标数据
  grafana-data:        # Grafana配置和仪表盘
```

#### 服务依赖关系
```yaml
depends_on:
  sqlserver:
    condition: service_healthy  # 等待SQL Server健康检查通过
  redis:
    condition: service_healthy  # 等待Redis启动
  servicebus:
    condition: service_started  # 等待Service Bus启动
```

### 环境变量配置

#### 开发环境变量
```yaml
environment:
  ASPNETCORE_ENVIRONMENT: Development
  ConnectionStrings__DefaultConnection: "Server=sqlserver,1433;Database=BidOneDB;User Id=sa;Password=BidOne123!;TrustServerCertificate=true"
  ConnectionStrings__Redis: "redis:6379"
  ConnectionStrings__ServiceBus: "Endpoint=sb://servicebus:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fake-key"
```

#### 生产环境对比
```yaml
# 生产环境使用Azure托管服务
ConnectionStrings__DefaultConnection: "Server=bidone-sql-prod.database.windows.net;Authentication=Active Directory Default;"
ConnectionStrings__ServiceBus: "Endpoint=sb://bidone-sb-prod.servicebus.windows.net/;Authentication=Active Directory Default;"
```

### 认证配置差异

#### JWT配置
```json
// 开发环境 - 固定密钥
{
  "Jwt": {
    "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "BidOne.InternalSystemApi",
    "Audience": "BidOne.Clients"
  }
}

// 生产环境 - Azure Key Vault
{
  "Jwt": {
    "SecretKey": "@Microsoft.KeyVault(SecretUri=https://bidone-kv.vault.azure.net/secrets/jwt-secret/)",
    "Issuer": "BidOne.InternalSystemApi", 
    "Audience": "BidOne.Clients"
  }
}
```

## 🚀 最佳实践建议

### 开发工作流推荐

1. **日常开发**: 使用混合模式（`./docker-dev.sh infra`）
2. **功能测试**: 使用完全容器化模式（`./docker-dev.sh start`）
3. **集成测试**: 确保所有服务容器化运行
4. **性能测试**: 使用生产级配置和资源限制

### 性能优化建议

#### Docker配置优化
```yaml
# 为CPU密集型服务分配更多资源
services:
  order-integration-function:
    deploy:
      resources:
        limits:
          memory: 2G
          cpus: '2'
        reservations:
          memory: 1G
          cpus: '1'
```

#### 开发机器优化
```bash
# Docker Desktop内存分配建议
# Settings → Resources → Advanced
# Memory: 8GB+ (推荐12GB)
# CPUs: 4+ (推荐6+)
# Disk image size: 100GB+
```

### 安全最佳实践

#### 开发环境安全
- 使用固定的开发密钥，避免生产密钥泄露
- 定期更新容器镜像到最新版本
- 限制开放端口，仅绑定localhost接口

#### 配置管理
- 敏感配置使用环境变量
- 开发和生产配置严格分离
- 使用.gitignore排除本地配置文件

## 📝 总结

本地开发环境通过精心设计的容器化架构，提供了与Azure生产环境高度一致的开发体验。关键优势包括：

✅ **高保真模拟**: 使用官方Azure服务模拟器，最大程度保证兼容性

✅ **开发效率**: 支持热重载、断点调试等现代开发体验

✅ **一键部署**: 自动化脚本简化环境管理

✅ **完整监控**: 集成Grafana、Jaeger等工具，提供生产级可观测性

✅ **灵活切换**: 支持完全容器化和混合开发两种模式

通过遵循本指南，开发者可以快速搭建稳定、高效的本地开发环境，确保开发质量和生产一致性。

---

📞 **技术支持**: 如遇到问题，请查看 [故障排查指南](#-故障排查指南) 或提交Issue。