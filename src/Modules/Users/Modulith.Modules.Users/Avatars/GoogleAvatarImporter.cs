using Microsoft.Extensions.Logging;

namespace Modulith.Modules.Users.Avatars;

public sealed partial class GoogleAvatarImporter(
    HttpClient http,
    IAvatarImageInspector validator,
    IUserAvatarStorage avatarStorage,
    ILogger<GoogleAvatarImporter> logger) : IGoogleAvatarImporter
{
    public async Task<StoredAvatar?> ImportAsync(string? pictureUrl, CancellationToken ct)
    {
        if (!IsAllowedGoogleAvatarUri(pictureUrl, out var uri))
        {
            LogRejectedUrl(logger);
            return null;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(AvatarConstants.GoogleImportTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                LogHttpFailure(logger, (int)response.StatusCode);
                return null;
            }

            if (response.Content.Headers.ContentLength is > AvatarConstants.MaxSizeBytes)
            {
                LogContentLengthExceeded(logger);
                return null;
            }

            await using var remote = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            await using var buffer = new MemoryStream();
            await CopyWithLimitAsync(remote, buffer, AvatarConstants.MaxSizeBytes, timeoutCts.Token);
            buffer.Position = 0;

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var validation = await validator.ValidateAsync(buffer, contentType, buffer.Length, timeoutCts.Token);
            if (validation.IsError)
            {
                LogValidationFailed(logger);
                return null;
            }

            return await avatarStorage.StoreAsync(buffer, validation.Value, timeoutCts.Token);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            LogTimeout(logger, ex);
            return null;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            LogImportFailed(logger, ex);
            return null;
        }
    }

    private static bool IsAllowedGoogleAvatarUri(string? pictureUrl, out Uri uri)
    {
        if (!Uri.TryCreate(pictureUrl, UriKind.Absolute, out uri!) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) ||
            uri.Port != 443 ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        return string.Equals(uri.Host, "googleusercontent.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".googleusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CopyWithLimitAsync(Stream source, Stream destination, long maxBytes, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, ct);
            if (read == 0)
            {
                return;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new InvalidDataException("Avatar image exceeded the allowed download size.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Rejected Google avatar URL because it did not match the expected googleusercontent.com HTTPS host allow-list.")]
    private static partial void LogRejectedUrl(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Google avatar import failed with HTTP status {StatusCode}.")]
    private static partial void LogHttpFailure(ILogger logger, int statusCode);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Google avatar import rejected because declared content length exceeded the avatar limit.")]
    private static partial void LogContentLengthExceeded(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Google avatar import rejected because the downloaded image failed avatar validation.")]
    private static partial void LogValidationFailed(ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Google avatar import timed out.")]
    private static partial void LogTimeout(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Google avatar import failed.")]
    private static partial void LogImportFailed(ILogger logger, Exception exception);
}
