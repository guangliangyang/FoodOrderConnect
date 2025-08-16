using BidOne.Shared.Models;

namespace BidOne.OrderIntegrationFunction.Services;

public interface INotificationService
{
    Task<bool> SendOrderConfirmationAsync(Order order, string customerEmail, CancellationToken cancellationToken = default);
    Task<bool> SendOrderUpdateAsync(Order order, string customerEmail, string updateMessage, CancellationToken cancellationToken = default);
    Task<bool> NotifySupplierAsync(Order order, string supplierEmail, CancellationToken cancellationToken = default);
    Task<bool> SendDeliveryNotificationAsync(Order order, string customerEmail, string trackingNumber, CancellationToken cancellationToken = default);
    Task<bool> SendErrorNotificationAsync(string errorMessage, string recipientEmail, Dictionary<string, object>? context = null, CancellationToken cancellationToken = default);
}

public class NotificationTemplate
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
}

public class NotificationResult
{
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public string NotificationId { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string Provider { get; set; } = string.Empty;
}
