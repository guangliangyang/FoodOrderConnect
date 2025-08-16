using BidOne.OrderIntegrationFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Add Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Configure logging
        services.Configure<LoggerFilterOptions>(options =>
        {
            var toRemove = options.Rules.FirstOrDefault(rule => rule.ProviderName
                == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

            if (toRemove is not null)
            {
                options.Rules.Remove(toRemove);
            }
        });

        // Add Entity Framework for SQL Server
        services.AddDbContext<OrderValidationDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("SqlConnectionString"),
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null);
                    sqlOptions.CommandTimeout(30);
                });
        });

        // Add Cosmos DB client for product enrichment
        services.AddDbContext<ProductEnrichmentDbContext>(options =>
        {
            options.UseCosmos(
                configuration.GetConnectionString("CosmosDbConnectionString") ?? "",
                "BidOneDB");
        });

        // Add Azure Service Bus
        var serviceBusConnectionString = configuration.GetConnectionString("ServiceBusConnection");
        if (!string.IsNullOrEmpty(serviceBusConnectionString))
        {
            services.AddSingleton<Azure.Messaging.ServiceBus.ServiceBusClient>(provider =>
                new Azure.Messaging.ServiceBus.ServiceBusClient(serviceBusConnectionString));
        }

        // Add HTTP client
        services.AddHttpClient<IExternalDataService, ExternalDataService>();

        // Add custom services
        services.AddScoped<IOrderValidationService, OrderValidationService>();
        services.AddScoped<IOrderEnrichmentService, OrderEnrichmentService>();
        services.AddScoped<IExternalDataService, ExternalDataService>();
        services.AddScoped<INotificationService, NotificationService>();
    })
    .Build();

host.Run();

// Note: HTTP retry policy can be configured through dependency injection if needed
