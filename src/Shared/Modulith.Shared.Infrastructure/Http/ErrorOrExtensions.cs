using ErrorOr;
using Microsoft.AspNetCore.Http;

namespace Modulith.Shared.Infrastructure.Http;

public static class ErrorOrExtensions
{
    public static IResult ToProblemDetailsOr<T>(
        this ErrorOr<T> result,
        Func<T, IResult> onSuccess)
    {
        if (!result.IsError)
            return onSuccess(result.Value);

        return result.FirstError.Type switch
        {
            ErrorType.NotFound => Results.Problem(
                title: "Not Found",
                detail: result.FirstError.Description,
                statusCode: StatusCodes.Status404NotFound),
            ErrorType.Conflict => Results.Problem(
                title: "Conflict",
                detail: result.FirstError.Description,
                statusCode: StatusCodes.Status409Conflict),
            ErrorType.Unauthorized => Results.Problem(
                title: "Unauthorized",
                detail: result.FirstError.Description,
                statusCode: StatusCodes.Status401Unauthorized),
            ErrorType.Validation => Results.ValidationProblem(
                result.Errors
                    .Where(e => e.Type == ErrorType.Validation)
                    .ToDictionary(e => e.Code, e => new[] { e.Description })),
            _ => Results.Problem(
                title: "Internal Server Error",
                detail: result.FirstError.Description,
                statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}
