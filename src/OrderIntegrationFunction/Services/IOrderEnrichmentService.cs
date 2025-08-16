using BidOne.Shared.Models;

namespace BidOne.OrderIntegrationFunction.Services;

public interface IOrderEnrichmentService
{
    Task<EnrichmentResult> EnrichOrderAsync(Order order, CancellationToken cancellationToken = default);
}
