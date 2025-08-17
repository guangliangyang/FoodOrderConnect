# BidOne Integration Platform - 开发者运行指南

## 🎯 概述

作为开发者，你有三种方式在本地运行和开发这个系统。本指南将详细介绍每种方式的优缺点和使用场景。

### 🔧 开发者工具亮点

本项目提供了强大的 **`docker-dev.sh`** 开发管理脚本，让你可以轻松管理整个开发环境：

```bash
./docker-dev.sh start          # 🚀 一键启动完整环境
./docker-dev.sh rebuild external-order-api  # 🔨 修改代码后重建
./docker-dev.sh status         # 📊 查看所有服务状态
./docker-dev.sh logs [service] # 📝 查看实时日志
./docker-dev.sh cleanup        # 🧹 完全清理环境
```

这比传统的 `docker-compose` 命令更智能、更便捷！

## 📋 前置要求

### 必需工具
- **.NET 8.0 SDK** - [下载地址](https://dotnet.microsoft.com/download)
- **Docker Desktop** - [下载地址](https://www.docker.com/products/docker-desktop)
- **Git** - 版本控制

### 可选工具
- **Azure Functions Core Tools** - 本地运行Azure Functions ([安装指南](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local))
- **Azure CLI** - Azure 部署和管理 ([安装指南](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli))
- **Visual Studio 2022** 或 **VS Code** - IDE

### 系统要求
- **内存**: 至少 8GB (推荐 16GB)
- **磁盘**: 至少 10GB 可用空间
- **端口**: 确保以下端口未被占用：
  - 1433 (SQL Server)
  - 6379 (Redis)
  - 8080-8081 (APIs), 8081 (Cosmos DB)
  - 10000-10254 (Azurite & Cosmos DB additional ports)
  - 3000, 9090 (Grafana, Prometheus)
  - 5672 (Service Bus)

## 🚀 三种运行模式

### 模式一：完全容器化运行 (推荐新手和演示)

**🎯 适用场景**: 
- 首次接触项目
- 快速演示系统功能
- 完整的AI功能测试
- 不需要频繁修改代码

**✅ 优势**:
- 一键启动所有服务
- 环境完全隔离
- 包含完整的AI功能
- 与生产环境高度一致

**❌ 劣势**:
- 代码修改需要重新构建镜像
- 调试相对困难
- 资源消耗较大

#### 启动步骤

```bash
# 1. 克隆项目（如果还没有）
git clone <repository-url>
cd FoodOrderConnect

# 2. 一键启动所有服务
./docker-dev.sh start

# 3. 🚨 首次运行：初始化数据库（重要！）
# 等待容器启动完成后，需要创建数据库表结构
cd src/InternalSystemApi
dotnet ef migrations add InitialCreate    # 创建迁移文件（首次运行）
dotnet ef database update               # 应用迁移，创建表结构

# 4. 验证数据库初始化
# 进入数据库检查表是否创建成功
docker exec -it bidone-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P BidOne123! -C -N
# 在 sqlcmd 中执行：
# 1> USE BidOneDB;
# 2> SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';
# 3> GO
# 应该看到: Orders, OrderItems, Customers, Products 等表

# 5. 查看服务状态
./docker-dev.sh status

# 6. 查看日志
./docker-dev.sh logs external-order-api
```

> **⚠️ 重要提醒：首次运行必须执行数据库初始化！**
> 
> 如果跳过步骤3，API会因为找不到数据库表而启动失败。这是Entity Framework项目的标准初始化流程。

#### 服务地址

| 服务 | 地址 | 说明 |
|------|------|------|
| **External Order API** | http://localhost:5001 | 外部订单接收API |
| **Internal System API** | http://localhost:5002 | 内部系统API |
| **Grafana** | http://localhost:3000 | 业务监控仪表板 (admin/admin123) |
| **Prometheus** | http://localhost:9090 | 指标收集系统 |
| **Jaeger** | http://localhost:16686 | 分布式链路追踪 |
| **SQL Server** | localhost:1433 | 数据库 (sa/BidOne123!) |
| **Redis** | localhost:6379 | 缓存服务 |
| **Cosmos DB** | https://localhost:8081 | 文档数据库模拟器 |
| **Azurite** | http://localhost:10000 | Azure Storage模拟器 |

#### 开发管理命令

```bash
# 🚀 完整的开发环境管理
./docker-dev.sh start          # 启动所有服务
./docker-dev.sh stop           # 停止所有服务 
./docker-dev.sh restart        # 重启所有服务
./docker-dev.sh status         # 查看服务状态和健康检查

# 🔧 代码开发和调试
./docker-dev.sh rebuild external-order-api  # 重建特定服务（修改代码后）
./docker-dev.sh rebuild-all    # 重建所有应用服务
./docker-dev.sh logs           # 查看所有服务日志
./docker-dev.sh logs external-order-api     # 查看特定服务日志

# 🧹 环境清理
./docker-dev.sh cleanup        # 完全清理环境

# 📖 帮助信息
./docker-dev.sh help           # 查看所有可用命令
```

#### 代码修改工作流

```bash
# 💡 当你修改了 ExternalOrderAPI 代码后：
./docker-dev.sh rebuild external-order-api

# 💡 当你修改了多个服务代码后：
./docker-dev.sh rebuild-all

# 💡 实时查看重建后的日志：
./docker-dev.sh logs external-order-api -f
```

#### 测试命令

```bash
# 发送正常订单
curl -X POST http://localhost:5001/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "customer-001",
    "items": [{"productId": "FOOD-001", "quantity": 5, "unitPrice": 25.00}],
    "deliveryDate": "2024-12-20T10:00:00Z"
  }'

# 触发AI智能错误处理
curl -X POST http://localhost:5001/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "premium-customer-001",
    "items": [{"productId": "INVALID-PRODUCT", "quantity": 100, "unitPrice": 50.00}],
    "deliveryDate": "2024-12-20T10:00:00Z"
  }'

# 观察AI处理日志
./docker-dev.sh logs customer-communication-function
```

### 模式二：混合开发模式 (推荐日常开发)

**🎯 适用场景**:
- 日常开发和调试
- 频繁修改代码
- 需要断点调试
- 性能调优

**✅ 优势**:
- 代码热重载
- 可以使用IDE断点调试
- 启动速度快
- 资源消耗适中

**❌ 劣势**:
- 需要手动管理多个进程
- 环境配置相对复杂

#### 启动步骤

```bash
# 1. 启动基础设施服务（数据库、缓存等）
./docker-dev.sh infra

# 🚨 重要：等待所有基础设施服务完全就绪
# 观察到 "Infrastructure services started successfully!" 消息
# 建议等待30-60秒确保Redis、SQL Server等服务完全初始化

# 2. 🚨 首次运行：初始化数据库（重要！）
# 确保基础设施服务完全启动后
cd src/InternalSystemApi
dotnet ef migrations add InitialCreate    # 创建迁移文件（首次运行）
dotnet ef database update               # 应用迁移，创建表结构

# 3. 验证数据库初始化（可选）
docker exec bidone-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P BidOne123! -C -N -Q "USE BidOneDB; SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';"
# 应该返回大于1的数字（包含业务表）

# 4a. 使用IDE运行API项目（推荐）
# 在 Visual Studio 或 VS Code 中：
# - 设置启动项目为 ExternalOrderApi
# - 按 F5 开始调试

# 3b. 或使用命令行运行
# 终端1: External Order API (http://localhost:5001)
cd src/ExternalOrderApi
dotnet run

# 终端2: Internal System API (http://localhost:5002)
cd src/InternalSystemApi
dotnet run

# 终端3: Order Integration Function (http://localhost:7071)
cd src/OrderIntegrationFunction
func start

# 终端4: Customer Communication Function (http://localhost:7072)
cd src/CustomerCommunicationFunction
func start --port 7072
```

#### 配置说明

项目已预配置了正确的端口分配：

**混合开发模式** (本地运行):
- External Order API: http://localhost:5001 & https://localhost:7001
- Internal System API: http://localhost:5002 & https://localhost:7002
- Order Function: http://localhost:7071
- AI Function: http://localhost:7072

**完全容器化模式**:
- External Order API: http://localhost:5001 (容器映射)
- Internal System API: http://localhost:5002 (容器映射)
- 基础设施服务端口保持一致

配置文件说明：
- `Properties/launchSettings.json` - 本地开发端口配置
- `appsettings.Development.json` - API项目配置（指向Docker服务）
- `local.settings.json` - Azure Functions配置

🎯 **设计优势**: 两种模式使用相同的端口，确保开发体验一致性。

#### 调试技巧

```bash
# 查看特定服务日志
./scripts/view-logs.sh redis -f
./scripts/view-logs.sh sqlserver -f

# 进入数据库查看数据
docker exec -it bidone-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P BidOne123! -C -N

# 查看Redis数据
docker exec -it bidone-redis redis-cli

# 重启单个服务
docker-compose restart redis
```

### 模式三：纯本地开发模式 (高级开发者)

**🎯 适用场景**:
- 高级开发者
- 性能调优和分析
- 集成开发环境定制
- 离线开发

**✅ 优势**:
- 最大的灵活性
- 最佳性能
- 完全控制环境

**❌ 劣势**:
- 环境搭建复杂
- 需要安装多个本地服务
- 配置管理复杂

#### 本地服务安装

```bash
# SQL Server LocalDB (Windows)
# 随 Visual Studio 或 SQL Server Express 安装

# Redis (macOS)
brew install redis
brew services start redis

# Redis (Ubuntu)
sudo apt update
sudo apt install redis-server
sudo systemctl start redis

# Cosmos DB Emulator
# 下载并安装 Azure Cosmos DB Emulator
```

#### 配置文件修改

需要修改连接字符串指向本地服务：

```json
// appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BidOneDB_Dev;Trusted_Connection=True;",
    "Redis": "localhost:6379",
    "ServiceBus": "本地Service Bus配置"
  }
}
```

## 🔧 开发工具和脚本

### 健康检查和监控

```bash
# 使用开发脚本检查（推荐）
./docker-dev.sh status             # 完整的服务状态检查

# 使用专用健康检查脚本
./scripts/health-check.sh           # 详细的健康检查
./scripts/health-check.sh --wait    # 等待服务就绪

# 手动检查API端点
curl -f http://localhost:5001/health  # External API
curl -f http://localhost:5002/health  # Internal API
```

### 构建和测试

```bash
# 构建所有项目
./scripts/build-all.sh

# 或手动构建
dotnet build BidOne.sln

# 运行测试
dotnet test

# 运行特定项目的测试
dotnet test tests/ExternalOrderApi.Tests/

# 容器化测试（使用完整环境）
./docker-dev.sh start               # 启动完整环境
./docker-dev.sh rebuild external-order-api  # 重建后测试
```

### 代码格式化

```bash
# 格式化代码
dotnet format

# 格式化并修复样式问题
dotnet format --fix-style --fix-analyzers
```

### 数据库管理

```bash
# 创建数据库迁移
dotnet ef migrations add MigrationName --project src/InternalSystemApi

# 应用数据库迁移
dotnet ef database update --project src/InternalSystemApi

# 删除数据库（重置）
dotnet ef database drop --project src/InternalSystemApi
```

## 🐛 常见问题解决

### 1. 🚨 首次运行：数据库表不存在

**问题症状**：
- API启动失败，日志显示 "Invalid object name 'Orders'"
- 数据库中只有 `__EFMigrationsHistory` 表
- Entity Framework 相关错误

**解决方案**：
```bash
# 1. 确保基础设施服务运行
./docker-dev.sh infra

# 2. 创建并应用数据库迁移
cd src/InternalSystemApi
dotnet ef migrations add InitialCreate    # 首次运行必需
dotnet ef database update               # 创建所有表

# 3. 验证表是否创建成功
docker exec bidone-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P BidOne123! -C -N -Q "USE BidOneDB; SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';"

# 应该看到: Orders, OrderItems, Customers, Products, Inventory 等表
```

**预防措施**：
- 首次克隆项目后，始终先执行数据库初始化
- 团队新成员必须运行此步骤
- 可以将此步骤添加到项目onboarding流程

### 2. Redis连接失败 (混合开发模式)

**问题症状**：
- API启动时出现 `RedisConnectionException: UnableToConnect`
- 错误信息包含 "Connection refused (127.0.0.1:6379)"
- API返回500错误，无法处理订单

**常见原因**：
- 基础设施服务未启动或Redis容器未完全就绪
- API启动过快，Redis还在初始化中
- Redis连接超时配置过短

**解决方案**：
```bash
# 1. 确认基础设施服务状态
./docker-dev.sh status
# 确保看到：bidone-redis running (healthy)

# 2. 测试Redis连接
docker exec bidone-redis redis-cli ping
# 应该返回：PONG

# 3. 测试端口连接
nc -zv localhost 6379
# 应该显示：Connection to localhost port 6379 [tcp/*] succeeded!

# 4. 如果Redis未启动，重新启动基础设施
./docker-dev.sh infra

# 5. 等待所有服务完全就绪（约30-60秒）
sleep 60

# 6. 重新启动API
cd src/ExternalOrderApi
dotnet run
```

**预防措施**：
- 在启动API前，确保 `./docker-dev.sh infra` 已完成并显示所有服务健康
- 观察到 "Infrastructure services started successfully!" 消息后再启动API
- 如果仍有问题，可能需要增加Redis初始化等待时间

### 3. 端口被占用

```bash
# 查看端口占用
netstat -tulpn | grep :8080
lsof -ti:8080  # macOS

# 停止占用进程
kill -9 <PID>
```

### 2. Docker服务启动失败

```bash
# 使用开发脚本诊断（推荐）
./docker-dev.sh status             # 查看服务状态
./docker-dev.sh logs <service-name> # 查看特定服务日志
./docker-dev.sh rebuild <service-name>  # 重建问题服务

# 手动诊断
docker-compose logs <service-name>
docker-compose build --no-cache <service-name>

# 完全重置环境
./docker-dev.sh cleanup            # 使用脚本清理
# 或手动清理
docker-compose down -v
docker system prune -f
```

### 3. 数据库连接失败

```bash
# 检查SQL Server状态
docker exec bidone-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P BidOne123! -Q "SELECT 1" -C -N

# 使用开发脚本重启（推荐）
./docker-dev.sh restart

# 或手动重置数据库
docker-compose restart sqlserver
sleep 30  # 等待数据库完全启动
```

### 4. AI功能不工作

```bash
# 使用开发脚本检查（推荐）
./docker-dev.sh logs customer-communication-function
./docker-dev.sh rebuild customer-communication-function  # 如果需要重建

# 手动检查
docker-compose logs customer-communication-function

# 如果没有OpenAI API Key，系统会使用智能模拟模式
# 这是正常的，会在日志中看到 "Using intelligent mock response" 消息
```

### 5. Grafana无数据

```bash
# 使用开发脚本检查
./docker-dev.sh status             # 检查所有监控服务状态
./docker-dev.sh restart            # 重启所有服务

# 手动检查
curl http://localhost:9090/api/v1/targets  # 检查Prometheus目标
docker-compose restart grafana

# 访问 http://localhost:3000，账号: admin/admin123
```

## 📝 开发最佳实践

### 1. Git工作流

```bash
# 创建功能分支
git checkout -b feature/your-feature-name

# 提交前运行测试
dotnet test
dotnet format

# 提交代码
git add .
git commit -m "feat: add new feature"
```

### 2. 日志记录

在代码中使用结构化日志：

```csharp
_logger.LogInformation("Processing order: {OrderId} for customer: {CustomerId}", 
    order.Id, order.CustomerId);
```

### 3. 错误处理

使用合适的异常处理：

```csharp
try
{
    await _orderService.ProcessOrderAsync(order);
}
catch (ValidationException ex)
{
    _logger.LogWarning("Order validation failed: {ValidationErrors}", ex.Errors);
    return BadRequest(ex.Errors);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error processing order: {OrderId}", order.Id);
    return StatusCode(500, "Internal server error");
}
```

### 4. 测试策略

```bash
# 单元测试
dotnet test tests/ExternalOrderApi.Tests/

# 集成测试（需要Docker服务运行）
./docker-dev.sh start
dotnet test tests/IntegrationTests/

# API测试（使用完整环境）
./docker-dev.sh start
curl -X POST http://localhost:5001/orders -H "Content-Type: application/json" -d @test-data/valid-order.json
```

## 🔄 环境重置

如果遇到问题需要重置环境，有两种清理选项：

### 快速重置（推荐日常使用）
```bash
# 快速重置 - 保留镜像，下次启动更快
./docker-dev.sh reset
./docker-dev.sh start          # 重新启动

# 🔄 保留的内容：
#   - 基础镜像（postgres, redis, nginx等）
#   - BidOne应用镜像
# ✅ 清理的内容：
#   - 所有容器和服务
#   - 所有卷和网络
#   - 悬空镜像和构建缓存
```

### 完全清理（重置应用镜像）
```bash
# 完全清理 - 删除自定义应用镜像，保留基础设施镜像
./docker-dev.sh cleanup
./docker-dev.sh start          # 重新启动（需要重新构建应用镜像）

# ✅ 清理的内容：
#   - 所有容器和服务
#   - BidOne自定义应用镜像
#   - 所有卷和网络
#   - 悬空镜像和构建缓存
# 🔄 保留的内容：
#   - 基础设施镜像（SQL Server, Redis, Cosmos DB等）
#   - 第三方镜像（nginx, grafana, prometheus等）
```

### 核心清理（仅在必要时使用）
```bash
# 🚨 核心清理 - 删除所有镜像包括基础设施镜像
./docker-dev.sh cleanup --force
# 需要输入 'YES' 确认，会删除SQL Server、Redis等大型镜像
# 下次启动需要重新下载所有镜像（可能需要较长时间）

# ✅ 清理的内容：
#   - 所有容器和服务
#   - 所有项目相关镜像（包括基础设施镜像）
#   - 所有卷和网络
#   - 悬空镜像和构建缓存
```

### 代码级重置
```bash
# 清理.NET构建产物
dotnet clean
rm -rf **/bin **/obj

# 重置Git状态（谨慎使用）
git clean -fd
git reset --hard HEAD
```

## 📚 进阶主题

### 性能分析

```bash
# 使用dotnet-counters监控性能
dotnet tool install --global dotnet-counters
dotnet-counters monitor --process-id <pid>

# 使用dotnet-trace进行性能分析
dotnet tool install --global dotnet-trace
dotnet-trace collect --process-id <pid>
```

### 容器化开发

```bash
# 仅构建特定服务
docker-compose build external-order-api

# 在容器中运行特定命令
docker-compose exec external-order-api dotnet ef database update
```

### Azure集成

```bash
# 部署到Azure
./scripts/deploy-to-azure.sh --environment dev --resource-group bidone-demo-rg

# 配置Azure连接字符串
az keyvault secret set --vault-name bidone-kv-dev --name "ConnectionString" --value "..."
```

## 🆘 获取帮助

如果遇到问题：

1. **查看日志**: 使用 `docker-compose logs <service-name>` 查看详细错误信息
2. **检查文档**: 参考 [故障排除指南](troubleshooting.md)
3. **重置环境**: 使用上面的环境重置步骤
4. **联系维护者**: guangliang.yang@hotmail.com

---

## 🎯 下一步

现在你已经了解了如何在本地运行系统，建议：

1. **从模式二开始** - 混合开发模式最适合日常开发
2. **阅读架构文档** - 了解系统设计原理
3. **查看演示指南** - 学习如何展示系统功能
4. **参与开发** - 开始添加新功能或修复问题

祝你开发愉快！🚀