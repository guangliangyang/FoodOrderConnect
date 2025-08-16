using System.Text.Json;
using BidOne.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BidOne.OrderIntegrationFunction.Services;

public class NotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendOrderConfirmationAsync(Order order, string customerEmail, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending order confirmation for order {OrderId} to {Email}", order.Id, customerEmail);

            var template = new NotificationTemplate
            {
                Subject = $"Order Confirmation - {order.Id}",
                Body = BuildOrderConfirmationBody(order),
                RecipientEmail = customerEmail,
                Variables = new Dictionary<string, object>
                {
                    ["OrderId"] = order.Id,
                    ["OrderTotal"] = order.Items.Sum(i => i.TotalPrice),
                    ["ItemCount"] = order.Items.Count,
                    ["EstimatedDelivery"] = order.DeliveryDate?.ToString("yyyy-MM-dd") ?? "TBD"
                }
            };

            var result = await SendNotificationAsync(template, "OrderConfirmation", cancellationToken);

            if (result.IsSuccessful)
            {
                _logger.LogInformation("Order confirmation sent successfully for order {OrderId}", order.Id);
            }
            else
            {
                _logger.LogWarning("Failed to send order confirmation for order {OrderId}: {Error}",
                    order.Id, result.ErrorMessage);
            }

            return result.IsSuccessful;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending order confirmation for order {OrderId}", order.Id);
            return false;
        }
    }

    public async Task<bool> SendOrderUpdateAsync(Order order, string customerEmail, string updateMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending order update for order {OrderId} to {Email}", order.Id, customerEmail);

            var template = new NotificationTemplate
            {
                Subject = $"Order Update - {order.Id}",
                Body = BuildOrderUpdateBody(order, updateMessage),
                RecipientEmail = customerEmail,
                Variables = new Dictionary<string, object>
                {
                    ["OrderId"] = order.Id,
                    ["UpdateMessage"] = updateMessage,
                    ["OrderStatus"] = order.Status.ToString(),
                    ["UpdateTime"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
                }
            };

            var result = await SendNotificationAsync(template, "OrderUpdate", cancellationToken);

            if (result.IsSuccessful)
            {
                _logger.LogInformation("Order update sent successfully for order {OrderId}", order.Id);
            }
            else
            {
                _logger.LogWarning("Failed to send order update for order {OrderId}: {Error}",
                    order.Id, result.ErrorMessage);
            }

            return result.IsSuccessful;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending order update for order {OrderId}", order.Id);
            return false;
        }
    }

    public async Task<bool> NotifySupplierAsync(Order order, string supplierEmail, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending supplier notification for order {OrderId} to {Email}", order.Id, supplierEmail);

            var template = new NotificationTemplate
            {
                Subject = $"New Order Received - {order.Id}",
                Body = BuildSupplierNotificationBody(order),
                RecipientEmail = supplierEmail,
                Variables = new Dictionary<string, object>
                {
                    ["OrderId"] = order.Id,
                    ["CustomerId"] = order.CustomerId,
                    ["OrderDate"] = order.CreatedAt.ToString("yyyy-MM-dd"),
                    ["RequestedDelivery"] = order.DeliveryDate?.ToString("yyyy-MM-dd") ?? "Not specified",
                    ["ItemCount"] = order.Items.Count,
                    ["OrderValue"] = order.Items.Sum(i => i.TotalPrice)
                }
            };

            var result = await SendNotificationAsync(template, "SupplierNotification", cancellationToken);

            if (result.IsSuccessful)
            {
                _logger.LogInformation("Supplier notification sent successfully for order {OrderId}", order.Id);
            }
            else
            {
                _logger.LogWarning("Failed to send supplier notification for order {OrderId}: {Error}",
                    order.Id, result.ErrorMessage);
            }

            return result.IsSuccessful;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending supplier notification for order {OrderId}", order.Id);
            return false;
        }
    }

    public async Task<bool> SendDeliveryNotificationAsync(Order order, string customerEmail, string trackingNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending delivery notification for order {OrderId} to {Email}", order.Id, customerEmail);

            var template = new NotificationTemplate
            {
                Subject = $"Your Order is On Its Way - {order.Id}",
                Body = BuildDeliveryNotificationBody(order, trackingNumber),
                RecipientEmail = customerEmail,
                Variables = new Dictionary<string, object>
                {
                    ["OrderId"] = order.Id,
                    ["TrackingNumber"] = trackingNumber,
                    ["DeliveryAddress"] = order.DeliveryAddress ?? "Not specified",
                    ["EstimatedDelivery"] = order.DeliveryDate?.ToString("yyyy-MM-dd") ?? "TBD"
                }
            };

            var result = await SendNotificationAsync(template, "DeliveryNotification", cancellationToken);

            if (result.IsSuccessful)
            {
                _logger.LogInformation("Delivery notification sent successfully for order {OrderId}", order.Id);
            }
            else
            {
                _logger.LogWarning("Failed to send delivery notification for order {OrderId}: {Error}",
                    order.Id, result.ErrorMessage);
            }

            return result.IsSuccessful;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending delivery notification for order {OrderId}", order.Id);
            return false;
        }
    }

    public async Task<bool> SendErrorNotificationAsync(string errorMessage, string recipientEmail, Dictionary<string, object>? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending error notification to {Email}", recipientEmail);

            var template = new NotificationTemplate
            {
                Subject = "Order Processing Error",
                Body = BuildErrorNotificationBody(errorMessage, context),
                RecipientEmail = recipientEmail,
                Variables = new Dictionary<string, object>
                {
                    ["ErrorMessage"] = errorMessage,
                    ["ErrorTime"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    ["Context"] = context ?? new Dictionary<string, object>()
                }
            };

            var result = await SendNotificationAsync(template, "ErrorNotification", cancellationToken);

            if (result.IsSuccessful)
            {
                _logger.LogInformation("Error notification sent successfully");
            }
            else
            {
                _logger.LogWarning("Failed to send error notification: {Error}", result.ErrorMessage);
            }

            return result.IsSuccessful;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending error notification");
            return false;
        }
    }

    private async Task<NotificationResult> SendNotificationAsync(NotificationTemplate template, string templateType, CancellationToken cancellationToken)
    {
        try
        {
            var emailServiceUrl = _configuration["ExternalServices:EmailServiceUrl"];

            if (string.IsNullOrEmpty(emailServiceUrl))
            {
                _logger.LogWarning("Email service URL not configured. Simulating email send.");
                return await SimulateEmailSend(template, templateType);
            }

            var requestData = new
            {
                To = template.RecipientEmail,
                Subject = template.Subject,
                Body = template.Body,
                TemplateType = templateType,
                Variables = template.Variables
            };

            var jsonContent = JsonSerializer.Serialize(requestData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            // Add authorization header if configured
            var apiKey = _configuration["ExternalServices:EmailServiceApiKey"];
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }

            var response = await _httpClient.PostAsync($"{emailServiceUrl}/send", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<NotificationResult>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return result ?? new NotificationResult
                {
                    IsSuccessful = true,
                    NotificationId = Guid.NewGuid().ToString(),
                    SentAt = DateTime.UtcNow,
                    Provider = "ExternalEmailService"
                };
            }

            return new NotificationResult
            {
                IsSuccessful = false,
                ErrorMessage = $"Email service returned {response.StatusCode}",
                SentAt = DateTime.UtcNow,
                Provider = "ExternalEmailService"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while sending notification");
            return await SimulateEmailSend(template, templateType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification via email service");
            return new NotificationResult
            {
                IsSuccessful = false,
                ErrorMessage = ex.Message,
                SentAt = DateTime.UtcNow,
                Provider = "ExternalEmailService"
            };
        }
    }

    private async Task<NotificationResult> SimulateEmailSend(NotificationTemplate template, string templateType)
    {
        await Task.Delay(100); // Simulate network delay

        _logger.LogInformation("SIMULATED EMAIL SEND:");
        _logger.LogInformation("To: {Email}", template.RecipientEmail);
        _logger.LogInformation("Subject: {Subject}", template.Subject);
        _logger.LogInformation("Template Type: {TemplateType}", templateType);
        _logger.LogInformation("Body Preview: {BodyPreview}",
            template.Body.Length > 100 ? template.Body[..100] + "..." : template.Body);

        return new NotificationResult
        {
            IsSuccessful = true,
            NotificationId = Guid.NewGuid().ToString(),
            SentAt = DateTime.UtcNow,
            Provider = "SimulatedEmailService"
        };
    }

    private static string BuildOrderConfirmationBody(Order order)
    {
        var totalAmount = order.Items.Sum(i => i.TotalPrice);
        var itemList = string.Join("\n", order.Items.Select(item =>
            $"- {item.ProductName} (Qty: {item.Quantity}) - ${item.TotalPrice:N2}"));

        return $@"Dear Customer,

Thank you for your order! Your order #{order.Id} has been received and is being processed.

Order Details:
{itemList}

Total Amount: ${totalAmount:N2}
Order Date: {order.CreatedAt:yyyy-MM-dd}
{(order.DeliveryDate.HasValue ? $"Estimated Delivery: {order.DeliveryDate.Value:yyyy-MM-dd}" : "")}
{(!string.IsNullOrEmpty(order.DeliveryAddress) ? $"Delivery Address: {order.DeliveryAddress}" : "")}

{(!string.IsNullOrEmpty(order.SpecialInstructions) ? $"Special Instructions: {order.SpecialInstructions}" : "")}

We'll send you updates as your order progresses.

Thank you for choosing us!

Best regards,
BidOne Team";
    }

    private static string BuildOrderUpdateBody(Order order, string updateMessage)
    {
        return $@"Dear Customer,

We have an update regarding your order #{order.Id}.

Update: {updateMessage}

Current Status: {order.Status}
Last Updated: {DateTime.UtcNow:yyyy-MM-dd HH:mm UTC}

If you have any questions, please don't hesitate to contact us.

Best regards,
BidOne Team";
    }

    private static string BuildSupplierNotificationBody(Order order)
    {
        var totalAmount = order.Items.Sum(i => i.TotalPrice);
        var itemList = string.Join("\n", order.Items.Select(item =>
            $"- Product ID: {item.ProductId}, Name: {item.ProductName}, Qty: {item.Quantity}, Unit Price: ${item.UnitPrice:N2}"));

        return $@"New Order Received

Order ID: {order.Id}
Customer ID: {order.CustomerId}
Order Date: {order.CreatedAt:yyyy-MM-dd}
Total Value: ${totalAmount:N2}

Items Ordered:
{itemList}

{(order.DeliveryDate.HasValue ? $"Requested Delivery Date: {order.DeliveryDate.Value:yyyy-MM-dd}" : "No specific delivery date requested")}
{(!string.IsNullOrEmpty(order.DeliveryAddress) ? $"Delivery Address: {order.DeliveryAddress}" : "")}

{(!string.IsNullOrEmpty(order.SpecialInstructions) ? $"Special Instructions: {order.SpecialInstructions}" : "")}

Please confirm availability and expected fulfillment timeline.

BidOne Order Management System";
    }

    private static string BuildDeliveryNotificationBody(Order order, string trackingNumber)
    {
        return $@"Great News! Your Order is On Its Way

Your order #{order.Id} has been shipped and is on its way to you.

Tracking Information:
Tracking Number: {trackingNumber}
{(order.DeliveryDate.HasValue ? $"Estimated Delivery: {order.DeliveryDate.Value:yyyy-MM-dd}" : "")}
{(!string.IsNullOrEmpty(order.DeliveryAddress) ? $"Delivery Address: {order.DeliveryAddress}" : "")}

You can track your package using the tracking number provided above.

We hope you enjoy your order!

Best regards,
BidOne Team";
    }

    private static string BuildErrorNotificationBody(string errorMessage, Dictionary<string, object>? context)
    {
        var contextInfo = context != null && context.Any()
            ? "\n\nContext Information:\n" + string.Join("\n", context.Select(kvp => $"- {kvp.Key}: {kvp.Value}"))
            : "";

        return $@"Order Processing Error

An error occurred while processing an order:

Error: {errorMessage}
Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}
{contextInfo}

Please review and take appropriate action.

BidOne Order Management System";
    }
}
