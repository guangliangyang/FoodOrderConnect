# BidOne Integration Platform - 系统架构文档

## 🎯 架构概述

BidOne Integration Platform 是一个展示**现代云原生架构与 AI 智能集成**的企业级订单处理系统，核心理念是 **"Never Lose an Order"** + **"AI-Powered Customer Experience"**。

## 🏛️ 设计原则

### 核心原则
1. **🛡️ 可靠性优先**: 确保订单处理的高可用性和数据一致性
2. **🔄 事件驱动**: 异步消息传递和松耦合设计
3. **🤖 AI 增强**: 智能错误处理和客户沟通自动化
4. **📊 可观察性**: 全面的监控、日志和业务洞察
5. **🔒 安全第一**: 端到端的安全防护和密钥管理
6. **⚡ 高性能**: 支持水平扩展和高并发处理

### 架构模式
- **🔗 事件驱动架构**: Service Bus + Event Grid 异步通信
- **🏗️ 微服务架构**: 服务解耦和独立部署  
- **🧠 AI 集成模式**: LangChain + OpenAI 智能处理
- **📦 容器化部署**: Docker + Azure Container Apps
- **🔄 CQRS + 事件溯源**: 命令查询分离和事件存储

## 🏗️ 系统架构图

### 整体架构
```mermaid
graph TB
    subgraph "🌐 外部接入层"
        Client[餐厅客户端]
        Mobile[移动应用]
        Partner[合作伙伴系统]
    end
    
    subgraph "🚪 API 网关层" 
        APIM[Azure API Management<br/>🔐 认证授权<br/>🚦 限流熔断<br/>📊 API 监控]
    end
    
    subgraph "⚙️ 微服务层"
        ExtAPI[External Order API<br/>🛒 订单接收<br/>✅ 数据验证<br/>📤 事件发布]
        IntAPI[Internal System API<br/>🏭 订单处理<br/>📦 库存管理<br/>🤝 供应商对接]
    end
    
    subgraph "🧠 智能处理层"
        OrderFunc[Order Integration Function<br/>📋 订单验证<br/>🔍 数据丰富化<br/>⚡ 业务流程]
        AIFunc[Customer Communication Function<br/>🤖 AI 错误分析<br/>💬 智能客户沟通<br/>📧 自动化通知]
        LogicApp[Azure Logic Apps<br/>🔄 工作流编排<br/>🔗 系统集成]
    end
    
    subgraph "📡 消息传递层"
        SB[Service Bus<br/>📬 可靠消息传递<br/>🔄 重试机制<br/>💀 死信处理]
        EG[Event Grid<br/>⚡ 事件驱动通信<br/>🔔 实时通知<br/>📡 系统解耦]
    end
    
    subgraph "💾 数据存储层"
        SQL[(SQL Database<br/>📊 业务数据<br/>🔄 事务处理)]
        Cosmos[(Cosmos DB<br/>📦 产品目录<br/>🌍 全球分布)]
        Redis[(Redis Cache<br/>⚡ 高速缓存<br/>🎯 会话存储)]
    end
    
    subgraph "🔒 安全与监控"
        KV[Key Vault<br/>🔐 密钥管理<br/>🛡️ 证书存储]
        AI_Insights[Application Insights<br/>📊 应用监控<br/>🔍 性能分析]
        Grafana[Grafana<br/>📈 业务仪表板<br/>📊 实时指标]
    end
    
    subgraph "🤖 AI 服务"
        OpenAI[OpenAI API<br/>🧠 智能分析<br/>💬 内容生成]
        LangChain[LangChain<br/>🔗 AI 工作流<br/>📝 提示工程]
    end
    
    %% 数据流向
    Client --> APIM
    Mobile --> APIM  
    Partner --> APIM
    APIM --> ExtAPI
    APIM --> IntAPI
    
    ExtAPI --> SB
    SB --> OrderFunc
    OrderFunc --> SB
    SB --> IntAPI
    
    %% AI 智能处理流
    ExtAPI -.-> EG
    IntAPI -.-> EG
    EG --> AIFunc
    AIFunc --> OpenAI
    AIFunc --> LangChain
    AIFunc --> SB
    
    %% 数据访问
    ExtAPI --> Redis
    IntAPI --> SQL
    OrderFunc --> Cosmos
    AIFunc --> Redis
    
    %% 监控流
    ExtAPI --> AI_Insights
    IntAPI --> AI_Insights
    OrderFunc --> AI_Insights
    AIFunc --> AI_Insights
    AI_Insights --> Grafana
```

### AI 智能沟通架构详图
```mermaid
sequenceDiagram
    participant Order as 订单处理失败
    participant SB as Service Bus<br/>high-value-errors
    participant EG as Event Grid<br/>System Topic
    participant AI as AI Communication<br/>Function
    participant LC as LangChain<br/>Service
    participant OpenAI as OpenAI<br/>API
    participant Notify as Notification<br/>Service
    
    Order->>SB: 发布高价值错误事件
    SB->>EG: 触发 Event Grid 系统事件
    EG->>AI: 实时触发 AI 处理函数
    
    AI->>LC: 1. 错误智能分析
    LC->>OpenAI: AI 分析请求
    OpenAI-->>LC: 分析结果
    LC-->>AI: 错误原因和影响评估
    
    AI->>LC: 2. 生成客户消息
    LC->>OpenAI: 个性化消息生成
    OpenAI-->>LC: 客户沟通内容
    LC-->>AI: 专业道歉和解决方案
    
    AI->>LC: 3. 生成行动建议
    LC->>OpenAI: 运营建议生成
    OpenAI-->>LC: 可执行行动计划
    LC-->>AI: 内部处理建议
    
    AI->>Notify: 4. 发送客户通知
    AI->>Notify: 5. 发送内部警报
    Notify-->>AI: 通知发送完成
    
    Note over AI,Notify: 整个流程 < 5秒完成
    Note over LC,OpenAI: 支持优雅降级到智能模拟
```

## 核心组件详细设计

### 1. External Order API

**职责**: 接收外部订单请求，进行基础验证和格式化

**技术栈**:
- ASP.NET Core 8.0
- Entity Framework Core
- FluentValidation
- Serilog

**核心功能**:
```csharp
// 订单接收端点
[HttpPost("orders")]
public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
{
    // 1. 请求验证
    var validationResult = await _validator.ValidateAsync(request);
    if (!validationResult.IsValid)
        return BadRequest(validationResult.Errors);
    
    // 2. 转换为领域模型
    var order = _mapper.Map<Order>(request);
    
    // 3. 发送到消息队列
    await _serviceBusPublisher.PublishAsync(order);
    
    // 4. 返回确认
    return Accepted(new { OrderId = order.Id, Status = "Received" });
}
```

**关键设计决策**:
- **快速响应**: 立即返回确认，异步处理降低响应时间
- **幂等性**: 支持重复提交检测
- **限流保护**: 集成 API Management 限流策略

### 2. Azure Logic Apps 工作流

**职责**: 业务流程编排和路由决策

**工作流设计**:
```json
{
    "definition": {
        "triggers": {
            "when_message_received": {
                "type": "ServiceBus",
                "inputs": {
                    "queueName": "order-received",
                    "subscriptionName": "order-processor"
                }
            }
        },
        "actions": {
            "validate_order": {
                "type": "Function",
                "inputs": {
                    "functionName": "ValidateOrder"
                }
            },
            "enrich_order_data": {
                "type": "Function",
                "inputs": {
                    "functionName": "EnrichOrderData"
                }
            },
            "route_to_internal_system": {
                "type": "Http",
                "inputs": {
                    "method": "POST",
                    "uri": "@{parameters('internalApiEndpoint')}/orders"
                }
            }
        }
    }
}
```

### 3. Azure Functions

**职责**: 复杂业务逻辑处理和数据转换

**关键函数**:

#### OrderValidationFunction
```csharp
[FunctionName("ValidateOrder")]
public async Task<IActionResult> ValidateOrder(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
    [ServiceBus("validation-results", Connection = "ServiceBusConnection")] IAsyncCollector<ValidationResult> outputQueue)
{
    // 业务规则验证
    // 库存检查
    // 供应商能力验证
}
```

#### OrderEnrichmentFunction
```csharp
[FunctionName("EnrichOrderData")]
public async Task<IActionResult> EnrichOrderData(
    [ServiceBusTrigger("enrichment-queue")] Order order,
    [CosmosDB("BidOneDB", "Products", Connection = "CosmosDBConnection")] IDocumentClient documentClient)
{
    // 商品信息补全
    // 价格计算
    // 配送信息enrichment
}
```

### 4. Internal System API

**职责**: 内部系统集成和订单状态管理

**核心实现**:
```csharp
[HttpPost("orders")]
[Authorize]
public async Task<IActionResult> ProcessOrder([FromBody] ProcessOrderRequest request)
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync();
    try
    {
        // 1. 保存订单到数据库
        var order = await _orderService.CreateOrderAsync(request);
        
        // 2. 更新库存
        await _inventoryService.ReserveItemsAsync(order.Items);
        
        // 3. 发送确认事件
        await _eventPublisher.PublishOrderConfirmedAsync(order);
        
        await transaction.CommitAsync();
        return Ok(new { OrderId = order.Id, Status = "Confirmed" });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

## 数据架构设计

### 数据模型

#### 订单聚合根 (Order Aggregate)
```csharp
public class Order : AggregateRoot
{
    public OrderId Id { get; private set; }
    public CustomerId CustomerId { get; private set; }
    public SupplierId SupplierId { get; private set; }
    public List<OrderItem> Items { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    
    // 业务方法
    public void Confirm() { /* ... */ }
    public void Cancel() { /* ... */ }
    public void AddItem(OrderItem item) { /* ... */ }
}
```

### 数据存储策略

| 数据类型 | 存储方案 | 原因 |
|----------|----------|------|
| 订单事务数据 | Azure SQL Database | ACID特性，强一致性 |
| 产品目录 | Azure Cosmos DB | 高读取性能，全局分发 |
| 用户会话 | Redis Cache | 快速访问，自动过期 |
| 审计日志 | Azure Storage | 长期存储，成本优化 |

## 消息架构设计

### 消息流设计

```
OrderReceived -> OrderValidation -> OrderEnrichment -> OrderConfirmation
     ↓               ↓                  ↓                ↓
  [Service Bus]  [Service Bus]    [Service Bus]    [Event Grid]
```

### 消息类型定义

#### OrderReceivedEvent
```csharp
public record OrderReceivedEvent(
    string OrderId,
    string CustomerId,
    List<OrderItem> Items,
    DateTime ReceivedAt
) : IIntegrationEvent;
```

#### OrderConfirmedEvent
```csharp
public record OrderConfirmedEvent(
    string OrderId,
    string ConfirmationId,
    DateTime ConfirmedAt,
    decimal TotalAmount
) : IIntegrationEvent;
```

## 安全架构

### 认证授权流程

```mermaid
sequenceDiagram
    participant C as Client
    participant APIM as API Management
    participant AAD as Azure AD
    participant API as Internal API
    
    C->>AAD: 1. 获取访问令牌
    AAD->>C: 2. 返回 JWT Token
    C->>APIM: 3. 请求 + JWT Token
    APIM->>APIM: 4. 验证和转换令牌
    APIM->>API: 5. 转发请求
    API->>C: 6. 返回响应
```

### 安全策略

1. **API级安全**:
   - OAuth 2.0 / OpenID Connect
   - JWT Token 验证
   - API Key 管理
   - 请求签名验证

2. **网络安全**:
   - VNet 网络隔离
   - NSG 网络安全组
   - WAF Web应用防火墙
   - DDoS 保护

3. **数据安全**:
   - TDE 透明数据加密
   - Column级加密
   - Azure Key Vault 密钥管理
   - RBAC 访问控制

## 可观测性架构

### 监控策略

#### 三大支柱

1. **Metrics (指标)**:
   ```csharp
   // 自定义指标示例
   _telemetryClient.TrackMetric("OrderProcessingTime", processingTime);
   _telemetryClient.TrackMetric("OrdersPerMinute", ordersPerMinute);
   ```

2. **Logs (日志)**:
   ```csharp
   // 结构化日志
   _logger.LogInformation("Order {OrderId} processed successfully for customer {CustomerId}", 
       order.Id, order.CustomerId);
   ```

3. **Traces (链路追踪)**:
   ```csharp
   // 分布式追踪
   using var activity = _activitySource.StartActivity("ProcessOrder");
   activity?.SetTag("order.id", orderId);
   activity?.SetTag("customer.id", customerId);
   ```

### 关键性能指标 (KPIs)

| 指标 | 目标值 | 告警阈值 |
|------|--------|----------|
| 订单处理成功率 | >99.9% | <99.5% |
| 端到端延迟 | <2s | >5s |
| API可用性 | >99.95% | <99.9% |
| 消息处理延迟 | <1s | >3s |

## 容灾和高可用

### 高可用设计

1. **多区域部署**: 
   - 主区域: East US
   - 灾备区域: West US 2

2. **数据复制策略**:
   - SQL Database: 地理复制
   - Cosmos DB: 多区域写入
   - Storage: GRS 地理冗余

3. **故障切换**:
   - 自动故障检测
   - DNS流量管理器
   - 应用层重试机制

### 灾难恢复

- **RTO (Recovery Time Objective)**: 15分钟
- **RPO (Recovery Point Objective)**: 1分钟
- **备份策略**: 
  - 数据库每小时增量备份
  - 每日完整备份
  - 跨区域备份复制

## 性能优化策略

### 缓存策略

1. **L1缓存**: 应用内存缓存
2. **L2缓存**: Redis分布式缓存  
3. **CDN**: 静态资源缓存

### 数据库优化

1. **读写分离**: 读副本分流查询
2. **分区策略**: 按时间和地理位置分区
3. **索引优化**: 覆盖索引和复合索引

### API优化

1. **分页**: 大数据集分页返回
2. **压缩**: Gzip响应压缩
3. **并发控制**: 合理的连接池配置

## 部署架构

### 环境策略

| 环境 | 用途 | 配置 |
|------|------|------|
| Development | 开发测试 | 单实例，共享资源 |
| Staging | 预生产验证 | 生产级配置 |
| Production | 生产环境 | 高可用，多实例 |

### CI/CD 流水线

```yaml
# 简化的流水线配置
stages:
  - build
  - test
  - security-scan
  - deploy-staging
  - integration-test
  - deploy-production
```

## 成本优化

### 资源优化策略

1. **自动扩缩容**: 根据负载自动调整实例数
2. **预留实例**: 生产环境使用预留实例
3. **存储分层**: 冷数据迁移到低成本存储
4. **监控告警**: 成本异常告警

### 成本预估

| 组件 | 月成本(USD) | 说明 |
|------|-------------|------|
| Container Apps | $200 | 3实例标准配置 |
| Azure SQL Database | $300 | S2标准层 |
| Service Bus | $50 | 标准层 |
| Application Insights | $100 | 基础监控 |
| **总计** | **$650** | 预估月成本 |

---

本架构文档将随着项目发展持续更新和完善。