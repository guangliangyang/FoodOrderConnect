using BidOne.Shared.Events;

namespace BidOne.CustomerCommunicationFunction.Services;

public interface ILangChainService
{
    Task<string> AnalyzeErrorAsync(HighValueErrorEvent errorEvent, CancellationToken cancellationToken = default);
    Task<string> GenerateCustomerMessageAsync(HighValueErrorEvent errorEvent, string analysis, CancellationToken cancellationToken = default);
    Task<List<string>> GenerateSuggestedActionsAsync(HighValueErrorEvent errorEvent, string analysis, CancellationToken cancellationToken = default);
}
