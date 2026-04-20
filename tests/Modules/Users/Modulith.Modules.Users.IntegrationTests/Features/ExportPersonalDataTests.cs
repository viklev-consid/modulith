using System.Net;
using System.Net.Http.Json;
using Modulith.Modules.Users.Features.ExportPersonalData;
using Modulith.Modules.Users.Features.Register;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersGdpr")]
[Trait("Category", "Integration")]
public sealed class ExportPersonalDataTests(GdprApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anonymous = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExportPersonalData_Authenticated_ReturnsExports()
    {
        var registerResp = await (await _anonymous.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice")))
            .Content.ReadFromJsonAsync<RegisterResponse>();

        var client = fixture.CreateAuthenticatedClient(
            registerResp!.UserId, "alice@example.com", "Alice");

        var response = await client.GetAsync("/v1/users/me/personal-data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExportPersonalDataResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.Exports);
        var usersExport = body.Exports.FirstOrDefault(e => e.ModuleName == "Users");
        Assert.NotNull(usersExport);
        Assert.Equal(registerResp.UserId, usersExport.UserId);
        Assert.True(usersExport.Data.ContainsKey("email"));
    }

    [Fact]
    public async Task ExportPersonalData_Unauthenticated_Returns401()
    {
        var response = await _anonymous.GetAsync("/v1/users/me/personal-data");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
