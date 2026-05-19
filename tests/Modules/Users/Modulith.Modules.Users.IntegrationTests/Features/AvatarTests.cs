using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.GetCurrentUser;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.UpdateAvatar;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Infrastructure.Blobs;
using Wolverine.Tracking;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersGdpr")]
[Trait("Category", "Integration")]
public sealed class AvatarTests(GdprApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient anonymous = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UpdateAvatar_WithValidImage_StoresAvatarAndReturnsUrl()
    {
        var (userId, client) = await RegisterAuthenticatedClientAsync("avatar@example.com", "Avatar User");

        var response = await client.PutAsync("/v1/users/me/avatar", CreateAvatarContent(CreateImageBytes(128, MagickFormat.Png), "image/png", "avatar.png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UpdateAvatarResponse>();
        Assert.NotNull(body);
        Assert.StartsWith($"/v1/users/{userId}/avatar?v=", body.Url, StringComparison.Ordinal);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == new UserId(userId));
        Assert.True(user.HasAvatar);
        Assert.Equal("image/png", user.AvatarContentType);
        Assert.NotNull(user.AvatarUpdatedAt);
    }

    [Fact]
    public async Task UpdateAvatar_WithNonSquareImage_Returns400()
    {
        var (_, client) = await RegisterAuthenticatedClientAsync("nonsquare@example.com", "Avatar User");

        var response = await client.PutAsync("/v1/users/me/avatar", CreateAvatarContent(CreateImageBytes(128, 256, MagickFormat.Png), "image/png", "avatar.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAvatar_WithUnsupportedContentType_Returns400()
    {
        var (_, client) = await RegisterAuthenticatedClientAsync("unsupported@example.com", "Avatar User");

        var response = await client.PutAsync("/v1/users/me/avatar", CreateAvatarContent(CreateImageBytes(128, MagickFormat.Png), "image/gif", "avatar.gif"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetUserAvatar_AllowsOwner()
    {
        var (userId, client) = await RegisterAuthenticatedClientAsync("owner@example.com", "Avatar User");
        var bytes = CreateImageBytes(128, MagickFormat.Png);
        await client.PutAsync("/v1/users/me/avatar", CreateAvatarContent(bytes, "image/png", "avatar.png"));

        var response = await client.GetAsync($"/v1/users/{userId}/avatar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GetUserAvatar_WithMatchingETag_Returns304WithoutBody()
    {
        var (userId, client) = await RegisterAuthenticatedClientAsync("etag@example.com", "Avatar User");
        await client.PutAsync("/v1/users/me/avatar", CreateAvatarContent(CreateImageBytes(128, MagickFormat.Png), "image/png", "avatar.png"));
        var first = await client.GetAsync($"/v1/users/{userId}/avatar");
        var etag = first.Headers.ETag?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(etag));

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/users/{userId}/avatar");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GetUserAvatar_AllowsAdminForAnotherUser()
    {
        var (userId, owner) = await RegisterAuthenticatedClientAsync("target@example.com", "Target User");
        await owner.PutAsync("/v1/users/me/avatar", CreateAvatarContent(CreateImageBytes(128, MagickFormat.WebP), "image/webp", "avatar.webp"));

        var admin = fixture.CreateAuthenticatedClient(Guid.NewGuid(), "admin@example.com", "Admin", role: "admin");

        var response = await admin.GetAsync($"/v1/users/{userId}/avatar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetUserAvatar_ForAnotherUserWithoutAdminRole_Returns403()
    {
        var (userId, owner) = await RegisterAuthenticatedClientAsync("private@example.com", "Private User");
        await owner.PutAsync("/v1/users/me/avatar", CreateAvatarContent(CreateImageBytes(128, MagickFormat.Png), "image/png", "avatar.png"));
        var other = fixture.CreateAuthenticatedClient(Guid.NewGuid(), "other@example.com", "Other");

        var response = await other.GetAsync($"/v1/users/{userId}/avatar");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAvatar_ClearsMetadataAndDeletesBlob()
    {
        var (userId, client) = await RegisterAuthenticatedClientAsync("delete-avatar@example.com", "Avatar User");
        await client.PutAsync("/v1/users/me/avatar", CreateAvatarContent(CreateImageBytes(128, MagickFormat.Png), "image/png", "avatar.png"));

        string? container;
        string? key;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == new UserId(userId));
            container = user.AvatarContainer;
            key = user.AvatarKey;
        }

        var response = await client.DeleteAsync("/v1/users/me/avatar");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == new UserId(userId));
            Assert.False(user.HasAvatar);

            var blobStore = scope.ServiceProvider.GetRequiredService<IBlobStore>();
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                blobStore.GetAsync(new BlobRef(container!, key!), CancellationToken.None));
        }
    }

    [Fact]
    public async Task GetCurrentUser_WhenAvatarExists_ReturnsAvatar()
    {
        var (userId, client) = await RegisterAuthenticatedClientAsync("me-avatar@example.com", "Avatar User");
        await client.PutAsync("/v1/users/me/avatar", CreateAvatarContent(CreateImageBytes(128, MagickFormat.Png), "image/png", "avatar.png"));

        var response = await client.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.Avatar);
        Assert.StartsWith($"/v1/users/{userId}/avatar?v=", body.Avatar.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AvatarChanges_AreAudited()
    {
        var (userId, client) = await RegisterAuthenticatedClientAsync("avatar-audit@example.com", "Avatar User");

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .WaitForMessageToBeReceivedAt<UserAvatarUpdatedV1>(fixture.ApplicationHost)
            .ExecuteAndWaitAsync(_ =>
                client.PutAsync("/v1/users/me/avatar", CreateAvatarContent(CreateImageBytes(128, MagickFormat.Png), "image/png", "avatar.png")));

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .WaitForMessageToBeReceivedAt<UserAvatarRemovedV1>(fixture.ApplicationHost)
            .ExecuteAndWaitAsync(_ => client.DeleteAsync("/v1/users/me/avatar"));

        using var scope = fixture.Services.CreateScope();
        var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var entries = await auditDb.AuditEntries.ToListAsync();
        Assert.Contains(entries, e => string.Equals(e.EventType, "user.avatar_updated", StringComparison.Ordinal) && e.ActorId == userId && e.ResourceId == userId);
        Assert.Contains(entries, e => string.Equals(e.EventType, "user.avatar_removed", StringComparison.Ordinal) && e.ActorId == userId && e.ResourceId == userId);
    }

    private async Task<(Guid UserId, HttpClient Client)> RegisterAuthenticatedClientAsync(string email, string displayName)
    {
        var register = await anonymous.PostAsJsonAsync("/v1/users/register", new RegisterRequest(email, "Password1!", displayName));
        register.EnsureSuccessStatusCode();
        var body = await register.Content.ReadFromJsonAsync<RegisterResponse>();
        return (body!.UserId, fixture.CreateAuthenticatedClient(body.UserId, email, displayName));
    }

    private static MultipartFormDataContent CreateAvatarContent(byte[] bytes, string contentType, string fileName)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "avatar", fileName);
        return content;
    }

    private static byte[] CreateImageBytes(int size, MagickFormat format) =>
        CreateImageBytes(size, size, format);

    private static byte[] CreateImageBytes(int width, int height, MagickFormat format)
    {
        using var image = new MagickImage(MagickColors.CornflowerBlue, (uint)width, (uint)height);
        image.Format = format;
        return image.ToByteArray();
    }

}
