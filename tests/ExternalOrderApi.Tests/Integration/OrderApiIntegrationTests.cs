using BidOne.ExternalOrderApi;
using BidOne.Shared.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BidOne.ExternalOrderApi.Tests.Integration;

public class OrderApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public OrderApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Override services for testing if needed
                // For example, replace message publisher with test double
            });
        });
        
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [Fact]
    public async Task CreateOrder_WithValidRequest_ReturnsAccepted()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-INT-001",
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "PROD-001",
                    Quantity = 2,
                    UnitPrice = 25.00m
                },
                new()
                {
                    ProductId = "PROD-002",
                    Quantity = 1,
                    UnitPrice = 50.00m
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var orderResponse = JsonSerializer.Deserialize<OrderResponse>(responseContent, _jsonOptions);
        
        orderResponse.Should().NotBeNull();
        orderResponse!.OrderId.Should().NotBeNullOrEmpty();
        orderResponse.OrderId.Should().StartWith("ORD-");
        orderResponse.Status.Should().Be(OrderStatus.Received);
        orderResponse.Message.Should().Be("Order received and queued for processing");
    }

    [Fact]
    public async Task CreateOrder_WithInvalidRequest_ReturnsBadRequest()
    {
        // Arrange - Invalid request with empty customer ID
        var request = new CreateOrderRequest
        {
            CustomerId = "", // Invalid
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "PROD-001",
                    Quantity = 1,
                    UnitPrice = 10.00m
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Validation Failed");
    }

    [Fact]
    public async Task CreateOrder_WithEmptyItems_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-001",
            Items = new List<CreateOrderItemRequest>() // Empty items
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_WithNullRequest_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsync("/api/orders", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HealthCheck_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/orders/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("status");
        responseContent.Should().Contain("healthy");
    }

    [Theory]
    [InlineData(1, 10.00)]
    [InlineData(5, 25.50)]
    [InlineData(100, 1.99)]
    public async Task CreateOrder_WithVariousQuantitiesAndPrices_ReturnsAccepted(int quantity, decimal price)
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-THEORY-001",
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "PROD-THEORY-001",
                    Quantity = quantity,
                    UnitPrice = price
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var orderResponse = JsonSerializer.Deserialize<OrderResponse>(responseContent, _jsonOptions);
        
        orderResponse.Should().NotBeNull();
        orderResponse!.Status.Should().Be(OrderStatus.Received);
    }

    [Fact]
    public async Task CreateOrder_WithLargeOrder_ReturnsAccepted()
    {
        // Arrange - Create order with many items
        var items = new List<CreateOrderItemRequest>();
        for (int i = 1; i <= 50; i++)
        {
            items.Add(new CreateOrderItemRequest
            {
                ProductId = $"PROD-{i:000}",
                Quantity = i % 10 + 1,
                UnitPrice = (decimal)(i * 2.5)
            });
        }

        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-LARGE-001",
            Items = items
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var orderResponse = JsonSerializer.Deserialize<OrderResponse>(responseContent, _jsonOptions);
        
        orderResponse.Should().NotBeNull();
        orderResponse!.Status.Should().Be(OrderStatus.Received);
    }

    [Fact]
    public async Task CreateMultipleOrders_Concurrently_AllReturnAccepted()
    {
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();
        
        for (int i = 1; i <= 10; i++)
        {
            var request = new CreateOrderRequest
            {
                CustomerId = $"CUST-CONCURRENT-{i:000}",
                Items = new List<CreateOrderItemRequest>
                {
                    new()
                    {
                        ProductId = $"PROD-{i:000}",
                        Quantity = i,
                        UnitPrice = 10.00m
                    }
                }
            };
            
            tasks.Add(_client.PostAsJsonAsync("/api/orders", request, _jsonOptions));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(10);
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Accepted);
        
        // Verify all orders have unique IDs
        var orderIds = new List<string>();
        foreach (var response in responses)
        {
            var content = await response.Content.ReadAsStringAsync();
            var orderResponse = JsonSerializer.Deserialize<OrderResponse>(content, _jsonOptions);
            orderIds.Add(orderResponse!.OrderId);
        }
        
        orderIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task CreateOrder_WithSpecialCharactersInCustomerId_ReturnsAccepted()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-SPECIAL-ÄÖÜ-001", // Special characters
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "PROD-SPECIAL-001",
                    Quantity = 1,
                    UnitPrice = 15.00m
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task CreateOrder_WithDecimalPrices_HandlesCorrectly()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-DECIMAL-001",
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "PROD-DECIMAL-001",
                    Quantity = 3,
                    UnitPrice = 15.999m // Many decimal places
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var orderResponse = JsonSerializer.Deserialize<OrderResponse>(responseContent, _jsonOptions);
        
        orderResponse.Should().NotBeNull();
        orderResponse!.Status.Should().Be(OrderStatus.Received);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
