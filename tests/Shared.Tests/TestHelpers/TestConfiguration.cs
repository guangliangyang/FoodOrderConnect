using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace BidOne.Shared.Tests.TestHelpers;

public static class TestConfiguration
{
    public static IConfiguration CreateTestConfiguration(Dictionary<string, string?>? settings = null)
    {
        var defaultSettings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
            ["ConnectionStrings:ServiceBus"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=testkey",
            ["ConnectionStrings:Redis"] = "localhost:6379",
            ["ConnectionStrings:CosmosDb"] = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=testkey",
            ["ApplicationInsights:ConnectionString"] = "InstrumentationKey=test-key",
            ["Logging:LogLevel:Default"] = "Information",
            ["Logging:LogLevel:Microsoft"] = "Warning",
            ["Environment"] = "Testing"
        };

        if (settings != null)
        {
            foreach (var kvp in settings)
            {
                defaultSettings[kvp.Key] = kvp.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaultSettings)
            .Build();
    }

    public static ServiceCollection CreateTestServices(IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        var config = configuration ?? CreateTestConfiguration();
        
        services.AddSingleton(config);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        return services;
    }

    public static Mock<ILogger<T>> CreateMockLogger<T>()
    {
        return new Mock<ILogger<T>>();
    }

    public static ILogger<T> CreateTestLogger<T>()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        return loggerFactory.CreateLogger<T>();
    }
}

public static class TestEnvironment
{
    public static bool IsRunningInCI => 
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));

    public static string GetTestDatabaseName() => 
        $"TestDb_{Guid.NewGuid():N}";

    public static string GetTestContainerName() => 
        $"test-container-{Guid.NewGuid():N}"[..20];

    public static TimeSpan GetTestTimeout() => 
        IsRunningInCI ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(2);
}

public class TestSettings
{
    public const string DefaultConnectionString = "Data Source=:memory:";
    public const string TestServiceBusConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=testkey";
    public const string TestRedisConnectionString = "localhost:6379";
    public const string TestCosmosConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=testkey";
    
    public static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MediumTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan LongTimeout = TimeSpan.FromMinutes(2);
}
