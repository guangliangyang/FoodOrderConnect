using System.Text.Json;
using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BidOne.OrderIntegrationFunction.Functions;

/// <summary>
/// 处理仪表板指标事件的 Azure Function
/// 专注于实时业务仪表板数据更新
/// </summary>
public class DashboardMetricsProcessor
{
    private readonly ILogger<DashboardMetricsProcessor> _logger;

    public DashboardMetricsProcessor(ILogger<DashboardMetricsProcessor> logger)
    {
        _logger = logger;
    }

    [Function("DashboardMetricsProcessor")]
    public async Task ProcessDashboardEvents(
        [EventGridTrigger] EventGridEvent eventGridEvent)
    {
        _logger.LogInformation("📊 Processing Dashboard Event: {EventType} for {Subject}",
            eventGridEvent.EventType, eventGridEvent.Subject);

        try
        {
            switch (eventGridEvent.EventType)
            {
                case "BidOne.Dashboard.OrderMetrics":
                    await HandleOrderMetricsEvent(eventGridEvent);
                    break;

                case "BidOne.Dashboard.PerformanceAlert":
                    await HandlePerformanceAlertEvent(eventGridEvent);
                    break;

                case "BidOne.Dashboard.SystemHealth":
                    await HandleSystemHealthEvent(eventGridEvent);
                    break;

                default:
                    _logger.LogWarning("⚠️ Unknown dashboard event type: {EventType}", eventGridEvent.EventType);
                    break;
            }

            _logger.LogInformation("✅ Dashboard event processed successfully: {EventType}", eventGridEvent.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to process dashboard event: {EventType}", eventGridEvent.EventType);
            throw; // Re-throw to trigger retry
        }
    }

    /// <summary>
    /// 处理订单指标事件 - 更新实时订单统计
    /// </summary>
    private async Task HandleOrderMetricsEvent(EventGridEvent eventGridEvent)
    {
        var metricsData = eventGridEvent.Data.ToObjectFromJson<OrderMetricsEventData>();
        if (metricsData == null)
        {
            _logger.LogWarning("Invalid metrics data received");
            return;
        }

        _logger.LogInformation("📈 Updating order metrics - Total: {TotalOrders}, Today: {TodayOrders}, Status: {Status}",
            metricsData.TotalOrdersCount, metricsData.TodayOrdersCount, metricsData.Status);

        // 更新 Redis 缓存中的实时指标
        await UpdateDashboardMetricsCache(metricsData);

        // 推送到 SignalR 实时通知前端仪表板
        await NotifyDashboardClients("orderMetrics", metricsData);

        // 存储到 Cosmos DB 用于历史分析
        await StoreDashboardMetrics(metricsData);
    }

    /// <summary>
    /// 处理性能告警事件
    /// </summary>
    private async Task HandlePerformanceAlertEvent(EventGridEvent eventGridEvent)
    {
        var alertData = eventGridEvent.Data.ToObjectFromJson<PerformanceAlertEventData>();
        if (alertData == null)
        {
            _logger.LogWarning("Invalid alert data received");
            return;
        }

        _logger.LogWarning("⚠️ Performance Alert - {AlertType}: {Message}, Severity: {Severity}",
            alertData.AlertType, alertData.Message, alertData.Severity);

        // 推送告警到仪表板
        await NotifyDashboardClients("performanceAlert", alertData);

        // 如果是严重告警，发送邮件通知
        if (alertData.Severity == "Critical" || alertData.Severity == "High")
        {
            await SendAlertNotification(alertData);
        }
    }

    /// <summary>
    /// 处理系统健康状态事件
    /// </summary>
    private async Task HandleSystemHealthEvent(EventGridEvent eventGridEvent)
    {
        var healthData = eventGridEvent.Data.ToObjectFromJson<SystemHealthEventData>();
        if (healthData == null)
        {
            _logger.LogWarning("Invalid health data received");
            return;
        }

        _logger.LogInformation("🏥 System Health Update - Service: {ServiceName}, Status: {Status}, Response Time: {ResponseTime}ms",
            healthData.ServiceName, healthData.Status, healthData.ResponseTimeMs);

        // 更新系统健康状态缓存
        await UpdateHealthStatusCache(healthData);

        // 推送到仪表板
        await NotifyDashboardClients("systemHealth", healthData);

        // 如果服务不健康，记录告警
        if (healthData.Status != "Healthy")
        {
            await LogHealthAlert(healthData);
        }
    }

    /// <summary>
    /// 更新仪表板指标缓存
    /// </summary>
    private async Task UpdateDashboardMetricsCache(OrderMetricsEventData metricsData)
    {
        try
        {
            var cacheKey = "dashboard:metrics:orders";
            var cacheData = new
            {
                TotalOrders = metricsData.TotalOrdersCount,
                TodayOrders = metricsData.TodayOrdersCount,
                PendingOrders = metricsData.PendingOrdersCount,
                CompletedOrders = metricsData.CompletedOrdersCount,
                FailedOrders = metricsData.FailedOrdersCount,
                AverageOrderValue = metricsData.AverageOrderValue,
                LastUpdated = DateTime.UtcNow
            };

            _logger.LogInformation("🔄 Updating dashboard metrics cache: {CacheKey}", cacheKey);

            // 实际实现中会更新 Redis 缓存
            await Task.Delay(10); // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update dashboard metrics cache");
        }
    }

    /// <summary>
    /// 通知仪表板客户端
    /// </summary>
    private async Task NotifyDashboardClients(string eventType, object data)
    {
        try
        {
            _logger.LogInformation("📡 Notifying dashboard clients - Event: {EventType}", eventType);

            // 实际实现中会通过 SignalR 推送到前端
            await Task.Delay(10); // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify dashboard clients for event: {EventType}", eventType);
        }
    }

    /// <summary>
    /// 存储仪表板指标到 Cosmos DB
    /// </summary>
    private async Task StoreDashboardMetrics(OrderMetricsEventData metricsData)
    {
        try
        {
            var document = new DashboardMetricsDocument
            {
                Id = Guid.NewGuid().ToString(),
                Type = "OrderMetrics",
                Timestamp = DateTime.UtcNow,
                Data = metricsData,
                PartitionKey = DateTime.UtcNow.ToString("yyyy-MM-dd")
            };

            _logger.LogInformation("💾 Storing dashboard metrics to Cosmos DB");

            // 实际实现中会写入 Cosmos DB
            await Task.Delay(10); // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store dashboard metrics");
        }
    }

    /// <summary>
    /// 更新健康状态缓存
    /// </summary>
    private async Task UpdateHealthStatusCache(SystemHealthEventData healthData)
    {
        try
        {
            var cacheKey = $"dashboard:health:{healthData.ServiceName}";
            var cacheData = new
            {
                ServiceName = healthData.ServiceName,
                Status = healthData.Status,
                ResponseTime = healthData.ResponseTimeMs,
                LastCheck = DateTime.UtcNow,
                Details = healthData.Details
            };

            _logger.LogInformation("🔄 Updating health status cache: {CacheKey}", cacheKey);

            // 实际实现中会更新 Redis 缓存
            await Task.Delay(10); // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update health status cache");
        }
    }

    /// <summary>
    /// 发送告警通知
    /// </summary>
    private async Task SendAlertNotification(PerformanceAlertEventData alertData)
    {
        try
        {
            _logger.LogWarning("📧 Sending alert notification: {AlertType}", alertData.AlertType);

            // 实际实现中会发送邮件或短信
            await Task.Delay(10); // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert notification");
        }
    }

    /// <summary>
    /// 记录健康告警
    /// </summary>
    private async Task LogHealthAlert(SystemHealthEventData healthData)
    {
        try
        {
            _logger.LogWarning("🚨 Health Alert - Service {ServiceName} is {Status}",
                healthData.ServiceName, healthData.Status);

            // 实际实现中会记录到告警系统
            await Task.Delay(10); // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log health alert");
        }
    }
}

// 事件数据模型
public class OrderMetricsEventData
{
    public int TotalOrdersCount { get; set; }
    public int TodayOrdersCount { get; set; }
    public int PendingOrdersCount { get; set; }
    public int CompletedOrdersCount { get; set; }
    public int FailedOrdersCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class PerformanceAlertEventData
{
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // Critical, High, Medium, Low
    public string ServiceName { get; set; } = string.Empty;
    public double Value { get; set; }
    public double Threshold { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SystemHealthEventData
{
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Healthy, Degraded, Unhealthy
    public int ResponseTimeMs { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class DashboardMetricsDocument
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object Data { get; set; } = new();
    public string PartitionKey { get; set; } = string.Empty;
}
