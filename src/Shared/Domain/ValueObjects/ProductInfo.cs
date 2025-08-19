namespace BidOne.Shared.Domain.ValueObjects;

public sealed class ProductInfo : ValueObject
{
    public string ProductId { get; }
    public string ProductName { get; }
    public string? Category { get; }

    private ProductInfo(string productId, string productName, string? category = null)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Product ID cannot be null or empty", nameof(productId));

        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("Product name cannot be null or empty", nameof(productName));

        ProductId = productId;
        ProductName = productName;
        Category = category;
    }

    public static ProductInfo Create(string productId, string productName, string? category = null)
    {
        return new ProductInfo(productId, productName, category);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ProductId;
        yield return ProductName;
        yield return Category;
    }

    public override string ToString() => $"{ProductName} ({ProductId})";
}
