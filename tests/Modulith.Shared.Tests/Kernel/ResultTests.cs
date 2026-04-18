using ErrorOr;

namespace Modulith.Shared.Tests.Kernel;

[Trait("Category", "Unit")]
public sealed class ResultTests
{
    [Fact]
    public void ImplicitConversion_FromValue_IsNotError()
    {
        ErrorOr<int> result = 42;

        Assert.False(result.IsError);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromError_IsError()
    {
        ErrorOr<int> result = Error.Validation("test.invalid", "Value is invalid.");

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void Errors_ContainsAllErrors_WhenMultipleErrorsReturned()
    {
        var errors = new[]
        {
            Error.Validation("field.required", "Field is required."),
            Error.Validation("field.tooLong", "Field is too long."),
        };

        ErrorOr<string> result = errors;

        Assert.True(result.IsError);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Match_InvokesSuccessBranch_WhenNotError()
    {
        ErrorOr<int> result = 10;

        var matched = result.Match(
            value => $"success:{value}",
            errors => $"error:{errors.Count}");

        Assert.Equal("success:10", matched);
    }

    [Fact]
    public void Match_InvokesErrorBranch_WhenIsError()
    {
        ErrorOr<int> result = Error.NotFound("entity.missing", "Not found.");

        var matched = result.Match(
            value => $"success:{value}",
            errors => $"error:{errors.Count}");

        Assert.Equal("error:1", matched);
    }

    [Fact]
    public void ErrorTypes_MapToExpectedDiscriminators()
    {
        Assert.Equal(ErrorType.Validation, Error.Validation("x", "x").Type);
        Assert.Equal(ErrorType.NotFound, Error.NotFound("x", "x").Type);
        Assert.Equal(ErrorType.Conflict, Error.Conflict("x", "x").Type);
        Assert.Equal(ErrorType.Unauthorized, Error.Unauthorized("x", "x").Type);
        Assert.Equal(ErrorType.Failure, Error.Failure("x", "x").Type);
    }
}
