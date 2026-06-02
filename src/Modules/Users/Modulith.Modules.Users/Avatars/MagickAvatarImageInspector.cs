using ErrorOr;
using ImageMagick;
using Modulith.Modules.Users.Errors;

namespace Modulith.Modules.Users.Avatars;

public sealed class MagickAvatarImageInspector : IAvatarImageInspector
{
    public Task<ErrorOr<AvatarImageInfo>> ValidateAsync(
        Stream content,
        string? declaredContentType,
        long length,
        CancellationToken ct)
    {
        if (length > AvatarConstants.MaxSizeBytes)
        {
            return Task.FromResult<ErrorOr<AvatarImageInfo>>(UsersErrors.AvatarTooLarge);
        }

        if (string.IsNullOrWhiteSpace(declaredContentType) ||
            !AvatarConstants.AllowedContentTypes.Contains(declaredContentType))
        {
            return Task.FromResult<ErrorOr<AvatarImageInfo>>(UsersErrors.AvatarContentTypeUnsupported);
        }

        try
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            var imageInfo = new MagickImageInfo(content);
            var detectedContentType = GetContentType(imageInfo.Format);
            if (detectedContentType is null ||
                !string.Equals(detectedContentType, declaredContentType, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<ErrorOr<AvatarImageInfo>>(UsersErrors.AvatarContentTypeUnsupported);
            }

            var width = checked((int)imageInfo.Width);
            var height = checked((int)imageInfo.Height);
            if (width != height ||
                width < AvatarConstants.MinDimensionPixels ||
                width > AvatarConstants.MaxDimensionPixels)
            {
                return Task.FromResult<ErrorOr<AvatarImageInfo>>(UsersErrors.AvatarDimensionsInvalid);
            }

            if (content.CanSeek)
            {
                content.Position = 0;
            }

            return Task.FromResult<ErrorOr<AvatarImageInfo>>(new AvatarImageInfo(detectedContentType, width, height, length));
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return Task.FromResult<ErrorOr<AvatarImageInfo>>(UsersErrors.AvatarInvalid);
        }
    }

    private static string? GetContentType(MagickFormat format) =>
        format switch
        {
            MagickFormat.Jpeg or MagickFormat.Jpg => "image/jpeg",
            MagickFormat.Png => "image/png",
            MagickFormat.WebP => "image/webp",
            _ => null,
        };
}
