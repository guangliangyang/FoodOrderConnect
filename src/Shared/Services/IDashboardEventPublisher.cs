namespace BidOne.Shared.Services;

/// <summary>
/// 仪表板事件发布接口 - 专注于实时业务指标
/// </summary>
public interface IDashboardEventPublisher
{
    /// <summary>
    /// 发布订单指标更新事件
    /// </summary>
    /// <param name="totalOrders">总订单数</param>
    /// <param name="todayOrders">今日订单数</param>
    /// <param name="pendingOrders">待处理订单数</param>
    /// <param name="status">当前状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PublishOrderMetricsAsync(int totalOrders, int todayOrders, int pendingOrders, string status, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发布性能告警事件
    /// </summary>
    /// <param name="alertType">告警类型</param>
    /// <param name="message">告警消息</param>
    /// <param name="severity">严重程度</param>
    /// <param name="serviceName">服务名称</param>
    /// <param name="value">当前值</param>
    /// <param name="threshold">阈值</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PublishPerformanceAlertAsync(string alertType, string message, string severity, string serviceName, double value, double threshold, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发布系统健康状态事件
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="status">健康状态</param>
    /// <param name="responseTimeMs">响应时间（毫秒）</param>
    /// <param name="details">详细信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PublishSystemHealthAsync(string serviceName, string status, int responseTimeMs, string details = "", CancellationToken cancellationToken = default);
}
