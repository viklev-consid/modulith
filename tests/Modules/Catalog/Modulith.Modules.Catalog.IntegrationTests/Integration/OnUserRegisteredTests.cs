using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Catalog.Domain;
using Modulith.Modules.Catalog.Persistence;
using Wolverine.Tracking;

namespace Modulith.Modules.Catalog.IntegrationTests.Integration;

[Collection("CrossModule")]
[Trait("Category", "Integration")]
public sealed class OnUserRegisteredTests(CrossModuleApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisteringUser_CreatesCatalogCustomer()
    {
        // Arrange
        var request = new { Email = "cross-module@example.com", Password = "Password1!", DisplayName = "Cross Module" };
        HttpResponseMessage? registerResponse = null;

        // Act — TrackActivity waits for all cascading messages to finish before returning
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync((Func<Wolverine.IMessageContext, Task>)(async _ =>
            {
                registerResponse = await client.PostAsJsonAsync("/v1/users/register", request);
            }));

        Assert.Equal(HttpStatusCode.Created, registerResponse!.StatusCode);
        var body = await registerResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();

        // Assert — no polling needed; TrackActivity waited for the subscriber to complete
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.UserId == userId);

        Assert.NotNull(customer);
        Assert.Equal("cross-module@example.com", customer.Email);
        Assert.Equal("Cross Module", customer.DisplayName);
        Assert.Equal(userId, customer.UserId);
    }
}
