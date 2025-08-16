# BidOne Integration Platform Demo

## 项目概述

这是一个完整的餐饮业务集成平台演示项目，展示从订单接收到供应商处理的完整业务流程，体现 **"Never Lose an Order"** 的核心理念。

## 业务场景

**核心业务流程：餐厅订单到批发供应商的集成平台**

### 主要角色
- **餐厅客户**: 通过系统下订单
- **BidOne平台**: 中间集成平台  
- **批发供应商**: 接收和处理订单
- **物流服务**: 配送管理

### 业务流程
1. 餐厅通过 External Order API 下单
2. 订单数据经过验证和丰富化处理
3. 通过 Azure Logic Apps 编排业务流程
4. Azure Functions 处理复杂业务逻辑和数据转换
5. 通过 Internal System API 更新内部系统
6. 实时监控和告警确保订单不丢失

## 技术架构

### 核心组件

| 组件 | 技术栈 | 功能 |
|------|--------|------|
| External Order API | C#/.NET, ASP.NET Core | 外部订单接收 |
| Internal System API | C#/.NET, ASP.NET Core | 内部系统集成 |
| Order Integration Function | C#, Azure Functions | 订单处理逻辑 |
| Integration Workflow | Azure Logic Apps | 业务流程编排 |
| Message Queue | Azure Service Bus | 异步消息处理 |
| API Gateway | Azure API Management | API 统一管理 |
| Monitoring | Application Insights | 应用监控 |

### 部署架构

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│  External       │    │   Azure Logic    │    │   Internal      │
│  Order API      │───▶│   Apps           │───▶│   System API    │
│                 │    │                  │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│  Azure Service  │    │   Azure          │    │   Database      │
│  Bus            │    │   Functions      │    │   (SQL/Cosmos)  │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

## 快速开始

### 前置要求
- .NET 8.0 SDK
- Azure CLI
- Docker Desktop
- Visual Studio 2022 或 VS Code

### 本地开发环境搭建

```bash
# 克隆项目
git clone https://github.com/your-org/BidOne-Integration-Demo.git
cd BidOne-Integration-Demo

# 运行开发环境设置脚本
./scripts/setup-dev-env.sh

# 启动本地服务
./scripts/start-local-services.sh
```

### 部署到 Azure

```bash
# 登录 Azure
az login

# 部署基础设施
az deployment group create \
  --resource-group bidone-demo-rg \
  --template-file infra/main.bicep \
  --parameters infra/parameters.json

# 部署应用服务
./scripts/deploy-to-azure.sh
```

## 项目结构

```
├── .github/workflows/     # CI/CD 管道
├── docs/                  # 项目文档
├── infra/                 # 基础设施即代码 (IaC)
├── src/                   # 源代码
│   ├── ExternalOrderApi/
│   ├── InternalSystemApi/
│   └── OrderIntegrationFunction/
├── tests/                 # 测试代码
├── scripts/               # 开发脚本
├── shared/                # 共享代码库
└── monitoring/            # 监控配置
```

## 核心特性

### 🔒 可靠性保证
- **消息持久化**: Azure Service Bus 确保消息不丢失
- **重试机制**: 自动重试失败的操作
- **死信队列**: 处理异常消息
- **事务一致性**: 确保数据完整性

### 📊 可观测性
- **分布式链路追踪**: Application Insights 端到端追踪
- **实时监控**: 关键业务指标监控
- **智能告警**: 异常情况自动告警
- **性能分析**: 详细的性能指标分析

### 🚀 高性能架构
- **异步处理**: 基于事件驱动的异步架构
- **弹性伸缩**: Container Apps 自动扩缩容
- **缓存优化**: Redis 分布式缓存
- **负载均衡**: Azure Load Balancer

### 🔐 安全最佳实践
- **身份认证**: Azure AD 集成
- **API 安全**: OAuth 2.0 + JWT
- **网络安全**: VNet 隔离
- **密钥管理**: Azure Key Vault

## 监控和运维

### 健康检查端点
- External Order API: `/health`
- Internal System API: `/health` 
- Azure Functions: `/api/health`

### 关键指标
- 订单处理成功率 > 99.9%
- 端到端延迟 < 2秒
- API 可用性 > 99.95%

## 贡献指南

1. Fork 项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

## 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 联系方式

- 项目维护者: [Ricky Yang]
- 邮箱: guangliang.yang@hotmail.com
- 项目链接: https://github.com/test-org/BidOne-Integration-Demo