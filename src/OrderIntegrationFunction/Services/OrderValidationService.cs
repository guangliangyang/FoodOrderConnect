using BidOne.Shared.Models;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace BidOne.OrderIntegrationFunction.Services;

public class OrderValidationService : IOrderValidationService
{
    private readonly OrderValidationDbContext _dbContext;
    private readonly ILogger<OrderValidationService> _logger;

    public OrderValidationService(OrderValidationDbContext dbContext, ILogger<OrderValidationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult
        {
            IsValid = true,
            Errors = new List<ValidationError>(),
            ValidatedAt = DateTime.UtcNow,
            ValidatedBy = "OrderValidationService"
        };

        try
        {
            _logger.LogInformation("Starting validation for order {OrderId}", order.Id);

            // Basic validation
            await ValidateBasicOrderData(order, result);

            // Customer validation
            await ValidateCustomer(order, result, cancellationToken);

            // Item validation
            await ValidateOrderItems(order, result, cancellationToken);

            // Business rules validation
            await ValidateBusinessRules(order, result, cancellationToken);

            // Calculate validation data
            result.ValidationData = new Dictionary<string, object>
            {
                ["TotalItemCount"] = order.Items.Count,
                ["TotalValue"] = order.Items.Sum(i => i.TotalPrice),
                ["Categories"] = order.Items.Select(i => i.Category).Distinct().ToList(),
                ["ValidationDuration"] = DateTime.UtcNow.Subtract(result.ValidatedAt).TotalMilliseconds
            };

            result.IsValid = result.Errors.Count == 0;

            _logger.LogInformation("Validation completed for order {OrderId}. IsValid: {IsValid}, ErrorCount: {ErrorCount}",
                order.Id, result.IsValid, result.Errors.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during order validation for order {OrderId}", order.Id);
            
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Field = "System",
                Code = "VALIDATION_EXCEPTION",
                Message = "An unexpected error occurred during validation",
                Context = new Dictionary<string, object> { ["Exception"] = ex.Message }
            });

            return result;
        }
    }

    private static Task ValidateBasicOrderData(Order order, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(order.Id))
        {
            result.Errors.Add(new ValidationError
            {
                Field = nameof(order.Id),
                Code = "REQUIRED",
                Message = "Order ID is required"
            });
        }

        if (string.IsNullOrWhiteSpace(order.CustomerId))
        {
            result.Errors.Add(new ValidationError
            {
                Field = nameof(order.CustomerId),
                Code = "REQUIRED",
                Message = "Customer ID is required"
            });
        }

        if (order.Items == null || order.Items.Count == 0)
        {
            result.Errors.Add(new ValidationError
            {
                Field = nameof(order.Items),
                Code = "REQUIRED",
                Message = "Order must contain at least one item"
            });
        }

        if (order.DeliveryDate.HasValue && order.DeliveryDate.Value.Date < DateTime.UtcNow.Date)
        {
            result.Errors.Add(new ValidationError
            {
                Field = nameof(order.DeliveryDate),
                Code = "INVALID_DATE",
                Message = "Delivery date cannot be in the past",
                AttemptedValue = order.DeliveryDate
            });
        }

        return Task.CompletedTask;
    }

    private async Task ValidateCustomer(Order order, ValidationResult result, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerId))
            return;

        try
        {
            // Check if customer exists and is active
            var customer = await _dbContext.Customers.FindAsync(new object[] { order.CustomerId }, cancellationToken);
            
            if (customer == null)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = nameof(order.CustomerId),
                    Code = "CUSTOMER_NOT_FOUND",
                    Message = $"Customer {order.CustomerId} not found",
                    AttemptedValue = order.CustomerId
                });
            }
            else if (!customer.IsActive)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = nameof(order.CustomerId),
                    Code = "CUSTOMER_INACTIVE",
                    Message = $"Customer {order.CustomerId} is not active",
                    AttemptedValue = order.CustomerId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating customer {CustomerId}", order.CustomerId);
            result.Errors.Add(new ValidationError
            {
                Field = nameof(order.CustomerId),
                Code = "CUSTOMER_VALIDATION_ERROR",
                Message = "Unable to validate customer",
                Context = new Dictionary<string, object> { ["Error"] = ex.Message }
            });
        }
    }

    private async Task ValidateOrderItems(Order order, ValidationResult result, CancellationToken cancellationToken)
    {
        if (order.Items == null || order.Items.Count == 0)
            return;

        for (int i = 0; i < order.Items.Count; i++)
        {
            var item = order.Items[i];
            var itemPrefix = $"Items[{i}]";

            // Basic item validation
            if (string.IsNullOrWhiteSpace(item.ProductId))
            {
                result.Errors.Add(new ValidationError
                {
                    Field = $"{itemPrefix}.ProductId",
                    Code = "REQUIRED",
                    Message = "Product ID is required for all items"
                });
                continue;
            }

            if (item.Quantity <= 0)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = $"{itemPrefix}.Quantity",
                    Code = "INVALID_QUANTITY",
                    Message = "Quantity must be greater than zero",
                    AttemptedValue = item.Quantity
                });
            }

            if (item.UnitPrice <= 0)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = $"{itemPrefix}.UnitPrice",
                    Code = "INVALID_PRICE",
                    Message = "Unit price must be greater than zero",
                    AttemptedValue = item.UnitPrice
                });
            }

            // Validate product exists
            try
            {
                var product = await _dbContext.Products.FindAsync(new object[] { item.ProductId }, cancellationToken);
                
                if (product == null)
                {
                    result.Errors.Add(new ValidationError
                    {
                        Field = $"{itemPrefix}.ProductId",
                        Code = "PRODUCT_NOT_FOUND",
                        Message = $"Product {item.ProductId} not found",
                        AttemptedValue = item.ProductId
                    });
                }
                else if (!product.IsActive)
                {
                    result.Errors.Add(new ValidationError
                    {
                        Field = $"{itemPrefix}.ProductId",
                        Code = "PRODUCT_INACTIVE",
                        Message = $"Product {item.ProductId} is not active",
                        AttemptedValue = item.ProductId
                    });
                }
                else
                {
                    // Validate price matches (with tolerance for small differences)
                    var priceDifference = Math.Abs(item.UnitPrice - product.UnitPrice);
                    var tolerance = product.UnitPrice * 0.05m; // 5% tolerance
                    
                    if (priceDifference > tolerance)
                    {
                        result.Errors.Add(new ValidationError
                        {
                            Field = $"{itemPrefix}.UnitPrice",
                            Code = "PRICE_MISMATCH",
                            Message = $"Unit price for product {item.ProductId} does not match catalog price",
                            AttemptedValue = item.UnitPrice,
                            Context = new Dictionary<string, object>
                            {
                                ["CatalogPrice"] = product.UnitPrice,
                                ["Tolerance"] = tolerance
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating product {ProductId}", item.ProductId);
                result.Errors.Add(new ValidationError
                {
                    Field = $"{itemPrefix}.ProductId",
                    Code = "PRODUCT_VALIDATION_ERROR",
                    Message = "Unable to validate product",
                    Context = new Dictionary<string, object> { ["Error"] = ex.Message }
                });
            }
        }
    }

    private static Task ValidateBusinessRules(Order order, ValidationResult result, CancellationToken cancellationToken)
    {
        // Business rule: Maximum order value
        var totalValue = order.Items.Sum(i => i.TotalPrice);
        const decimal maxOrderValue = 100000m; // $100,000 limit

        if (totalValue > maxOrderValue)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "TotalAmount",
                Code = "ORDER_VALUE_EXCEEDED",
                Message = $"Order total ${totalValue:N2} exceeds maximum allowed value of ${maxOrderValue:N2}",
                AttemptedValue = totalValue,
                Context = new Dictionary<string, object> { ["MaxOrderValue"] = maxOrderValue }
            });
        }

        // Business rule: Maximum items per order
        const int maxItemsPerOrder = 100;
        if (order.Items.Count > maxItemsPerOrder)
        {
            result.Errors.Add(new ValidationError
            {
                Field = nameof(order.Items),
                Code = "TOO_MANY_ITEMS",
                Message = $"Order contains {order.Items.Count} items, maximum allowed is {maxItemsPerOrder}",
                AttemptedValue = order.Items.Count,
                Context = new Dictionary<string, object> { ["MaxItems"] = maxItemsPerOrder }
            });
        }

        // Business rule: Delivery date constraints
        if (order.DeliveryDate.HasValue)
        {
            var businessDaysFromNow = CalculateBusinessDays(DateTime.UtcNow.Date, order.DeliveryDate.Value.Date);
            const int minBusinessDays = 1; // At least 1 business day notice

            if (businessDaysFromNow < minBusinessDays)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = nameof(order.DeliveryDate),
                    Code = "INSUFFICIENT_LEAD_TIME",
                    Message = $"Delivery date requires at least {minBusinessDays} business day(s) notice",
                    AttemptedValue = order.DeliveryDate,
                    Context = new Dictionary<string, object> 
                    { 
                        ["MinBusinessDays"] = minBusinessDays,
                        ["BusinessDaysFromNow"] = businessDaysFromNow
                    }
                });
            }
        }

        return Task.CompletedTask;
    }

    private static int CalculateBusinessDays(DateTime startDate, DateTime endDate)
    {
        if (endDate < startDate)
            return 0;

        var businessDays = 0;
        var currentDate = startDate;

        while (currentDate < endDate)
        {
            if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
            {
                businessDays++;
            }
            currentDate = currentDate.AddDays(1);
        }

        return businessDays;
    }
}