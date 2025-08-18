namespace BidOne.Shared.Domain.ValueObjects;

public sealed class OrderId : ValueObject
{
    public string Value { get; }

    private OrderId(string value)
    {
        Value = value;
    }

    public static OrderId Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Order ID cannot be null or empty", nameof(value));

        return new OrderId(value);
    }

    public static OrderId CreateNew()
    {
        return new OrderId($"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}");
    }

    public static implicit operator string(OrderId orderId) => orderId.Value;
    public static implicit operator OrderId(string value) => Create(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}