using BidOne.Shared.Events;

namespace BidOne.CustomerCommunicationFunction.Services;

public interface ICustomerCommunicationService
{
    Task ProcessHighValueErrorAsync(HighValueErrorEvent errorEvent, CancellationToken cancellationToken = default);
}

public class CommunicationResult
{
    public bool IsSuccessful { get; set; }
    public string CustomerMessage { get; set; } = string.Empty;
    public string InternalAnalysis { get; set; } = string.Empty;
    public List<string> SuggestedActions { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime ProcessedAt { get; set; }
}
