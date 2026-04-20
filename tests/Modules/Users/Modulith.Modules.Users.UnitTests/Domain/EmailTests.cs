using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class EmailTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("USER@EXAMPLE.COM")]
    [InlineData("user+tag@sub.domain.com")]
    public void Create_WithValidEmail_Succeeds(string input)
    {
        var result = Email.Create(input);

        Assert.False(result.IsError);
        Assert.Equal(input.Trim().ToLowerInvariant(), result.Value.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notanemail")]
    [InlineData("@nodomain")]
    [InlineData("noat")]
    public void Create_WithInvalidEmail_ReturnsValidationError(string input)
    {
        var result = Email.Create(input);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void Create_NormalizesEmailToLowercase()
    {
        var result = Email.Create("ALICE@EXAMPLE.COM");

        Assert.False(result.IsError);
        Assert.Equal("alice@example.com", result.Value.Value);
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var result = Email.Create("  alice@example.com  ");

        Assert.False(result.IsError);
        Assert.Equal("alice@example.com", result.Value.Value);
    }

    [Fact]
    public void Equality_IsByValue()
    {
        var a = Email.Create("alice@example.com").Value;
        var b = Email.Create("alice@example.com").Value;

        Assert.Equal(a, b);
    }
}
