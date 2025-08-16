using System.Text.Json;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BidOne.Shared.Services;

/// <summary>
/// 仪表板事件发布服务实现
/// </summary>
public class DashboardEventPublisher : IDashboardEventPublisher
{
    private readonly EventGridPublisherClient? _eventGridClient;
    private readonly ILogger<DashboardEventPublisher> _logger;
    private readonly bool _isEnabled;

    public DashboardEventPublisher(IConfiguration configuration, ILogger<DashboardEventPublisher> logger)
    {
        _logger = logger;

        var endpoint = configuration.GetValue<string>("EventGridTopicEndpoint");
        var accessKey = configuration.GetValue<string>("EventGridTopicKey");

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(accessKey))
        {
            _eventGridClient = new EventGridPublisherClient(new Uri(endpoint), new Azure.AzureKeyCredential(accessKey));
            _isEnabled = true;
            _logger.LogInformation("✅ Dashboard Event Publisher initialized with Event Grid");
        }
        else
        {
            _isEnabled = false;
            _logger.LogWarning("⚠️ Dashboard Event Publisher disabled - Event Grid configuration missing");
        }
    }

    public async Task PublishOrderMetricsAsync(int totalOrders, int todayOrders, int pendingOrders, string status, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("📊 [SIMULATION] Order metrics - Total: {Total}, Today: {Today}, Pending: {Pending}, Status: {Status}",
                totalOrders, todayOrders, pendingOrders, status);
            return;
        }

        try
        {
            var eventData = new
            {
                TotalOrdersCount = totalOrders,
                TodayOrdersCount = todayOrders,
                PendingOrdersCount = pendingOrders,
                CompletedOrdersCount = totalOrders - pendingOrders,
                FailedOrdersCount = 0, // 可以从其他地方获取
                AverageOrderValue = 0m, // 可以计算或从缓存获取
                Status = status,
                Timestamp = DateTime.UtcNow
            };

            var eventGridEvent = new EventGridEvent(
                subject: "dashboard/metrics/orders",
                eventType: "BidOne.Dashboard.OrderMetrics",
                dataVersion: "1.0",
                data: eventData
            );

            await _eventGridClient!.SendEventAsync(eventGridEvent, cancellationToken);

            _logger.LogInformation("📈 Dashboard order metrics published - Total: {Total}, Today: {Today}, Pending: {Pending}",
                totalOrders, todayOrders, pendingOrders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to publish order metrics event");
            // 不重新抛出异常，避免影响主业务流程
        }
    }

    public async Task PublishPerformanceAlertAsync(string alertType, string message, string severity, string serviceName, double value, double threshold, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("⚠️ [SIMULATION] Performance alert - {AlertType}: {Message} ({Severity})",
                alertType, message, severity);
            return;
        }

        try
        {
            var eventData = new
            {
                AlertType = alertType,
                Message = message,
                Severity = severity,
                ServiceName = serviceName,
                Value = value,
                Threshold = threshold,
                Timestamp = DateTime.UtcNow
            };

            var eventGridEvent = new EventGridEvent(
                subject: $"dashboard/alerts/{serviceName}",
                eventType: "BidOne.Dashboard.PerformanceAlert",
                dataVersion: "1.0",
                data: eventData
            );

            await _eventGridClient!.SendEventAsync(eventGridEvent, cancellationToken);

            _logger.LogWarning("⚠️ Performance alert published - {AlertType}: {Message} ({Severity})",
                alertType, message, severity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to publish performance alert event");
        }
    }

    public async Task PublishSystemHealthAsync(string serviceName, string status, int responseTimeMs, string details = "", CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("🏥 [SIMULATION] System health - {ServiceName}: {Status} ({ResponseTime}ms)",
                serviceName, status, responseTimeMs);
            return;
        }

        try
        {
            var eventData = new
            {
                ServiceName = serviceName,
                Status = status,
                ResponseTimeMs = responseTimeMs,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            var eventGridEvent = new EventGridEvent(
                subject: $"dashboard/health/{serviceName}",
                eventType: "BidOne.Dashboard.SystemHealth",
                dataVersion: "1.0",
                data: eventData
            );

            await _eventGridClient!.SendEventAsync(eventGridEvent, cancellationToken);

            _logger.LogInformation("🏥 System health published - {ServiceName}: {Status} ({ResponseTime}ms)",
                serviceName, status, responseTimeMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to publish system health event");
        }
    }
}
