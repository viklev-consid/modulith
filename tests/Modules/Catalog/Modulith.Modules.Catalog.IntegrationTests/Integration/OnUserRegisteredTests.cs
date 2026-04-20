using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Catalog.Domain;
using Modulith.Modules.Catalog.Persistence;

namespace Modulith.Modules.Catalog.IntegrationTests.Integration;

[Collection("CrossModule")]
[Trait("Category", "Integration")]
public sealed class OnUserRegisteredTests(CrossModuleApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisteringUser_CreatesCatalogCustomer()
    {
        // Arrange
        var request = new { Email = "cross-module@example.com", Password = "Password1!", DisplayName = "Cross Module" };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/users/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();

        // Assert — poll until the Wolverine outbox delivers UserRegisteredV1 and creates the Customer
        Customer? customer = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!cts.IsCancellationRequested)
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            customer = await db.Customers.FirstOrDefaultAsync(c => c.UserId == userId, cts.Token);
            if (customer is not null) break;
            await Task.Delay(200, cts.Token);
        }

        Assert.NotNull(customer);
        Assert.Equal("cross-module@example.com", customer.Email);
        Assert.Equal("Cross Module", customer.DisplayName);
        Assert.Equal(userId, customer.UserId);
    }
}
