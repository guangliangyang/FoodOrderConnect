namespace BidOne.Shared.Domain.ValueObjects;

public sealed class Quantity : ValueObject
{
    public int Value { get; }

    private Quantity(int value)
    {
        if (value <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(value));

        Value = value;
    }

    public static Quantity Create(int value)
    {
        return new Quantity(value);
    }

    public Quantity Add(Quantity other)
    {
        return new Quantity(Value + other.Value);
    }

    public Quantity Subtract(Quantity other)
    {
        return new Quantity(Value - other.Value);
    }

    public static implicit operator int(Quantity quantity) => quantity.Value;
    public static implicit operator Quantity(int value) => Create(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
