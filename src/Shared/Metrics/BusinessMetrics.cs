using Prometheus;

namespace BidOne.Shared.Metrics;

/// <summary>
/// 业务指标收集 - 展示 Prometheus 集成能力
/// </summary>
public static class BusinessMetrics
{
    /// <summary>
    /// 订单处理总数计数器
    /// </summary>
    public static readonly Counter OrdersProcessed = Prometheus.Metrics
        .CreateCounter("bidone_orders_processed_total", "订单处理总数",
            new[] { "status", "service" });

    /// <summary>
    /// 订单处理时间直方图
    /// </summary>
    public static readonly Histogram OrderProcessingTime = Prometheus.Metrics
        .CreateHistogram("bidone_order_processing_seconds", "订单处理时间(秒)",
            new HistogramConfiguration
            {
                Buckets = Histogram.LinearBuckets(0.01, 0.05, 20), // 0.01s 到 1s
                LabelNames = new[] { "service", "operation" }
            });

    /// <summary>
    /// 当前待处理订单数量计量器
    /// </summary>
    public static readonly Gauge PendingOrders = Prometheus.Metrics
        .CreateGauge("bidone_pending_orders_count", "当前待处理订单数量",
            new[] { "service" });

    /// <summary>
    /// API 请求响应时间直方图
    /// </summary>
    public static readonly Histogram ApiRequestDuration = Prometheus.Metrics
        .CreateHistogram("bidone_api_request_duration_seconds", "API请求响应时间(秒)",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 15), // 1ms 到 16s
                LabelNames = new[] { "method", "endpoint", "status" }
            });

    /// <summary>
    /// 系统健康状态计量器
    /// </summary>
    public static readonly Gauge SystemHealth = Prometheus.Metrics
        .CreateGauge("bidone_system_health_status", "系统健康状态 (1=健康, 0=不健康)",
            new[] { "service", "component" });
}
