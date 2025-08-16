using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BidOne.InternalSystemApi.Data.Entities;

namespace BidOne.InternalSystemApi.Services;

public class SupplierNotificationService : ISupplierNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SupplierNotificationService> _logger;

    public SupplierNotificationService(HttpClient httpClient, ILogger<SupplierNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> NotifyOrderAsync(SupplierEntity supplier, OrderEntity order, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(supplier.ApiEndpoint))
            {
                _logger.LogWarning("Supplier {SupplierId} has no API endpoint configured", supplier.Id);
                return false;
            }

            var notification = new SupplierOrderNotification
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                Items = order.Items.Select(item => new SupplierOrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice
                }).ToList(),
                TotalAmount = order.TotalAmount,
                DeliveryDate = order.DeliveryDate,
                Notes = order.Notes,
                CreatedAt = order.CreatedAt,
                ConfirmedAt = order.ConfirmedAt
            };

            var jsonContent = JsonSerializer.Serialize(notification, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Add authentication headers if needed
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "supplier-api-token");

            var response = await _httpClient.PostAsync($"{supplier.ApiEndpoint}/orders", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully notified supplier {SupplierId} about order {OrderId}",
                    supplier.Id, order.Id);
                return true;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to notify supplier {SupplierId} about order {OrderId}. Status: {StatusCode}, Response: {Response}",
                    supplier.Id, order.Id, response.StatusCode, responseContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying supplier {SupplierId} about order {OrderId}", supplier.Id, order.Id);
            return false;
        }
    }

    public async Task<bool> NotifyOrderUpdateAsync(SupplierEntity supplier, OrderEntity order, string updateType, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(supplier.ApiEndpoint))
            {
                _logger.LogWarning("Supplier {SupplierId} has no API endpoint configured", supplier.Id);
                return false;
            }

            var notification = new SupplierOrderUpdateNotification
            {
                OrderId = order.Id,
                UpdateType = updateType,
                Status = order.Status.ToString(),
                UpdatedAt = order.UpdatedAt,
                Notes = order.Notes
            };

            var jsonContent = JsonSerializer.Serialize(notification, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "supplier-api-token");

            var response = await _httpClient.PutAsync($"{supplier.ApiEndpoint}/orders/{order.Id}", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully notified supplier {SupplierId} about order {OrderId} update: {UpdateType}",
                    supplier.Id, order.Id, updateType);
                return true;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to notify supplier {SupplierId} about order {OrderId} update. Status: {StatusCode}, Response: {Response}",
                    supplier.Id, order.Id, response.StatusCode, responseContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying supplier {SupplierId} about order {OrderId} update", supplier.Id, order.Id);
            return false;
        }
    }

    public async Task<bool> NotifyOrderCancellationAsync(SupplierEntity supplier, string orderId, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(supplier.ApiEndpoint))
            {
                _logger.LogWarning("Supplier {SupplierId} has no API endpoint configured", supplier.Id);
                return false;
            }

            var notification = new SupplierOrderCancellationNotification
            {
                OrderId = orderId,
                Reason = reason,
                CancelledAt = DateTime.UtcNow
            };

            var jsonContent = JsonSerializer.Serialize(notification, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "supplier-api-token");

            var response = await _httpClient.DeleteAsync($"{supplier.ApiEndpoint}/orders/{orderId}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully notified supplier {SupplierId} about order {OrderId} cancellation",
                    supplier.Id, orderId);
                return true;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to notify supplier {SupplierId} about order {OrderId} cancellation. Status: {StatusCode}, Response: {Response}",
                    supplier.Id, orderId, response.StatusCode, responseContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying supplier {SupplierId} about order {OrderId} cancellation", supplier.Id, orderId);
            return false;
        }
    }
}

public class SupplierOrderNotification
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public List<SupplierOrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}

public class SupplierOrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class SupplierOrderUpdateNotification
{
    public string OrderId { get; set; } = string.Empty;
    public string UpdateType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string? Notes { get; set; }
}

public class SupplierOrderCancellationNotification
{
    public string OrderId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CancelledAt { get; set; }
}
