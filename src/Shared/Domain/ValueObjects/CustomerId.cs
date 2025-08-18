namespace BidOne.Shared.Domain.ValueObjects;

public sealed class CustomerId : ValueObject
{
    public string Value { get; }

    private CustomerId(string value)
    {
        Value = value;
    }

    public static CustomerId Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Customer ID cannot be null or empty", nameof(value));

        return new CustomerId(value);
    }

    public static implicit operator string(CustomerId customerId) => customerId.Value;
    public static implicit operator CustomerId(string value) => Create(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}