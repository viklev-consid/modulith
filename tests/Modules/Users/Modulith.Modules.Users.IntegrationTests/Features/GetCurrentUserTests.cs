using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Modulith.Modules.Users.Features.GetCurrentUser;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.TwoFactor.ConfirmTotp;
using Modulith.Modules.Users.Features.TwoFactor.SetupTotp;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class GetCurrentUserTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient anonymous = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetCurrentUser_Authenticated_ReturnsProfile()
    {
        var registerResponse = await (await anonymous.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice")))
            .Content.ReadFromJsonAsync<RegisterResponse>();

        var client = fixture.CreateAuthenticatedClient(
            registerResponse!.UserId, "alice@example.com", "Alice");

        var response = await client.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Equal(registerResponse.UserId, body.UserId);
        Assert.Equal("alice@example.com", body.Email);
        Assert.Equal("Alice", body.DisplayName);
        Assert.False(body.TwoFactorEnabled);
    }

    [Fact]
    public async Task GetCurrentUser_WhenTwoFactorEnabled_ReturnsTwoFactorEnabled()
    {
        var registerResponse = await (await anonymous.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("totp-me@example.com", "Password1!", "Totp User")))
            .Content.ReadFromJsonAsync<RegisterResponse>();

        var client = fixture.CreateAuthenticatedClient(
            registerResponse!.UserId, "totp-me@example.com", "Totp User");

        var setupResponse = await client.PostAsync("/v1/users/me/2fa/totp/setup", content: null);
        setupResponse.EnsureSuccessStatusCode();
        var setup = (await setupResponse.Content.ReadFromJsonAsync<SetupTotpResponse>())!;

        var confirmResponse = await client.PostAsJsonAsync(
            "/v1/users/me/2fa/totp/confirm",
            new ConfirmTotpRequest(CurrentTotp(setup.Secret)));
        confirmResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>();
        Assert.NotNull(body);
        Assert.True(body.TwoFactorEnabled);
    }

    [Fact]
    public async Task GetCurrentUser_Unauthenticated_Returns401()
    {
        var response = await anonymous.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string CurrentTotp(string secret)
    {
        var secretBytes = Base32Decode(secret);
        var timeStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        Span<byte> counter = stackalloc byte[8];
        BitConverter.TryWriteBytes(counter, timeStep);
        if (BitConverter.IsLittleEndian)
        {
            counter.Reverse();
        }

#pragma warning disable CA5350
        using var hmac = new HMACSHA1(secretBytes);
#pragma warning restore CA5350
        var hash = hmac.ComputeHash(counter.ToArray());
        var offset = hash[^1] & 0x0f;
        var binary =
            ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var bitBuffer = 0;
        var bitCount = 0;

        foreach (var c in value.Trim().ToUpperInvariant())
        {
            var index = alphabet.IndexOf(c, StringComparison.Ordinal);
            bitBuffer = (bitBuffer << 5) | index;
            bitCount += 5;

            if (bitCount >= 8)
            {
                bytes.Add((byte)((bitBuffer >> (bitCount - 8)) & 0xff));
                bitCount -= 8;
            }
        }

        return bytes.ToArray();
    }
}
