using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.LoginTwoFactor;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.TwoFactor.ConfirmTotp;
using Modulith.Modules.Users.Features.TwoFactor.DisableTwoFactor;
using Modulith.Modules.Users.Features.TwoFactor.RegenerateRecoveryCodes;
using Modulith.Modules.Users.Features.TwoFactor.SetupTotp;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class TwoFactorTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ConfirmTotp_WithValidCode_EnablesTwoFactorAndReturnsRecoveryCodes()
    {
        var login = await RegisterAndLoginAsync("alice@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(login.AccessToken);

        var setup = await StartSetupAsync(auth);
        var response = await auth.PostAsJsonAsync("/v1/users/me/2fa/totp/confirm",
            new ConfirmTotpRequest(CurrentTotp(setup.Secret)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ConfirmTotpResponse>();
        Assert.NotNull(body);
        Assert.Equal(10, body.RecoveryCodes.Count);

        var enabled = await fixture.QueryDbAsync<UsersDbContext, bool>((db, ct) =>
            db.TwoFactorCredentials.AnyAsync(c => c.ConfirmedAt != null && c.DisabledAt == null, ct));
        Assert.True(enabled);
    }

    [Fact]
    public async Task Login_WhenTwoFactorEnabled_ReturnsChallengeWithoutIssuingRefreshToken()
    {
        var before = await EnableTwoFactorAsync("challenge@example.com");

        var response = await client.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("challenge@example.com", "Password1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.Equal(LoginResponseStatus.TwoFactorRequired, body.Status);
        Assert.NotNull(body.Challenge);
        Assert.NotEmpty(body.Challenge.ChallengeToken);
        Assert.Null(body.Session);

        var activeRefreshTokenCount = await fixture.QueryDbAsync<UsersDbContext, int>((db, ct) =>
            db.RefreshTokens.CountAsync(t => t.UserId == new UserId(before.UserId) && t.RevokedAt == null, ct));
        Assert.Equal(1, activeRefreshTokenCount);
    }

    [Fact]
    public async Task LoginTwoFactor_WithValidTotp_IssuesTokensAndConsumesChallenge()
    {
        var setup = await EnableTwoFactorAsync("complete@example.com");

        var login = await client.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("complete@example.com", "Password1!"));
        var challenge = await login.Content.ReadFromJsonAsync<LoginResponse>();

        var response = await client.PostAsJsonAsync("/v1/users/login/2fa",
            new LoginTwoFactorRequest(challenge!.Challenge!.ChallengeToken, NextTotp(setup.Secret)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginTwoFactorResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.AccessToken);
        Assert.NotEmpty(body.RefreshToken);

        var consumed = await fixture.QueryDbAsync<UsersDbContext, bool>((db, ct) =>
            db.PendingTwoFactorChallenges.AnyAsync(c => c.ConsumedAt != null, ct));
        Assert.True(consumed);
    }

    [Fact]
    public async Task LoginTwoFactor_WithRecoveryCode_ConsumesRecoveryCode()
    {
        var setup = await EnableTwoFactorAsync("recovery@example.com");

        var login = await client.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("recovery@example.com", "Password1!"));
        var challenge = await login.Content.ReadFromJsonAsync<LoginResponse>();

        var response = await client.PostAsJsonAsync("/v1/users/login/2fa",
            new LoginTwoFactorRequest(challenge!.Challenge!.ChallengeToken, setup.RecoveryCodes[0]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var secondLogin = await client.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("recovery@example.com", "Password1!"));
        var secondChallenge = await secondLogin.Content.ReadFromJsonAsync<LoginResponse>();

        var replay = await client.PostAsJsonAsync("/v1/users/login/2fa",
            new LoginTwoFactorRequest(secondChallenge!.Challenge!.ChallengeToken, setup.RecoveryCodes[0]));

        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task LoginTwoFactor_InvalidAttempts_ConsumeChallengeAfterMaxAttempts()
    {
        var setup = await EnableTwoFactorAsync("attempts@example.com");
        var login = await client.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("attempts@example.com", "Password1!"));
        var challenge = await login.Content.ReadFromJsonAsync<LoginResponse>();

        for (var i = 0; i < 5; i++)
        {
            var attempt = await client.PostAsJsonAsync("/v1/users/login/2fa",
                new LoginTwoFactorRequest(challenge!.Challenge!.ChallengeToken, "000000"));

            Assert.Equal(HttpStatusCode.BadRequest, attempt.StatusCode);
        }

        var validAfterLock = await client.PostAsJsonAsync("/v1/users/login/2fa",
            new LoginTwoFactorRequest(challenge!.Challenge!.ChallengeToken, NextTotp(setup.Secret)));

        Assert.Equal(HttpStatusCode.BadRequest, validAfterLock.StatusCode);

        var stored = await fixture.QueryDbAsync<UsersDbContext, (int Attempts, bool Consumed)>((db, ct) =>
            db.PendingTwoFactorChallenges
                .Select(c => new ValueTuple<int, bool>(c.AttemptCount, c.ConsumedAt != null))
                .SingleAsync(ct));

        Assert.Equal(5, stored.Attempts);
        Assert.True(stored.Consumed);
    }

    [Fact]
    public async Task LoginTwoFactor_ReusingTotpStep_Returns400()
    {
        var setup = await EnableTwoFactorAsync("replay@example.com");
        var code = NextTotp(setup.Secret);

        var firstChallenge = await LoginForChallengeAsync("replay@example.com");
        var first = await client.PostAsJsonAsync("/v1/users/login/2fa",
            new LoginTwoFactorRequest(firstChallenge.Challenge!.ChallengeToken, code));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var secondChallenge = await LoginForChallengeAsync("replay@example.com");
        var replay = await client.PostAsJsonAsync("/v1/users/login/2fa",
            new LoginTwoFactorRequest(secondChallenge.Challenge!.ChallengeToken, code));

        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_RequiresCurrentPassword()
    {
        var setup = await EnableTwoFactorAsync("sudo@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(setup.AccessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/2fa/recovery-codes/regenerate",
            new RegenerateRecoveryCodesRequest("WrongPassword1!", NextTotp(setup.Secret)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_InvalidatesPreviousBatch()
    {
        var setup = await EnableTwoFactorAsync("regenerate@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(setup.AccessToken);
        var oldCode = setup.RecoveryCodes[0];

        var response = await auth.PostAsJsonAsync("/v1/users/me/2fa/recovery-codes/regenerate",
            new RegenerateRecoveryCodesRequest("Password1!", NextTotp(setup.Secret)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var regenerated = await response.Content.ReadFromJsonAsync<RegenerateRecoveryCodesResponse>();
        Assert.NotNull(regenerated);
        Assert.Equal(10, regenerated.RecoveryCodes.Count);
        Assert.DoesNotContain(oldCode, regenerated.RecoveryCodes);

        var challenge = await LoginForChallengeAsync("regenerate@example.com");
        var oldCodeLogin = await client.PostAsJsonAsync("/v1/users/login/2fa",
            new LoginTwoFactorRequest(challenge.Challenge!.ChallengeToken, oldCode));

        Assert.Equal(HttpStatusCode.BadRequest, oldCodeLogin.StatusCode);

        var secondChallenge = await LoginForChallengeAsync("regenerate@example.com");
        var newCodeLogin = await client.PostAsJsonAsync("/v1/users/login/2fa",
            new LoginTwoFactorRequest(secondChallenge.Challenge!.ChallengeToken, regenerated.RecoveryCodes[0]));

        Assert.Equal(HttpStatusCode.OK, newCodeLogin.StatusCode);
    }

    [Fact]
    public async Task DisableTwoFactor_WithValidPasswordAndCode_DisablesAndRevokesSessions()
    {
        var setup = await EnableTwoFactorAsync("disable@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(setup.AccessToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/v1/users/me/2fa")
        {
            Content = JsonContent.Create(new DisableTwoFactorRequest("Password1!", NextTotp(setup.Secret))),
        };

        var response = await auth.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await fixture.QueryDbAsync<UsersDbContext, (bool Enabled, int ActiveRefreshTokens)>((db, ct) =>
            db.TwoFactorCredentials
                .Select(c => new ValueTuple<bool, int>(
                    c.ConfirmedAt != null && c.DisabledAt == null,
                    db.RefreshTokens.Count(t => t.UserId == c.UserId && t.RevokedAt == null)))
                .SingleAsync(ct));

        Assert.False(state.Enabled);
        Assert.Equal(0, state.ActiveRefreshTokens);
    }

    [Fact]
    public async Task DisableTwoFactor_WithRecoveryCode_DisablesAndDeletesRecoveryCodes()
    {
        var setup = await EnableTwoFactorAsync("disable-recovery@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(setup.AccessToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/v1/users/me/2fa")
        {
            Content = JsonContent.Create(new DisableTwoFactorRequest("Password1!", setup.RecoveryCodes[0])),
        };

        var response = await auth.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var codeCount = await fixture.QueryDbAsync<UsersDbContext, int>((db, ct) =>
            db.RecoveryCodes.CountAsync(ct));
        Assert.Equal(0, codeCount);
    }

    private async Task<LoginResponse> RegisterAndLoginAsync(string email)
    {
        await client.PostAsJsonAsync("/v1/users/register", new RegisterRequest(email, "Password1!", "Alice"));
        var response = await client.PostAsJsonAsync("/v1/users/login", new LoginRequest(email, "Password1!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private async Task<LoginResponse> LoginForChallengeAsync(string email)
    {
        var login = await client.PostAsJsonAsync("/v1/users/login", new LoginRequest(email, "Password1!"));
        login.EnsureSuccessStatusCode();
        var challenge = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal(LoginResponseStatus.TwoFactorRequired, challenge.Status);
        Assert.NotNull(challenge.Challenge);
        return challenge;
    }

    private static async Task<SetupTotpResponse> StartSetupAsync(HttpClient auth)
    {
        var setupResponse = await auth.PostAsync("/v1/users/me/2fa/totp/setup", content: null);
        setupResponse.EnsureSuccessStatusCode();
        return (await setupResponse.Content.ReadFromJsonAsync<SetupTotpResponse>())!;
    }

    private async Task<EnabledTwoFactor> EnableTwoFactorAsync(string email)
    {
        var login = await RegisterAndLoginAsync(email);
        var auth = fixture.CreateAuthenticatedClientWithToken(login.AccessToken);
        var setup = await StartSetupAsync(auth);
        var confirm = await auth.PostAsJsonAsync("/v1/users/me/2fa/totp/confirm",
            new ConfirmTotpRequest(CurrentTotp(setup.Secret)));
        confirm.EnsureSuccessStatusCode();
        var confirmation = (await confirm.Content.ReadFromJsonAsync<ConfirmTotpResponse>())!;

        return new EnabledTwoFactor(
            login.UserId,
            login.AccessToken,
            setup.Secret,
            confirmation.RecoveryCodes);
    }

    private static string CurrentTotp(string secret) => Totp(secret, DateTimeOffset.UtcNow);

    private static string NextTotp(string secret) => Totp(secret, DateTimeOffset.UtcNow.AddSeconds(30));

    private static string Totp(string secret, DateTimeOffset timestamp)
    {
        var secretBytes = Base32Decode(secret);
        var timeStep = timestamp.ToUnixTimeSeconds() / 30;
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

    private sealed record EnabledTwoFactor(
        Guid UserId,
        string AccessToken,
        string Secret,
        IReadOnlyList<string> RecoveryCodes);
}
