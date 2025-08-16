using BidOne.Shared.Models;
using FluentValidation;

namespace BidOne.ExternalOrderApi.Validators;

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required")
            .Length(3, 50)
            .WithMessage("Customer ID must be between 3 and 50 characters")
            .Matches("^[A-Za-z0-9-_]+$")
            .WithMessage("Customer ID can only contain letters, numbers, hyphens, and underscores");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("At least one order item is required")
            .Must(items => items.Count <= 100)
            .WithMessage("Maximum of 100 items allowed per order");

        RuleForEach(x => x.Items).SetValidator(new CreateOrderItemRequestValidator());

        RuleFor(x => x.DeliveryDate)
            .Must(BeAFutureDate)
            .When(x => x.DeliveryDate.HasValue)
            .WithMessage("Delivery date must be in the future");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("Notes cannot exceed 1000 characters");

        RuleFor(x => x)
            .Must(HaveValidTotalValue)
            .WithMessage("Total order value cannot exceed $100,000");
    }

    private static bool BeAFutureDate(DateTime? date)
    {
        if (!date.HasValue) return true;
        return date.Value.Date >= DateTime.UtcNow.Date;
    }

    private static bool HaveValidTotalValue(CreateOrderRequest request)
    {
        var totalValue = request.Items.Sum(item => item.Quantity * item.UnitPrice);
        return totalValue <= 100_000m;
    }
}

public class CreateOrderItemRequestValidator : AbstractValidator<CreateOrderItemRequest>
{
    public CreateOrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("Product ID is required")
            .Length(3, 50)
            .WithMessage("Product ID must be between 3 and 50 characters")
            .Matches("^[A-Za-z0-9-_]+$")
            .WithMessage("Product ID can only contain letters, numbers, hyphens, and underscores");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0")
            .LessThanOrEqualTo(10_000)
            .WithMessage("Quantity cannot exceed 10,000 units");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0)
            .WithMessage("Unit price must be greater than 0")
            .LessThanOrEqualTo(10_000)
            .WithMessage("Unit price cannot exceed $10,000")
            .ScalePrecision(2, 10, ignoreTrailingZeros: true)
            .WithMessage("Unit price cannot have more than 2 decimal places");
    }
}
