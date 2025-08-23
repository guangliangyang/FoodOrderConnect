using FluentAssertions;
using Xunit;

namespace BidOne.ExternalOrderApi.Tests;

public class BasicTests
{
    [Fact]
    public void BasicTest_ShouldPass()
    {
        // Arrange
        var expected = 42;
        
        // Act
        var result = 40 + 2;
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(2, 3, 5)]
    [InlineData(10, 15, 25)]
    public void AdditionTest_WithValidInputs_ReturnsCorrectSum(int a, int b, int expected)
    {
        // Act
        var result = a + b;
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void StringTest_ShouldWorkCorrectly()
    {
        // Arrange
        var input = "Hello World";
        
        // Act
        var result = input.ToUpperInvariant();
        
        // Assert
        result.Should().Be("HELLO WORLD");
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(11);
    }
}