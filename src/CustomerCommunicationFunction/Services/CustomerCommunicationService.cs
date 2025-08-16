using System.Text.Json;
using BidOne.Shared.Events;
using Microsoft.Extensions.Logging;

namespace BidOne.CustomerCommunicationFunction.Services;

public class CustomerCommunicationService : ICustomerCommunicationService
{
    private readonly ILangChainService _langChainService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CustomerCommunicationService> _logger;

    public CustomerCommunicationService(
        ILangChainService langChainService,
        INotificationService notificationService,
        ILogger<CustomerCommunicationService> logger)
    {
        _langChainService = langChainService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task ProcessHighValueErrorAsync(HighValueErrorEvent errorEvent, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogWarning("🚨 Processing high-value error for order {OrderId}, customer {CustomerId}, value ${OrderValue:N2}",
                errorEvent.OrderId, errorEvent.CustomerId, errorEvent.OrderValue);

            // 第一步：使用 LangChain 分析错误
            _logger.LogInformation("🔍 Step 1: Analyzing error with LangChain AI...");
            var analysis = await _langChainService.AnalyzeErrorAsync(errorEvent, cancellationToken);

            // 第二步：生成客户消息
            _logger.LogInformation("📝 Step 2: Generating customer communication...");
            var customerMessage = await _langChainService.GenerateCustomerMessageAsync(errorEvent, analysis, cancellationToken);

            // 第三步：生成内部行动建议
            _logger.LogInformation("📋 Step 3: Generating suggested actions...");
            var suggestedActions = await _langChainService.GenerateSuggestedActionsAsync(errorEvent, analysis, cancellationToken);

            // 第四步：发送客户通知
            _logger.LogInformation("📧 Step 4: Sending customer notification...");
            await _notificationService.SendCustomerNotificationAsync(
                errorEvent.CustomerId,
                errorEvent.CustomerEmail,
                customerMessage,
                cancellationToken);

            // 第五步：发送内部警报
            _logger.LogInformation("🚨 Step 5: Sending internal alert...");
            await _notificationService.SendInternalAlertAsync(
                $"High-Value Order Error: {errorEvent.OrderId}",
                $"AI Analysis: {analysis}\n\nSuggested Actions:\n{string.Join("\n", suggestedActions)}",
                new Dictionary<string, object>
                {
                    ["OrderId"] = errorEvent.OrderId,
                    ["CustomerId"] = errorEvent.CustomerId,
                    ["OrderValue"] = errorEvent.OrderValue,
                    ["CustomerTier"] = errorEvent.CustomerTier,
                    ["ErrorCategory"] = errorEvent.ErrorCategory,
                    ["ProcessingTime"] = stopwatch.Elapsed.TotalSeconds
                },
                cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation("✅ High-value error processing completed for order {OrderId} in {ProcessingTime:F2}s",
                errorEvent.OrderId, stopwatch.Elapsed.TotalSeconds);

            // 记录成功的处理指标
            LogProcessingMetrics(errorEvent, true, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "❌ Failed to process high-value error for order {OrderId}", errorEvent.OrderId);

            // 记录失败的处理指标
            LogProcessingMetrics(errorEvent, false, stopwatch.Elapsed);

            // 发送紧急内部警报
            try
            {
                await _notificationService.SendInternalAlertAsync(
                    $"URGENT: AI Communication System Failure for Order {errorEvent.OrderId}",
                    $"Failed to process high-value error automatically. Manual intervention required.\n\nError: {ex.Message}\n\nOrder Details:\n{JsonSerializer.Serialize(errorEvent, new JsonSerializerOptions { WriteIndented = true })}",
                    new Dictionary<string, object>
                    {
                        ["OrderId"] = errorEvent.OrderId,
                        ["ErrorType"] = "SystemFailure",
                        ["RequiresManualIntervention"] = true
                    },
                    cancellationToken);
            }
            catch (Exception alertEx)
            {
                _logger.LogCritical(alertEx, "🔥 CRITICAL: Failed to send emergency alert for order {OrderId}", errorEvent.OrderId);
            }

            throw;
        }
    }

    private void LogProcessingMetrics(HighValueErrorEvent errorEvent, bool isSuccessful, TimeSpan processingTime)
    {
        var status = isSuccessful ? "Success" : "Failed";

        _logger.LogInformation("📊 Processing Metrics: Order={OrderId}, Status={Status}, Time={ProcessingTime:F2}s, Value=${OrderValue:N2}, Tier={CustomerTier}",
            errorEvent.OrderId, status, processingTime.TotalSeconds, errorEvent.OrderValue, errorEvent.CustomerTier);
    }
}
