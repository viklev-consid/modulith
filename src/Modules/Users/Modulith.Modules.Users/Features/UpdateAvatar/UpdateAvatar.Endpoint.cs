using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Users.Avatars;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.UpdateAvatar;

internal static class UpdateAvatarEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut(UsersRoutes.MyAvatar,
            async (
                HttpRequest request,
                ICurrentUser currentUser,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                if (!Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                if (!request.HasFormContentType)
                {
                    return Results.ValidationProblem(
                        new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            ["avatar"] = ["Avatar upload must be multipart/form-data."]
                        },
                        statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                var form = await request.ReadFormAsync(ct);
                var file = form.Files.GetFile("avatar");
                if (file is null)
                {
                    return Results.ValidationProblem(
                        new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            ["avatar"] = ["Avatar file is required."]
                        },
                        statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                if (file.Length > AvatarConstants.MaxSizeBytes)
                {
                    return Results.ValidationProblem(
                        new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            ["Users.Avatar.TooLarge"] = ["Avatar image cannot exceed 1 MB."]
                        },
                        statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                var buffer = new byte[file.Length];
                await using var stream = file.OpenReadStream();
                await stream.ReadExactlyAsync(buffer, ct);

                var command = new UpdateAvatarCommand(userId, buffer, file.ContentType, file.FileName);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<UpdateAvatarResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("UpdateAvatar")
        .WithSummary("Upload or replace the authenticated user's avatar.")
        .WithMetadata(
            new RequestSizeLimitAttribute(AvatarConstants.MaxMultipartBodyBytes),
            new RequestFormLimitsAttribute { MultipartBodyLengthLimit = AvatarConstants.MaxMultipartBodyBytes })
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<UpdateAvatarResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .RequireRateLimiting("write")
        .DisableAntiforgery();
}
