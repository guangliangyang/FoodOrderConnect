using System.Text.Json;
using BidOne.CustomerCommunicationFunction.Services;
using BidOne.Shared.Events;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BidOne.CustomerCommunicationFunction.Functions;

public class CustomerCommunicationProcessor
{
    private readonly ICustomerCommunicationService _communicationService;
    private readonly ILogger<CustomerCommunicationProcessor> _logger;

    public CustomerCommunicationProcessor(
        ICustomerCommunicationService communicationService,
        ILogger<CustomerCommunicationProcessor> logger)
    {
        _communicationService = communicationService;
        _logger = logger;
    }

    /// <summary>
    /// 处理来自 Service Bus 高价值错误队列的消息
    /// 这是主要的处理逻辑，直接从 Service Bus 拉取消息进行处理
    /// </summary>
    [Function("ProcessHighValueErrorFromServiceBus")]
    public async Task ProcessHighValueErrorFromServiceBus(
        [ServiceBusTrigger("high-value-errors", Connection = "ServiceBusConnection")] string errorMessage)
    {
        _logger.LogInformation("🚨 High-value error message received from Service Bus queue");

        try
        {
            var errorEvent = JsonSerializer.Deserialize<HighValueErrorEvent>(errorMessage, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (errorEvent == null)
            {
                _logger.LogError("❌ Failed to deserialize high-value error event");
                throw new InvalidOperationException("Invalid error event data");
            }

            _logger.LogInformation("📨 Processing high-value error: OrderId={OrderId}, CustomerId={CustomerId}, Value=${OrderValue:N2}",
                errorEvent.OrderId, errorEvent.CustomerId, errorEvent.OrderValue);

            await _communicationService.ProcessHighValueErrorAsync(errorEvent);

            _logger.LogInformation("✅ High-value error processed successfully from Service Bus");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing high-value error from Service Bus");
            throw; // This will move the message to dead letter queue
        }
    }

    /// <summary>
    /// 处理来自 Event Grid 的事件（当 Service Bus 队列有新消息时）
    /// 这是为了演示 Event Grid 集成，实际处理仍然从 Service Bus 获取详细消息
    /// </summary>
    [Function("CustomerCommunicationProcessor")]
    public async Task ProcessEventGridNotification(
        [EventGridTrigger] EventGridEvent eventGridEvent)
    {
        _logger.LogInformation("🔔 Event Grid notification received: {EventType}", eventGridEvent.EventType);

        try
        {
            // Event Grid 事件表示 Service Bus 队列中有新的高价值错误消息
            // 在实际场景中，我们可以使用这个事件来触发额外的处理逻辑
            // 例如：发送即时通知、更新监控仪表板等

            if (eventGridEvent.EventType == "Microsoft.ServiceBus.ActiveMessagesAvailableWithNoListeners")
            {
                var eventData = JsonSerializer.Deserialize<ServiceBusEventData>(eventGridEvent.Data.GetRawText());

                _logger.LogInformation("📊 Service Bus event: Queue={QueueName}, MessageCount={MessageCount}",
                    eventData?.EntityName, eventData?.MessageCount);

                // 这里可以添加额外的实时通知逻辑
                // 例如：发送Teams通知、更新实时仪表板、触发警报等

                _logger.LogInformation("🔔 Real-time notification triggered for high-value error queue activity");
            }

            _logger.LogInformation("✅ Event Grid notification processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing Event Grid notification");
            // Event Grid 错误不应该阻止主要的处理流程
        }
    }
}

/// <summary>
/// Service Bus Event Grid 事件数据结构
/// </summary>
public class ServiceBusEventData
{
    public string? EntityName { get; set; }
    public int MessageCount { get; set; }
    public string? NamespaceName { get; set; }
    public string? RequestUri { get; set; }
}

/// <summary>
/// Event Grid 事件包装器
/// </summary>
public class EventGridEvent
{
    public string EventType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public JsonElement Data { get; set; }
    public DateTime EventTime { get; set; }
    public string Id { get; set; } = string.Empty;
}
