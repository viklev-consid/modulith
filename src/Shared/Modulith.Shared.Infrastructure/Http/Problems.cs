using System.Diagnostics;
using ErrorOr;
using Microsoft.AspNetCore.Http;

namespace Modulith.Shared.Infrastructure.Http;

public static class Problems
{
    public static IResult ToProblemResult<T>(this ErrorOr<T> result, Func<T, IResult> onSuccess) =>
        result.Match(onSuccess, FromErrors);

    public static IResult FromErrors(IList<Error> errors)
    {
        if (errors.Any(e => e.Type == ErrorType.Validation))
        {
            var validationErrors = errors
                .Where(e => e.Type == ErrorType.Validation)
                .GroupBy(e => e.Code, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray(), StringComparer.Ordinal);

            return Results.ValidationProblem(
                validationErrors,
                type: "https://tools.ietf.org/html/rfc4918#section-11.2",
                extensions: Trace());
        }

        var first = errors[0];
        var extensions = WithCode(first.Code);

        return first.Type switch
        {
            ErrorType.NotFound => Results.Problem(
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                title: first.Description,
                statusCode: StatusCodes.Status404NotFound,
                extensions: extensions),
            ErrorType.Conflict => Results.Problem(
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                title: first.Description,
                statusCode: StatusCodes.Status409Conflict,
                extensions: extensions),
            ErrorType.Unauthorized => Results.Problem(
                type: "https://tools.ietf.org/html/rfc7235#section-3.1",
                title: first.Description,
                statusCode: StatusCodes.Status401Unauthorized,
                extensions: extensions),
            ErrorType.Forbidden => Results.Problem(
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                title: first.Description,
                statusCode: StatusCodes.Status403Forbidden,
                extensions: extensions),
            _ => Results.Problem(
                type: "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                title: "An unexpected error occurred.",
                statusCode: StatusCodes.Status500InternalServerError,
                extensions: extensions),
        };
    }

    private static Dictionary<string, object?> WithCode(string code) =>
        new(StringComparer.Ordinal)
        {
            ["errorCode"] = code,
            ["traceId"] = Activity.Current?.TraceId.ToString()
        };

    private static Dictionary<string, object?> Trace() =>
        new(StringComparer.Ordinal) { ["traceId"] = Activity.Current?.TraceId.ToString() };
}
