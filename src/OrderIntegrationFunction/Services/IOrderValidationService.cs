using BidOne.Shared.Models;

namespace BidOne.OrderIntegrationFunction.Services;

public interface IOrderValidationService
{
    Task<ValidationResult> ValidateOrderAsync(Order order, CancellationToken cancellationToken = default);
}
