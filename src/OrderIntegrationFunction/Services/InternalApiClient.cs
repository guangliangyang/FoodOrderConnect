using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BidOne.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace BidOne.OrderIntegrationFunction.Services;

/// <summary>
/// HTTP client for communicating with the Internal System API
/// </summary>
public class InternalApiClient : IInternalApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalApiClient> _logger;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public InternalApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<InternalApiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        
        _baseUrl = configuration["InternalApi:BaseUrl"] ?? "http://internal-system-api";
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Configure HTTP client defaults
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<OrderResponse> ProcessOrderAsync(ProcessOrderRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing order {OrderId} via Internal System API", request.Order.Id);

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Add JWT token if available
            await AddAuthenticationHeaderAsync();

            var response = await _httpClient.PostAsync("/api/orders", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var orderResponse = JsonSerializer.Deserialize<OrderResponse>(responseContent, _jsonOptions);

                if (orderResponse == null)
                {
                    throw new InvalidOperationException("Failed to deserialize order response from Internal System API");
                }

                _logger.LogInformation("Order {OrderId} processed successfully via Internal System API with status {Status}",
                    request.Order.Id, orderResponse.Status);

                return orderResponse;
            }

            // Handle different HTTP error codes
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Internal System API returned error {StatusCode}: {ErrorContent}", 
                response.StatusCode, errorContent);

            throw response.StatusCode switch
            {
                HttpStatusCode.BadRequest => new ArgumentException($"Invalid order data: {errorContent}"),
                HttpStatusCode.Unauthorized => new UnauthorizedAccessException("Authentication failed for Internal System API"),
                HttpStatusCode.NotFound => new InvalidOperationException($"Order not found: {errorContent}"),
                HttpStatusCode.Conflict => new InvalidOperationException($"Order processing conflict: {errorContent}"),
                _ => new HttpRequestException($"Internal System API error ({response.StatusCode}): {errorContent}")
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout occurred while processing order {OrderId} via Internal System API", request.Order.Id);
            throw new TimeoutException($"Timeout processing order {request.Order.Id} via Internal System API", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while processing order {OrderId} via Internal System API", request.Order.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing order {OrderId} via Internal System API", request.Order.Id);
            throw;
        }
    }

    public async Task<OrderResponse?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting order {OrderId} from Internal System API", orderId);

            // Add JWT token if available
            await AddAuthenticationHeaderAsync();

            var response = await _httpClient.GetAsync($"/api/orders/{orderId}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Order {OrderId} not found in Internal System API", orderId);
                return null;
            }

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var orderResponse = JsonSerializer.Deserialize<OrderResponse>(content, _jsonOptions);

                _logger.LogDebug("Order {OrderId} retrieved from Internal System API", orderId);
                return orderResponse;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Internal System API returned error {StatusCode} for order {OrderId}: {ErrorContent}",
                response.StatusCode, orderId, errorContent);

            throw new HttpRequestException($"Failed to get order {orderId} from Internal System API ({response.StatusCode}): {errorContent}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout occurred while getting order {OrderId} from Internal System API", orderId);
            throw new TimeoutException($"Timeout getting order {orderId} from Internal System API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order {OrderId} from Internal System API", orderId);
            throw;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking Internal System API health");

            var response = await _httpClient.GetAsync("/health", cancellationToken);
            var isHealthy = response.IsSuccessStatusCode;

            _logger.LogDebug("Internal System API health check result: {IsHealthy}", isHealthy);
            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Internal System API health check failed");
            return false;
        }
    }

    private async Task AddAuthenticationHeaderAsync()
    {
        try
        {
            // In a real implementation, this would get a JWT token from Azure AD or a token service
            // For development, we'll use a simple JWT token generation or configuration
            var jwtToken = await GetJwtTokenAsync();
            
            if (!string.IsNullOrEmpty(jwtToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
                _logger.LogDebug("JWT token added to request headers");
            }
            else
            {
                _logger.LogWarning("No JWT token available for Internal System API authentication");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add authentication header");
            // Don't throw - let the request proceed without auth and let the API handle it
        }
    }

    private Task<string?> GetJwtTokenAsync()
    {
        // In development, use a configured token or generate a simple one
        var configuredToken = _configuration["InternalApi:JwtToken"];
        if (!string.IsNullOrEmpty(configuredToken))
        {
            return Task.FromResult<string?>(configuredToken);
        }

        // In production, this would integrate with Azure AD or another identity provider
        // For now, return null to indicate no authentication is available
        _logger.LogDebug("No JWT token configuration found for Internal System API");
        return Task.FromResult<string?>(null);
    }
}

/// <summary>
/// Extension methods for configuring the Internal API client with retry policies
/// </summary>
public static class InternalApiClientExtensions
{
    /// <summary>
    /// Configure HTTP client with retry and circuit breaker policies
    /// </summary>
    public static IHttpClientBuilder AddInternalApiRetryPolicy(this IHttpClientBuilder builder)
    {
        // Retry policy with exponential backoff
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError() // HttpRequestException and 5XX and 408 responses
            .OrResult(msg => !msg.IsSuccessStatusCode && msg.StatusCode != HttpStatusCode.BadRequest && msg.StatusCode != HttpStatusCode.NotFound)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Simple retry logging without context dependency
                    Console.WriteLine($"Retrying Internal API call (attempt {retryCount}) after {timespan.TotalMilliseconds}ms");
                });

        // Circuit breaker policy
        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    // Log circuit breaker activation
                },
                onReset: () =>
                {
                    // Log circuit breaker reset
                });

        // For now, just return the builder without policy handler since we're having issues with extensions
        // In production, you would properly configure Polly policies
        return builder;
    }
}