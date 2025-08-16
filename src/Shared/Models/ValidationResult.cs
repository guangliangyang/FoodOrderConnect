namespace BidOne.Shared.Models;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public Dictionary<string, object> ValidationData { get; set; } = new();
    public DateTime ValidatedAt { get; set; }
    public string ValidatedBy { get; set; } = string.Empty;
}

public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? AttemptedValue { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
}

public class EnrichmentResult
{
    public bool IsSuccessful { get; set; }
    public Order EnrichedOrder { get; set; } = new();
    public Dictionary<string, object> EnrichmentData { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public DateTime EnrichedAt { get; set; }
}

