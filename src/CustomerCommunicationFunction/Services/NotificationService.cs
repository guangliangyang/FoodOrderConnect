using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BidOne.CustomerCommunicationFunction.Services;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public async Task SendCustomerNotificationAsync(string customerId, string email, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            // 在实际环境中，这里会集成真实的邮件服务（如SendGrid、Azure Communication Services等）
            _logger.LogInformation("📧 Sending customer notification to {CustomerId} ({Email})", customerId, email);

            // 模拟邮件发送延迟
            await Task.Delay(100, cancellationToken);

            // 记录邮件内容（仅用于演示）
            _logger.LogInformation("📧 Email content preview: {MessagePreview}...",
                message.Length > 100 ? message.Substring(0, 100) + "..." : message);

            _logger.LogInformation("✅ Customer notification sent successfully to {CustomerId}", customerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send customer notification to {CustomerId}", customerId);
            throw;
        }
    }

    public async Task SendInternalAlertAsync(string subject, string message, Dictionary<string, object> metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            // 在实际环境中，这里会发送到Teams、Slack或其他内部通知系统
            _logger.LogWarning("🚨 INTERNAL ALERT: {Subject}", subject);
            _logger.LogInformation("Alert details: {Message}", message);
            _logger.LogDebug("Alert metadata: {Metadata}", JsonSerializer.Serialize(metadata));

            // 模拟通知发送
            await Task.Delay(50, cancellationToken);

            _logger.LogInformation("✅ Internal alert sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send internal alert");
            throw;
        }
    }
}
