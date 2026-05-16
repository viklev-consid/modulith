using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Modulith.Modules.Users.Features.Login;

namespace Modulith.Api.Infrastructure.OpenApi;

internal sealed class LoginResponseStatusSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.JsonPropertyInfo?.DeclaringType != typeof(LoginResponse) ||
            !string.Equals(context.JsonPropertyInfo?.Name, "status", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        schema.Type = JsonSchemaType.String;
        schema.Enum =
        [
            JsonValue.Create(LoginResponseStatus.Authenticated)!,
            JsonValue.Create(LoginResponseStatus.TwoFactorRequired)!,
        ];

        return Task.CompletedTask;
    }
}
