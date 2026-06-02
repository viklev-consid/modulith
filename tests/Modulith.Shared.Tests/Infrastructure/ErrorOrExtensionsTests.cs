using System.Text.Json;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Shared.Infrastructure.Http;

namespace Modulith.Shared.Tests.Infrastructure;

[Trait("Category", "Unit")]
public sealed class ErrorOrExtensionsTests
{
    [Fact]
    public async Task ToProblemDetailsOr_WithDuplicateValidationCodes_GroupsDescriptions()
    {
        ErrorOr<string> result = new[]
        {
            Error.Validation("name", "Name is required."),
            Error.Validation("name", "Name is too long."),
        };
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ToProblemDetailsOr(Results.Ok).ExecuteAsync(httpContext);

        httpContext.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);
        var errors = body.GetProperty("errors").GetProperty("name");
        Assert.Equal(2, errors.GetArrayLength());
    }
}
