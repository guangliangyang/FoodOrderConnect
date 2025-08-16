# 故障排除指南

## 概述

本文档提供 BidOne Integration Platform 常见问题的诊断和解决方案，帮助开发和运维团队快速定位和解决系统问题。

## 目录

- [监控和诊断工具](#监控和诊断工具)
- [API 服务问题](#api-服务问题)
- [Azure Functions 问题](#azure-functions-问题)
- [Azure Logic Apps 问题](#azure-logic-apps-问题)
- [数据库连接问题](#数据库连接问题)
- [消息队列问题](#消息队列问题)
- [网络和安全问题](#网络和安全问题)
- [性能问题](#性能问题)
- [部署问题](#部署问题)
- [监控和日志](#监控和日志)

## 监控和诊断工具

### 快速健康检查脚本

```bash
#!/bin/bash
# health-check.sh - 系统健康状态快速检查

echo "=== BidOne Integration Platform 健康检查 ==="
echo "检查时间: $(date)"
echo

# 检查 API 服务状态
echo "1. 检查 API 服务..."
curl -s -o /dev/null -w "External Order API: %{http_code}\n" \
    https://bidone-apim-prod.azure-api.net/external/orders/health

curl -s -o /dev/null -w "Internal System API: %{http_code}\n" \
    https://bidone-apim-prod.azure-api.net/internal/orders/health

# 检查 Azure Functions
echo -e "\n2. 检查 Azure Functions..."
az functionapp show --name bidone-functions-prod --resource-group bidone-demo-rg \
    --query "state" --output tsv

# 检查 Logic Apps
echo -e "\n3. 检查 Logic Apps..."
az logic workflow show --resource-group bidone-demo-rg \
    --name bidone-order-workflow --query "state" --output tsv

# 检查数据库连接
echo -e "\n4. 检查数据库..."
# SQL Database
sqlcmd -S bidone-sql-prod.database.windows.net -d BidOneDB -U sqladmin \
    -Q "SELECT 1 as HealthCheck" -o /dev/null && echo "SQL Database: OK" || echo "SQL Database: FAILED"

echo -e "\n=== 健康检查完成 ==="
```

### 日志聚合查询脚本

```powershell
# log-analyzer.ps1 - 日志分析脚本
param(
    [Parameter(Mandatory=$true)]
    [string]$TimeRange = "1h",
    
    [Parameter(Mandatory=$false)]
    [string]$LogLevel = "Error"
)

# Application Insights 错误查询
$ErrorQuery = @"
exceptions
| where timestamp > ago($TimeRange)
| where cloud_RoleName in ('external-order-api', 'internal-system-api', 'order-integration-function')
| where severityLevel >= 3
| project timestamp, cloud_RoleName, message, operation_Name
| order by timestamp desc
| take 50
"@

Write-Host "查询最近 $TimeRange 的错误日志..."
az monitor log-analytics query --workspace bidone-logs-prod --analytics-query $ErrorQuery --output table
```

## API 服务问题

### 1. 容器无法启动

**症状**: Container Apps 显示"Failed"状态，容器反复重启

**诊断步骤**:

```bash
# 查看容器日志
az containerapp logs show \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --follow

# 检查容器配置
az containerapp show \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --query "properties.template.containers[0]"
```

**常见原因和解决方案**:

#### a) 环境变量配置错误
```bash
# 检查环境变量
az containerapp show \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --query "properties.template.containers[0].env"

# 修复环境变量
az containerapp update \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --set-env-vars ASPNETCORE_ENVIRONMENT=Production
```

#### b) 镜像拉取失败
```bash
# 检查 ACR 访问权限
az acr check-health --name bidonecr

# 重新推送镜像
docker push bidonecr.azurecr.io/bidone/external-order-api:latest

# 重启容器应用
az containerapp update \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --image bidonecr.azurecr.io/bidone/external-order-api:latest
```

#### c) 端口配置错误
```bash
# 检查端口配置
az containerapp show \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --query "properties.template.containers[0].probes"

# 更新端口配置
az containerapp update \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --target-port 80
```

### 2. API 响应超时

**症状**: 客户端请求超时，API 响应时间过长

**诊断查询**:
```kusto
requests
| where timestamp > ago(1h)
| where cloud_RoleName == "external-order-api"
| where duration > 5000  // 超过5秒的请求
| project timestamp, name, url, duration, resultCode
| order by duration desc
```

**解决方案**:

#### a) 增加容器资源
```bash
az containerapp update \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --cpu 2.0 \
    --memory 4Gi
```

#### b) 优化数据库查询
```csharp
// 添加数据库连接池配置
services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.CommandTimeout(30);
        sqlOptions.EnableRetryOnFailure(3);
    });
}, ServiceLifetime.Scoped);
```

#### c) 启用缓存
```csharp
// 添加响应缓存
services.AddResponseCaching();
services.AddMemoryCache();

// 在控制器中使用缓存
[ResponseCache(Duration = 300)]
public async Task<IActionResult> GetProducts()
{
    // 实现缓存逻辑
}
```

### 3. 高内存使用

**症状**: 容器内存使用率过高，可能导致 OOM 错误

**诊断步骤**:
```bash
# 查看容器资源使用情况
az monitor metrics list \
    --resource "/subscriptions/{subscription-id}/resourceGroups/bidone-demo-rg/providers/Microsoft.App/containerApps/external-order-api" \
    --metric "MemoryPercentage" \
    --interval PT1M
```

**解决方案**:

#### a) 分析内存泄漏
```csharp
// 在 Startup.cs 中添加内存监控
services.Configure<GCSettings>(options =>
{
    options.GCMemoryInfo = true;
});

// 添加内存诊断中间件
public class MemoryDiagnosticsMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var before = GC.GetTotalMemory(false);
        await _next(context);
        var after = GC.GetTotalMemory(false);
        
        if (after - before > 1024 * 1024) // 超过1MB
        {
            _logger.LogWarning("High memory allocation: {MemoryDiff}MB", 
                (after - before) / 1024 / 1024);
        }
    }
}
```

#### b) 优化对象生命周期
```csharp
// 使用对象池
services.AddSingleton<ObjectPool<StringBuilder>>(serviceProvider =>
{
    var provider = new DefaultObjectPoolProvider();
    return provider.Create(new StringBuilderPooledObjectPolicy());
});

// 及时释放资源
using var httpClient = _httpClientFactory.CreateClient();
using var response = await httpClient.GetAsync(url);
```

## Azure Functions 问题

### 1. Function 触发失败

**症状**: Service Bus 消息未能触发 Function 执行

**诊断步骤**:
```bash
# 查看 Function 日志
az functionapp log tail \
    --name bidone-functions-prod \
    --resource-group bidone-demo-rg

# 检查 Service Bus 连接
az servicebus namespace authorization-rule keys list \
    --resource-group bidone-demo-rg \
    --namespace-name bidone-sb-prod \
    --name RootManageSharedAccessKey
```

**解决方案**:

#### a) 检查连接字符串
```bash
# 更新 Function App 配置
az functionapp config appsettings set \
    --name bidone-functions-prod \
    --resource-group bidone-demo-rg \
    --settings ServiceBusConnection="Endpoint=sb://bidone-sb-prod.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=..."
```

#### b) 重新同步触发器
```bash
az functionapp function sync-function-triggers \
    --name bidone-functions-prod \
    --resource-group bidone-demo-rg
```

#### c) 检查队列权限
```csharp
// 确保 Function 具有正确的权限
[FunctionName("ProcessOrder")]
public async Task ProcessOrder(
    [ServiceBusTrigger("order-queue", Connection = "ServiceBusConnection")] 
    string orderMessage,
    ILogger log)
{
    try
    {
        log.LogInformation($"Processing order: {orderMessage}");
        // 处理逻辑
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Error processing order");
        throw; // 重新抛出异常以触发重试
    }
}
```

### 2. Function 执行超时

**症状**: Function 执行时间超过配置的超时时间

**解决方案**:

#### a) 调整超时配置
```json
// host.json
{
    "version": "2.0",
    "functionTimeout": "00:10:00",
    "extensions": {
        "serviceBus": {
            "messageHandlerOptions": {
                "maxConcurrentCalls": 32,
                "maxAutoRenewDuration": "00:55:00"
            }
        }
    }
}
```

#### b) 优化代码性能
```csharp
// 使用异步操作和并发处理
public async Task ProcessOrderAsync(Order order)
{
    var tasks = new List<Task>
    {
        ValidateOrderAsync(order),
        EnrichOrderDataAsync(order),
        UpdateInventoryAsync(order)
    };
    
    await Task.WhenAll(tasks);
}
```

### 3. Function 冷启动问题

**症状**: Function 第一次执行时延迟较高

**解决方案**:

#### a) 使用预热功能
```csharp
[FunctionName("WarmUp")]
public async Task WarmUp(
    [WarmupTrigger] WarmupContext context,
    ILogger log)
{
    log.LogInformation("Function warm-up triggered");
    
    // 预加载依赖项
    await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
    await _httpClient.GetStringAsync("https://api.example.com/health");
}
```

#### b) 使用 Premium 计划
```bash
# 升级到 Premium 计划
az functionapp plan create \
    --resource-group bidone-demo-rg \
    --name bidone-functions-premium \
    --location eastus \
    --sku EP1

az functionapp update \
    --name bidone-functions-prod \
    --resource-group bidone-demo-rg \
    --plan bidone-functions-premium
```

## Azure Logic Apps 问题

### 1. Logic App 工作流失败

**症状**: Logic App 执行失败，显示错误状态

**诊断步骤**:
```bash
# 查看最近的执行记录
az logic workflow run list \
    --resource-group bidone-demo-rg \
    --workflow-name bidone-order-workflow \
    --top 10

# 查看具体失败详情
az logic workflow run show \
    --resource-group bidone-demo-rg \
    --workflow-name bidone-order-workflow \
    --run-name {run-id}
```

**常见问题和解决方案**:

#### a) 连接器认证失败
```bash
# 重新授权连接器
az logic workflow update \
    --resource-group bidone-demo-rg \
    --name bidone-order-workflow \
    --definition @logic-app-definition.json
```

#### b) HTTP 请求超时
```json
{
    "type": "Http",
    "inputs": {
        "method": "POST",
        "uri": "@{parameters('apiEndpoint')}",
        "headers": {
            "Content-Type": "application/json"
        },
        "body": "@{triggerBody()}",
        "timeout": "PT2M",
        "retryPolicy": {
            "type": "fixed",
            "count": 3,
            "interval": "PT30S"
        }
    }
}
```

### 2. 消息处理积压

**症状**: Service Bus 队列中消息积压，Logic App 处理缓慢

**解决方案**:

#### a) 启用并行处理
```json
{
    "definition": {
        "triggers": {
            "when_message_received": {
                "type": "ServiceBus",
                "inputs": {
                    "queueName": "order-queue"
                },
                "recurrence": {
                    "frequency": "Second",
                    "interval": 30
                },
                "splitOn": "@triggerBody()?['messages']"
            }
        }
    }
}
```

#### b) 增加并发连接数
```bash
# 更新 Logic App 运行时配置
az logic workflow update \
    --resource-group bidone-demo-rg \
    --name bidone-order-workflow \
    --set properties.definition.parameters.maxConcurrency.value=50
```

## 数据库连接问题

### 1. 连接池耗尽

**症状**: "Pool or semaphore limit exceeded" 错误

**解决方案**:

#### a) 优化连接池配置
```csharp
// appsettings.json
{
    "ConnectionStrings": {
        "DefaultConnection": "Server=bidone-sql-prod.database.windows.net;Database=BidOneDB;User Id=sqladmin;Password=***;Max Pool Size=200;Min Pool Size=10;Connection Timeout=30;"
    }
}

// 使用连接池监控
services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString)
           .LogTo(Console.WriteLine, LogLevel.Information)
           .EnableSensitiveDataLogging(isDevelopment);
});
```

#### b) 实现连接池监控
```csharp
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _context;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy("Database is accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database is not accessible", ex);
        }
    }
}
```

### 2. 查询性能问题

**症状**: 数据库查询执行时间过长

**诊断工具**:
```sql
-- 查看长时间运行的查询
SELECT 
    s.session_id,
    r.status,
    r.command,
    r.cpu_time,
    r.total_elapsed_time,
    t.text
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
INNER JOIN sys.dm_exec_sessions s ON r.session_id = s.session_id
WHERE r.total_elapsed_time > 5000
ORDER BY r.total_elapsed_time DESC;

-- 查看缺失的索引建议
SELECT 
    dm_mid.database_id,
    dm_migs.avg_total_user_cost * (dm_migs.avg_user_impact / 100.0) * (dm_migs.user_seeks + dm_migs.user_scans) AS improvement_measure,
    'CREATE INDEX [missing_index_' + CONVERT(varchar, dm_mig.index_group_handle) + '_' + CONVERT(varchar, dm_mid.index_handle) + ']'
    + ' ON ' + dm_mid.statement
    + ' (' + ISNULL(dm_mid.equality_columns,'')
    + CASE WHEN dm_mid.equality_columns IS NOT NULL AND dm_mid.inequality_columns IS NOT NULL THEN ',' ELSE '' END
    + ISNULL(dm_mid.inequality_columns, '') + ')'
    + ISNULL(' INCLUDE (' + dm_mid.included_columns + ')', '') AS create_index_statement
FROM sys.dm_db_missing_index_groups dm_mig
INNER JOIN sys.dm_db_missing_index_group_stats dm_migs ON dm_migs.group_handle = dm_mig.index_group_handle
INNER JOIN sys.dm_db_missing_index_details dm_mid ON dm_mig.index_handle = dm_mid.index_handle
WHERE dm_migs.avg_total_user_cost * (dm_migs.avg_user_impact / 100.0) * (dm_migs.user_seeks + dm_migs.user_scans) > 10
ORDER BY improvement_measure DESC;
```

**优化方案**:

#### a) 添加索引
```sql
-- 为订单查询添加复合索引
CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_CreatedAt 
ON Orders (CustomerId, CreatedAt)
INCLUDE (Status, TotalAmount);

-- 为产品查询添加索引
CREATE NONCLUSTERED INDEX IX_Products_Category_IsActive 
ON Products (Category, IsActive)
INCLUDE (Name, Price, Description);
```

#### b) 查询优化
```csharp
// 使用分页查询
public async Task<PagedResult<Order>> GetOrdersAsync(
    string customerId, 
    int page = 1, 
    int pageSize = 20)
{
    var query = _context.Orders
        .Where(o => o.CustomerId == customerId)
        .OrderByDescending(o => o.CreatedAt);

    var totalCount = await query.CountAsync();
    var orders = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Include(o => o.Items)
        .ToListAsync();

    return new PagedResult<Order>(orders, totalCount, page, pageSize);
}

// 使用投影减少数据传输
public async Task<List<OrderSummary>> GetOrderSummariesAsync(string customerId)
{
    return await _context.Orders
        .Where(o => o.CustomerId == customerId)
        .Select(o => new OrderSummary
        {
            Id = o.Id,
            CreatedAt = o.CreatedAt,
            TotalAmount = o.TotalAmount,
            Status = o.Status
        })
        .ToListAsync();
}
```

## 消息队列问题

### 1. 消息积压

**症状**: Service Bus 队列中未处理消息数量持续增长

**诊断步骤**:
```bash
# 查看队列统计信息
az servicebus queue show \
    --resource-group bidone-demo-rg \
    --namespace-name bidone-sb-prod \
    --name order-queue \
    --query "{ActiveMessages:messageCount, DeadLetters:deadLetterMessageCount}"
```

**解决方案**:

#### a) 增加消费者实例
```bash
# 扩展 Function App 实例
az functionapp config set \
    --name bidone-functions-prod \
    --resource-group bidone-demo-rg \
    --always-on true

# 调整并发处理数量
az functionapp config appsettings set \
    --name bidone-functions-prod \
    --resource-group bidone-demo-rg \
    --settings FUNCTIONS_WORKER_PROCESS_COUNT=4
```

#### b) 优化消息处理逻辑
```csharp
// 批量处理消息
[FunctionName("ProcessOrdersBatch")]
public async Task ProcessOrdersBatch(
    [ServiceBusTrigger("order-queue", Connection = "ServiceBusConnection", 
                       IsSessionsEnabled = false)] string[] messages,
    ILogger log)
{
    var batchSize = Math.Min(messages.Length, 10);
    var batches = messages.Chunk(batchSize);

    await Task.WhenAll(batches.Select(async batch =>
    {
        try
        {
            await ProcessOrderBatchAsync(batch);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error processing order batch");
        }
    }));
}
```

### 2. 死信队列消息

**症状**: 消息被移动到死信队列

**诊断步骤**:
```bash
# 查看死信队列消息
az servicebus queue show \
    --resource-group bidone-demo-rg \
    --namespace-name bidone-sb-prod \
    --name order-queue \
    --query "deadLetterMessageCount"

# 查看死信消息详情（使用 Service Bus Explorer 或自定义工具）
```

**解决方案**:

#### a) 分析死信原因
```csharp
// 添加死信消息处理
[FunctionName("ProcessDeadLetterMessages")]
public async Task ProcessDeadLetterMessages(
    [ServiceBusTrigger("order-queue/$DeadLetterQueue", Connection = "ServiceBusConnection")] 
    ServiceBusReceivedMessage message,
    ServiceBusMessageActions messageActions,
    ILogger log)
{
    try
    {
        log.LogWarning("Processing dead letter message: {MessageId}, DeadLetterReason: {Reason}", 
            message.MessageId, message.DeadLetterReason);

        // 分析死信原因并重新处理
        if (message.DeadLetterReason == "MaxDeliveryCountExceeded")
        {
            // 手动处理或发送到管理员
            await NotifyAdministratorAsync(message);
        }

        await messageActions.CompleteMessageAsync(message);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Error processing dead letter message");
        await messageActions.AbandonMessageAsync(message);
    }
}
```

#### b) 实现重试策略
```csharp
// 在 host.json 中配置重试策略
{
    "version": "2.0",
    "extensions": {
        "serviceBus": {
            "messageHandlerOptions": {
                "maxConcurrentCalls": 16,
                "maxDeliveryCount": 5,
                "maxAutoRenewDuration": "00:05:00"
            }
        }
    }
}

// 在代码中实现指数退避
public async Task ProcessOrderWithRetryAsync(Order order)
{
    var retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                _logger.LogWarning("Retry {RetryCount} for order {OrderId} in {Delay}ms", 
                    retryCount, order.Id, timespan.TotalMilliseconds);
            });

    await retryPolicy.ExecuteAsync(async () =>
    {
        await ProcessOrderInternalAsync(order);
    });
}
```

## 网络和安全问题

### 1. SSL/TLS 证书问题

**症状**: HTTPS 连接失败，证书验证错误

**诊断步骤**:
```bash
# 检查证书状态
openssl s_client -connect bidone-apim-prod.azure-api.net:443 -servername bidone-apim-prod.azure-api.net

# 检查证书过期时间
echo | openssl s_client -connect bidone-apim-prod.azure-api.net:443 2>/dev/null | openssl x509 -noout -dates
```

**解决方案**:

#### a) 更新证书
```bash
# 如果使用自定义域名，更新证书
az network application-gateway ssl-cert create \
    --gateway-name bidone-appgw \
    --resource-group bidone-demo-rg \
    --name bidone-ssl-cert \
    --cert-file /path/to/certificate.pfx \
    --cert-password "certificate_password"
```

#### b) 验证证书链
```csharp
// 在应用程序中添加证书验证日志
services.Configure<HttpClientFactoryOptions>("MyHttpClient", options =>
{
    options.HttpClientActions.Add(client =>
    {
        client.DefaultRequestHeaders.Add("User-Agent", "BidOne-Integration/1.0");
    });
    
    options.HttpMessageHandlerBuilderActions.Add(builder =>
    {
        builder.AdditionalHandlers.Add(new CertificateValidationHandler());
    });
});

public class CertificateValidationHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("SSL"))
        {
            _logger.LogError(ex, "SSL certificate validation failed for {Uri}", request.RequestUri);
            throw;
        }
    }
}
```

### 2. 防火墙和网络访问问题

**症状**: 网络连接超时，无法访问外部服务

**诊断步骤**:
```bash
# 检查网络安全组规则
az network nsg rule list \
    --resource-group bidone-demo-rg \
    --nsg-name bidone-nsg-prod \
    --output table

# 测试网络连通性
az network watcher connectivity-check \
    --resource-group bidone-demo-rg \
    --source-resource "/subscriptions/{subscription-id}/resourceGroups/bidone-demo-rg/providers/Microsoft.App/containerApps/external-order-api" \
    --dest-address "api.external-supplier.com" \
    --dest-port 443
```

**解决方案**:

#### a) 更新防火墙规则
```bash
# 添加出站规则
az network nsg rule create \
    --resource-group bidone-demo-rg \
    --nsg-name bidone-nsg-prod \
    --name AllowHTTPSOutbound \
    --protocol Tcp \
    --priority 200 \
    --destination-port-range 443 \
    --access Allow \
    --direction Outbound
```

#### b) 配置私有端点
```bash
# 为数据库创建私有端点
az network private-endpoint create \
    --resource-group bidone-demo-rg \
    --name bidone-sql-private-endpoint \
    --vnet-name bidone-vnet \
    --subnet bidone-data-subnet \
    --private-connection-resource-id "/subscriptions/{subscription-id}/resourceGroups/bidone-demo-rg/providers/Microsoft.Sql/servers/bidone-sql-prod" \
    --group-ids sqlServer \
    --connection-name bidone-sql-connection
```

## 性能问题

### 1. 高 CPU 使用率

**症状**: 容器 CPU 使用率持续高于 80%

**诊断步骤**:
```bash
# 查看 CPU 使用率趋势
az monitor metrics list \
    --resource "/subscriptions/{subscription-id}/resourceGroups/bidone-demo-rg/providers/Microsoft.App/containerApps/external-order-api" \
    --metric "CpuPercentage" \
    --interval PT5M \
    --start-time 2024-01-01T00:00:00Z \
    --end-time 2024-01-01T23:59:59Z
```

**解决方案**:

#### a) 代码优化
```csharp
// 使用异步并发代替串行处理
public async Task<OrderProcessingResult> ProcessOrderAsync(Order order)
{
    // 并行执行独立操作
    var validationTask = ValidateOrderAsync(order);
    var enrichmentTask = EnrichOrderDataAsync(order);
    var inventoryTask = CheckInventoryAsync(order);

    await Task.WhenAll(validationTask, enrichmentTask, inventoryTask);

    var validationResult = await validationTask;
    var enrichmentResult = await enrichmentTask;
    var inventoryResult = await inventoryTask;

    return new OrderProcessingResult
    {
        IsValid = validationResult.IsValid,
        EnrichedData = enrichmentResult,
        InventoryStatus = inventoryResult
    };
}

// 使用对象池减少GC压力
private readonly ObjectPool<StringBuilder> _stringBuilderPool;

public string FormatOrderDetails(Order order)
{
    var sb = _stringBuilderPool.Get();
    try
    {
        sb.AppendLine($"Order ID: {order.Id}");
        sb.AppendLine($"Customer: {order.CustomerId}");
        // ... 其他格式化逻辑
        return sb.ToString();
    }
    finally
    {
        _stringBuilderPool.Return(sb);
    }
}
```

#### b) 垂直扩展
```bash
# 增加 CPU 和内存资源
az containerapp update \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --cpu 2.0 \
    --memory 4Gi
```

#### c) 水平扩展
```bash
# 配置自动扩缩容
az containerapp update \
    --name external-order-api \
    --resource-group bidone-demo-rg \
    --min-replicas 3 \
    --max-replicas 15 \
    --scale-rule-name cpu-scaling \
    --scale-rule-type cpu \
    --scale-rule-metadata targetCpuUtilization=70
```

### 2. 内存泄漏

**症状**: 内存使用量持续增长，不回收

**诊断工具**:
```csharp
// 添加内存监控中间件
public class MemoryMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MemoryMonitoringMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        var beforeMemory = GC.GetTotalMemory(false);
        var beforeGen0 = GC.CollectionCount(0);
        var beforeGen1 = GC.CollectionCount(1);
        var beforeGen2 = GC.CollectionCount(2);

        await _next(context);

        var afterMemory = GC.GetTotalMemory(false);
        var afterGen0 = GC.CollectionCount(0);
        var afterGen1 = GC.CollectionCount(1);
        var afterGen2 = GC.CollectionCount(2);

        var memoryDiff = afterMemory - beforeMemory;
        
        if (memoryDiff > 1024 * 1024) // 超过1MB
        {
            _logger.LogWarning("High memory allocation: {MemoryDiff}MB, GC: Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}", 
                memoryDiff / 1024 / 1024,
                afterGen0 - beforeGen0,
                afterGen1 - beforeGen1,
                afterGen2 - beforeGen2);
        }
    }
}
```

**解决方案**:

#### a) 修复资源泄漏
```csharp
// 正确使用 HttpClient
public class OrderService
{
    private readonly HttpClient _httpClient;

    public OrderService(HttpClient httpClient)
    {
        _httpClient = httpClient; // 注入的 HttpClient 由 DI 容器管理
    }

    public async Task<ExternalOrderData> GetExternalOrderDataAsync(string orderId)
    {
        // 不要在这里创建新的 HttpClient
        using var response = await _httpClient.GetAsync($"/orders/{orderId}");
        return await response.Content.ReadFromJsonAsync<ExternalOrderData>();
    }
}

// 正确处理数据库连接
public async Task<List<Order>> GetOrdersAsync()
{
    // 使用 using 确保连接被释放
    using var connection = new SqlConnection(_connectionString);
    await connection.OpenAsync();
    
    using var command = new SqlCommand("SELECT * FROM Orders", connection);
    using var reader = await command.ExecuteReaderAsync();
    
    var orders = new List<Order>();
    while (await reader.ReadAsync())
    {
        orders.Add(MapOrderFromReader(reader));
    }
    
    return orders;
}
```

## 部署问题

### 1. Docker 镜像构建失败

**症状**: 无法构建或推送 Docker 镜像

**常见错误和解决方案**:

#### a) 基础镜像拉取失败
```dockerfile
# 使用特定版本的基础镜像
FROM mcr.microsoft.com/dotnet/aspnet:8.0.1-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0.1-alpine AS build
WORKDIR /src

# 复制项目文件
COPY ["src/ExternalOrderApi/ExternalOrderApi.csproj", "src/ExternalOrderApi/"]
COPY ["src/Shared/Shared.csproj", "src/Shared/"]

# 还原依赖项
RUN dotnet restore "src/ExternalOrderApi/ExternalOrderApi.csproj"

# 复制源代码
COPY . .
WORKDIR "/src/src/ExternalOrderApi"

# 构建应用程序
RUN dotnet build "ExternalOrderApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExternalOrderApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ExternalOrderApi.dll"]
```

#### b) 权限问题
```bash
# 确保 Docker 守护进程运行
sudo systemctl start docker

# 添加用户到 docker 组
sudo usermod -aG docker $USER
newgrp docker

# 登录到 ACR
az acr login --name bidonecr
```

### 2. Bicep 部署失败

**症状**: Azure 资源部署失败

**常见错误和解决方案**:

#### a) 资源名称冲突
```bicep
// 使用唯一后缀避免名称冲突
param uniqueSuffix string = substring(uniqueString(resourceGroup().id), 0, 6)

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2021-11-01' = {
  name: 'bidone-sb-${uniqueSuffix}'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
}
```

#### b) 权限不足
```bash
# 检查当前用户权限
az role assignment list --assignee $(az account show --query user.name -o tsv) --resource-group bidone-demo-rg

# 分配必要的权限
az role assignment create \
    --assignee $(az account show --query user.name -o tsv) \
    --role "Contributor" \
    --resource-group bidone-demo-rg
```

## 监控和日志

### 高级日志查询示例

#### Application Insights 查询

```kusto
// 查看API性能趋势
requests
| where timestamp > ago(24h)
| where cloud_RoleName in ("external-order-api", "internal-system-api")
| summarize 
    RequestCount = count(),
    AvgDuration = avg(duration),
    P95Duration = percentile(duration, 95),
    ErrorRate = countif(success == false) * 100.0 / count()
    by bin(timestamp, 1h), cloud_RoleName
| render timechart

// 查看错误分布
exceptions
| where timestamp > ago(24h)
| summarize ErrorCount = count() by problemId, outerMessage
| order by ErrorCount desc
| take 20

// 查看依赖调用失败
dependencies
| where timestamp > ago(1h)
| where success == false
| project timestamp, name, type, target, resultCode, duration
| order by timestamp desc

// 自定义事件分析
customEvents
| where timestamp > ago(24h)
| where name == "OrderProcessed"
| extend OrderId = tostring(customDimensions["OrderId"])
| extend ProcessingTime = todouble(customMeasurements["ProcessingTimeMs"])
| summarize 
    OrderCount = count(),
    AvgProcessingTime = avg(ProcessingTime),
    MaxProcessingTime = max(ProcessingTime)
    by bin(timestamp, 1h)
| render timechart
```

#### 容器日志查询

```bash
# 查询错误日志
az monitor log-analytics query \
    --workspace bidone-logs-prod \
    --analytics-query "
        ContainerAppConsoleLogs_CL
        | where TimeGenerated > ago(1h)
        | where Log_s contains 'ERROR' or Log_s contains 'Exception'
        | project TimeGenerated, ContainerName_s, Log_s
        | order by TimeGenerated desc
        | take 50
    " \
    --output table

# 查询性能指标
az monitor log-analytics query \
    --workspace bidone-logs-prod \
    --analytics-query "
        InsightsMetrics
        | where TimeGenerated > ago(1h)
        | where Namespace == 'container.azm.ms/cpu'
        | summarize AvgCpuUsage = avg(Val) by bin(TimeGenerated, 5m), Computer
        | render timechart
    "
```

### 告警规则配置

```bash
# 创建错误率告警
az monitor metrics alert create \
    --name "High Error Rate" \
    --resource-group bidone-demo-rg \
    --scopes "/subscriptions/{subscription-id}/resourceGroups/bidone-demo-rg/providers/Microsoft.App/containerApps/external-order-api" \
    --condition "avg requests/failed > 5" \
    --window-size 5m \
    --evaluation-frequency 1m \
    --action-group "/subscriptions/{subscription-id}/resourceGroups/bidone-demo-rg/providers/Microsoft.Insights/actionGroups/bidone-alerts"

# 创建 CPU 使用率告警
az monitor metrics alert create \
    --name "High CPU Usage" \
    --resource-group bidone-demo-rg \
    --scopes "/subscriptions/{subscription-id}/resourceGroups/bidone-demo-rg/providers/Microsoft.App/containerApps/external-order-api" \
    --condition "avg CpuPercentage > 80" \
    --window-size 10m \
    --evaluation-frequency 5m \
    --action-group "/subscriptions/{subscription-id}/resourceGroups/bidone-demo-rg/providers/Microsoft.Insights/actionGroups/bidone-alerts"
```

## 紧急响应流程

### 1. 系统全面故障

**响应步骤**:
1. 确认故障范围
2. 启动紧急响应团队
3. 切换到备用系统（如果可用）
4. 开始根因分析
5. 制定恢复计划
6. 执行恢复操作
7. 验证系统功能
8. 事后总结和改进

### 2. 数据丢失事件

**响应步骤**:
1. 立即停止可能导致进一步数据丢失的操作
2. 评估数据丢失范围
3. 从最近的备份开始恢复
4. 验证数据完整性
5. 通知相关利益相关者
6. 更新灾难恢复计划

### 3. 安全事件

**响应步骤**:
1. 隔离受影响的系统
2. 保存证据
3. 评估安全漏洞
4. 修复安全问题
5. 重新部署安全的系统
6. 加强监控
7. 事件报告和合规性检查

---

## 技术支持联系信息

- **紧急支持**: [emergency@example.com](mailto:emergency@example.com)
- **技术支持**: [support@example.com](mailto:support@example.com)
- **文档中心**: [https://docs.example.com](https://docs.example.com)
- **状态页面**: [https://status.example.com](https://status.example.com)

如果问题无法通过本指南解决，请及时联系技术支持团队，并提供详细的错误信息和重现步骤。