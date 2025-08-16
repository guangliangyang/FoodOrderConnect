using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.Http;
using System.Net;

namespace BidOne.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHttpClientWithRetry(
        this IServiceCollection services,
        string name,
        Action<HttpClient>? configureClient = null,
        int retryCount = 3,
        TimeSpan? baseDelay = null)
    {
        var delay = baseDelay ?? TimeSpan.FromSeconds(1);

        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + delay,
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogWarning("HTTP retry {RetryCount} for {HttpMethod} {Uri} in {Delay}ms. Result: {Result}",
                        retryCount,
                        context["HttpMethod"],
                        context["Uri"],
                        timespan.TotalMilliseconds,
                        outcome.Result?.StatusCode.ToString() ?? outcome.Exception?.Message);
                });

        services.AddHttpClient(name, configureClient ?? (_ => { }))
            .AddPolicyHandler(retryPolicy)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            });

        return services;
    }

    public static IServiceCollection AddCircuitBreaker(
        this IServiceCollection services,
        string name,
        int handledEventsAllowedBeforeBreaking = 5,
        TimeSpan? durationOfBreak = null)
    {
        var breakDuration = durationOfBreak ?? TimeSpan.FromSeconds(30);

        var circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking,
                breakDuration,
                onBreak: (exception, duration) =>
                {
                    // Log circuit breaker opening
                },
                onReset: () =>
                {
                    // Log circuit breaker closing
                });

        // Register the policy in DI if needed
        return services;
    }

    public static IServiceCollection AddBulkhead(
        this IServiceCollection services,
        string name,
        int maxParallelization = 10,
        int maxQueuingActions = 100)
    {
        var bulkheadPolicy = Policy.BulkheadAsync(
            maxParallelization,
            maxQueuingActions);

        // Register the policy in DI if needed
        return services;
    }

    private static ILogger? GetLogger(this Context context)
    {
        if (context.TryGetValue("ILogger", out var logger))
        {
            return logger as ILogger;
        }
        return null;
    }
}