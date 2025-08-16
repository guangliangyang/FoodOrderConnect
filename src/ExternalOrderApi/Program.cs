using System.Text.Json;
using Azure.Messaging.ServiceBus;
using BidOne.ExternalOrderApi.Services;
using BidOne.ExternalOrderApi.Validators;
using BidOne.Shared.Metrics;
using BidOne.Shared.Services;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Distributed;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.ApplicationInsights(builder.Configuration.GetConnectionString("ApplicationInsights") ?? "", TelemetryConverter.Traces)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "BidOne External Order API",
        Version = "v1",
        Description = "API for receiving external orders from suppliers and partners"
    });
});

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry(builder.Configuration);

// Add Azure Service Bus
var serviceBusConnectionString = builder.Configuration.GetConnectionString("ServiceBus");
if (!string.IsNullOrEmpty(serviceBusConnectionString))
{
    builder.Services.AddSingleton<ServiceBusClient>(provider =>
        new ServiceBusClient(serviceBusConnectionString));
    builder.Services.AddScoped<IMessagePublisher, ServiceBusMessagePublisher>();
}
else
{
    // Use a console-based publisher for development/testing
    builder.Services.AddScoped<IMessagePublisher, ConsoleMessagePublisher>();
}

// Add Redis Cache (optional)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
    });
}
else
{
    // Use in-memory distributed cache as fallback for development
    builder.Services.AddDistributedMemoryCache();
}

// Add custom services
builder.Services.AddScoped<IOrderService, OrderService>();

// Add Dashboard Event Publisher
builder.Services.AddScoped<IDashboardEventPublisher, DashboardEventPublisher>();

// 📊 Add Prometheus metrics (演示监控能力)
builder.Services.AddSingleton<MetricServer>(provider =>
{
    var metricServer = new MetricServer(hostname: "*", port: 9090);
    metricServer.Start();
    return metricServer;
});

// Add health checks
var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

// Add Redis health check if connection string is provided
if (!string.IsNullOrEmpty(redisConnectionString))
{
    healthChecksBuilder.AddRedis(redisConnectionString, name: "redis");
}

// Add Service Bus health check if connection string is provided
if (!string.IsNullOrEmpty(serviceBusConnectionString))
{
    healthChecksBuilder.AddAzureServiceBusQueue(
        serviceBusConnectionString,
        "order-received",
        name: "servicebus-order-queue");
}

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BidOne External Order API V1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

// 📊 Enable Prometheus metrics endpoint (/metrics)
app.UseMetricServer();
app.UseHttpMetrics(); // 自动收集 HTTP 请求指标

app.UseAuthorization();

// Add request logging
app.UseSerilogRequestLogging();

// Configure health checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                exception = x.Value.Exception?.Message,
                duration = x.Value.Duration.ToString()
            }),
            duration = report.TotalDuration.ToString()
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapControllers();

try
{
    Log.Information("Starting BidOne External Order API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
