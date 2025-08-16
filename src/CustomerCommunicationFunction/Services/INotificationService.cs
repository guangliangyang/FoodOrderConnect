namespace BidOne.CustomerCommunicationFunction.Services;

public interface INotificationService
{
    Task SendCustomerNotificationAsync(string customerId, string email, string message, CancellationToken cancellationToken = default);
    Task SendInternalAlertAsync(string subject, string message, Dictionary<string, object> metadata, CancellationToken cancellationToken = default);
}
