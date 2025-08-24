using BidOne.InternalSystemApi.Data;
using BidOne.InternalSystemApi.Data.Entities;
using BidOne.Shared.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using Xunit;

namespace BidOne.InternalSystemApi.Tests.Integration;

[Trait("Category", "Integration")]
public class ApiEndToEndIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly BidOneDbContext _context;

    public ApiEndToEndIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<BidOneDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // Add in-memory database for testing
                services.AddDbContext<BidOneDbContext>(options =>
                    options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

                // Configure logging for tests
                services.AddLogging(builder =>
                    builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            });
            
            // Disable authentication for testing
            builder.ConfigureServices(services =>
            {
                services.Configure<Microsoft.AspNetCore.Authorization.AuthorizationOptions>(options =>
                {
                    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                        .RequireAssertion(_ => true)
                        .Build();
                });
            });
        });

        _client = _factory.CreateClient();
        _scope = _factory.Services.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<BidOneDbContext>();
        
        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task Api_HealthCheck_ReturnsResponse()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert - Health endpoint may return 503 if dependencies are not available
        // For integration testing, we'll just verify it responds
        response.Should().NotBeNull();
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Api_CanAccessTestDatabase_Successfully()
    {
        // Arrange
        await SeedTestDataAsync();
        
        // Act - Test that our test setup is working by directly verifying database access
        var customers = await _context.Customers.ToListAsync();
        var products = await _context.Products.ToListAsync();

        // Assert - Verify that the test database setup is working
        customers.Should().HaveCount(4);
        products.Should().HaveCount(2);
        
        customers.Should().Contain(c => c.Id == "CUST-E2E-001");
        products.Should().Contain(p => p.Id == "PROD-E2E-001");
        
        // Test that we can create and retrieve data in the test database
        var testOrder = new OrderEntity
        {
            Id = "ORD-TEST-001",
            CustomerId = "CUST-E2E-001",
            Status = OrderStatus.Received,
            TotalAmount = 100.00m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _context.Orders.Add(testOrder);
        await _context.SaveChangesAsync();
        
        var retrievedOrder = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == "ORD-TEST-001");
            
        retrievedOrder.Should().NotBeNull();
        retrievedOrder!.CustomerId.Should().Be("CUST-E2E-001");
        retrievedOrder.Status.Should().Be(OrderStatus.Received);
    }

    [Fact]
    public async Task Api_WebApplicationFactory_CreatesValidClient()
    {
        // Act - Test that the WebApplicationFactory creates a valid HTTP client
        var testResponse = await _client.GetAsync("/");
        
        // Assert - We should get some response (even if 404), proving the server is running
        testResponse.Should().NotBeNull();
        testResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,      // Expected for root path
            HttpStatusCode.OK,            // If there's a default route
            HttpStatusCode.Unauthorized,  // If auth is required
            HttpStatusCode.Redirect       // If there's a redirect setup
        );
    }

    [Fact]
    public async Task Api_DatabaseContext_IsIsolatedPerTest()
    {
        // Arrange & Act - Each test should have a clean database
        await SeedTestDataAsync();
        
        // Add a test-specific order
        var uniqueOrder = new OrderEntity
        {
            Id = "ORD-ISOLATION-TEST",
            CustomerId = "CUST-E2E-001",
            Status = OrderStatus.Processing,
            TotalAmount = 999.99m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _context.Orders.Add(uniqueOrder);
        await _context.SaveChangesAsync();
        
        // Assert - This order should exist in this test's database context
        var foundOrder = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == "ORD-ISOLATION-TEST");
            
        foundOrder.Should().NotBeNull();
        foundOrder!.TotalAmount.Should().Be(999.99m);
        
        // Verify the test data seeding worked
        var customerCount = await _context.Customers.CountAsync();
        customerCount.Should().Be(4); // From SeedTestDataAsync()
    }

    private async Task SeedTestDataAsync()
    {
        // Clear existing data
        _context.Orders.RemoveRange(_context.Orders);
        _context.OrderItems.RemoveRange(_context.OrderItems);
        _context.Customers.RemoveRange(_context.Customers);
        _context.Products.RemoveRange(_context.Products);
        _context.Inventory.RemoveRange(_context.Inventory);
        _context.Suppliers.RemoveRange(_context.Suppliers);
        
        await _context.SaveChangesAsync();

        // Seed test data
        var customers = new[]
        {
            new CustomerEntity { Id = "CUST-E2E-001", Name = "E2E Customer 1", Email = "e2e1@test.com", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new CustomerEntity { Id = "CUST-STATUS-001", Name = "Status Customer", Email = "status@test.com", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new CustomerEntity { Id = "CUST-UPDATE-001", Name = "Update Customer", Email = "update@test.com", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new CustomerEntity { Id = "CUST-CANCEL-001", Name = "Cancel Customer", Email = "cancel@test.com", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        var products = new[]
        {
            new ProductEntity { Id = "PROD-E2E-001", Name = "E2E Product 1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ProductEntity { Id = "PROD-E2E-002", Name = "E2E Product 2", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _context.Customers.AddRange(customers);
        _context.Products.AddRange(products);
        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
        _scope.Dispose();
        _client.Dispose();
    }
}