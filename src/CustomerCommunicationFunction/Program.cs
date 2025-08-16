using BidOne.CustomerCommunicationFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register custom services
        services.AddScoped<ICustomerCommunicationService, CustomerCommunicationService>();
        services.AddScoped<ILangChainService, LangChainService>();
        services.AddScoped<INotificationService, NotificationService>();

        // Configure Serilog
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "🕐 {Timestamp:HH:mm:ss} [{Level:u3}] 🤖 CustomerComm: {Message:lj}{NewLine}{Exception}")
            .WriteTo.ApplicationInsights(
                services.BuildServiceProvider().GetRequiredService<Microsoft.ApplicationInsights.TelemetryClient>(),
                TelemetryConverter.Traces)
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(logger);
        });
    })
    .Build();

host.Run();
