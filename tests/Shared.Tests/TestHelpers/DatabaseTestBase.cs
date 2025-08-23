using BidOne.InternalSystemApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BidOne.Shared.Tests.TestHelpers;

public abstract class DatabaseTestBase : IDisposable
{
    protected readonly BidOneDbContext Context;
    protected readonly IServiceProvider ServiceProvider;
    private readonly ServiceCollection _services;

    protected DatabaseTestBase()
    {
        _services = new ServiceCollection();
        ConfigureServices(_services);
        ServiceProvider = _services.BuildServiceProvider();
        Context = ServiceProvider.GetRequiredService<BidOneDbContext>();
        
        // Ensure database is created
        Context.Database.EnsureCreated();
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Configure in-memory database
        services.AddDbContext<BidOneDbContext>(options =>
            options.UseInMemoryDatabase(TestEnvironment.GetTestDatabaseName()));

        // Add logging
        services.AddLogging(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Add test configuration
        services.AddSingleton(TestConfiguration.CreateTestConfiguration());
    }

    protected async Task SeedTestDataAsync()
    {
        // Add common test data
        var customers = TestDataFactory.CreateOrderEntityList(3)
            .Select(o => TestDataFactory.CreateValidCustomerEntity(o.CustomerId))
            .ToList();

        var products = TestDataFactory.CreateInventoryEntityList(5)
            .Select(i => TestDataFactory.CreateValidProductEntity(i.ProductId))
            .ToList();

        var inventory = TestDataFactory.CreateInventoryEntityList(5);

        await Context.Customers.AddRangeAsync(customers);
        await Context.Products.AddRangeAsync(products);
        await Context.Inventory.AddRangeAsync(inventory);
        await Context.SaveChangesAsync();
    }

    protected async Task ClearDatabaseAsync()
    {
        Context.Orders.RemoveRange(Context.Orders);
        Context.OrderItems.RemoveRange(Context.OrderItems);
        Context.Customers.RemoveRange(Context.Customers);
        Context.Products.RemoveRange(Context.Products);
        Context.Inventory.RemoveRange(Context.Inventory);
        Context.Suppliers.RemoveRange(Context.Suppliers);
        Context.AuditLogs.RemoveRange(Context.AuditLogs);
        Context.OrderEvents.RemoveRange(Context.OrderEvents);
        
        await Context.SaveChangesAsync();
    }

    public virtual void Dispose()
    {
        Context.Dispose();
        ServiceProvider?.GetService<IServiceScope>()?.Dispose();
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

public abstract class ServiceTestBase<TService> : DatabaseTestBase
    where TService : class
{
    protected readonly TService Service;

    protected ServiceTestBase()
    {
        Service = ServiceProvider.GetRequiredService<TService>();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        ConfigureServiceUnderTest(services);
    }

    protected abstract void ConfigureServiceUnderTest(IServiceCollection services);
}

public abstract class ControllerTestBase<TController> : DatabaseTestBase
    where TController : class
{
    protected readonly TController Controller;

    protected ControllerTestBase()
    {
        Controller = ServiceProvider.GetRequiredService<TController>();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        ConfigureController(services);
    }

    protected abstract void ConfigureController(IServiceCollection services);
}
