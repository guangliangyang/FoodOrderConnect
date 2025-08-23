using BidOne.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace BidOne.Shared.Tests.Domain.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_WithValidAmount_CreatesMoneyObject()
    {
        // Arrange & Act
        var money = new Money(100.50m);

        // Assert
        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be("USD"); // Default currency
    }

    [Fact]
    public void Constructor_WithAmountAndCurrency_CreatesMoneyObjectWithCurrency()
    {
        // Arrange & Act
        var money = new Money(75.25m, "EUR");

        // Assert
        money.Amount.Should().Be(75.25m);
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Constructor_WithNegativeAmount_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new Money(-10.00m));
        exception.Message.Should().Contain("Amount cannot be negative");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidCurrency_ThrowsArgumentException(string? invalidCurrency)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new Money(100.00m, invalidCurrency!));
        exception.Message.Should().Contain("Currency cannot be null or empty");
    }

    [Fact]
    public void Add_WithSameCurrency_ReturnsSum()
    {
        // Arrange
        var money1 = new Money(50.00m, "USD");
        var money2 = new Money(30.00m, "USD");

        // Act
        var result = money1.Add(money2);

        // Assert
        result.Amount.Should().Be(80.00m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_WithDifferentCurrencies_ThrowsInvalidOperationException()
    {
        // Arrange
        var money1 = new Money(50.00m, "USD");
        var money2 = new Money(30.00m, "EUR");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => money1.Add(money2));
        exception.Message.Should().Contain("Cannot perform operations on different currencies");
    }

    [Fact]
    public void Subtract_WithSameCurrency_ReturnsDifference()
    {
        // Arrange
        var money1 = new Money(50.00m, "USD");
        var money2 = new Money(20.00m, "USD");

        // Act
        var result = money1.Subtract(money2);

        // Assert
        result.Amount.Should().Be(30.00m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Subtract_WithDifferentCurrencies_ThrowsInvalidOperationException()
    {
        // Arrange
        var money1 = new Money(50.00m, "USD");
        var money2 = new Money(20.00m, "EUR");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => money1.Subtract(money2));
        exception.Message.Should().Contain("Cannot perform operations on different currencies");
    }

    [Fact]
    public void Subtract_ResultingInNegativeAmount_ThrowsInvalidOperationException()
    {
        // Arrange
        var money1 = new Money(20.00m, "USD");
        var money2 = new Money(50.00m, "USD");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => money1.Subtract(money2));
        exception.Message.Should().Contain("Subtraction would result in negative amount");
    }

    [Fact]
    public void Multiply_WithPositiveMultiplier_ReturnsProduct()
    {
        // Arrange
        var money = new Money(25.00m, "USD");
        var multiplier = 3;

        // Act
        var result = money.Multiply(multiplier);

        // Assert
        result.Amount.Should().Be(75.00m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Multiply_WithZeroMultiplier_ReturnsZero()
    {
        // Arrange
        var money = new Money(25.00m, "USD");
        var multiplier = 0;

        // Act
        var result = money.Multiply(multiplier);

        // Assert
        result.Amount.Should().Be(0.00m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Multiply_WithNegativeMultiplier_ThrowsArgumentException()
    {
        // Arrange
        var money = new Money(25.00m, "USD");
        var multiplier = -2;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => money.Multiply(multiplier));
        exception.Message.Should().Contain("Multiplier cannot be negative");
    }

    [Fact]
    public void Equals_WithSameAmountAndCurrency_ReturnsTrue()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(100.00m, "USD");

        // Act & Assert
        money1.Equals(money2).Should().BeTrue();
        (money1 == money2).Should().BeTrue();
        (money1 != money2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentAmount_ReturnsFalse()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(150.00m, "USD");

        // Act & Assert
        money1.Equals(money2).Should().BeFalse();
        (money1 == money2).Should().BeFalse();
        (money1 != money2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentCurrency_ReturnsFalse()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(100.00m, "EUR");

        // Act & Assert
        money1.Equals(money2).Should().BeFalse();
        (money1 == money2).Should().BeFalse();
        (money1 != money2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WithSameAmountAndCurrency_ReturnsSameHash()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(100.00m, "USD");

        // Act & Assert
        money1.GetHashCode().Should().Be(money2.GetHashCode());
    }

    [Theory]
    [InlineData(100.00, "USD", "100.00 USD")]
    [InlineData(75.50, "EUR", "75.50 EUR")]
    [InlineData(0.00, "GBP", "0.00 GBP")]
    public void ToString_ReturnsFormattedString(decimal amount, string currency, string expected)
    {
        // Arrange
        var money = new Money(amount, currency);

        // Act
        var result = money.ToString();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CompareTo_WithSmallerAmount_ReturnsPositive()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(50.00m, "USD");

        // Act & Assert
        money1.CompareTo(money2).Should().BePositive();
        (money1 > money2).Should().BeTrue();
        (money1 >= money2).Should().BeTrue();
        (money1 < money2).Should().BeFalse();
        (money1 <= money2).Should().BeFalse();
    }

    [Fact]
    public void CompareTo_WithLargerAmount_ReturnsNegative()
    {
        // Arrange
        var money1 = new Money(50.00m, "USD");
        var money2 = new Money(100.00m, "USD");

        // Act & Assert
        money1.CompareTo(money2).Should().BeNegative();
        (money1 < money2).Should().BeTrue();
        (money1 <= money2).Should().BeTrue();
        (money1 > money2).Should().BeFalse();
        (money1 >= money2).Should().BeFalse();
    }

    [Fact]
    public void CompareTo_WithEqualAmount_ReturnsZero()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(100.00m, "USD");

        // Act & Assert
        money1.CompareTo(money2).Should().Be(0);
        (money1 >= money2).Should().BeTrue();
        (money1 <= money2).Should().BeTrue();
    }

    [Fact]
    public void CompareTo_WithDifferentCurrencies_ThrowsInvalidOperationException()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(100.00m, "EUR");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => money1.CompareTo(money2));
        exception.Message.Should().Contain("Cannot compare amounts in different currencies");
    }

    [Fact]
    public void IsZero_WithZeroAmount_ReturnsTrue()
    {
        // Arrange
        var money = new Money(0.00m, "USD");

        // Act & Assert
        money.IsZero.Should().BeTrue();
    }

    [Fact]
    public void IsZero_WithNonZeroAmount_ReturnsFalse()
    {
        // Arrange
        var money = new Money(0.01m, "USD");

        // Act & Assert
        money.IsZero.Should().BeFalse();
    }
}
