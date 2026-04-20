using System.Net;
using System.Net.Http.Json;
using Modulith.Modules.Catalog.Features.CreateProduct;

namespace Modulith.Modules.Catalog.IntegrationTests.Features;

[Collection("CatalogModule")]
[Trait("Category", "Integration")]
public sealed class CreateProductTests(CatalogApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anonClient = fixture.CreateAnonymousClient();
    private readonly HttpClient _authedClient = fixture.CreateAuthenticatedClient(
        Guid.NewGuid(), "admin@example.com", "Admin");

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateProduct_WithValidRequest_Returns201()
    {
        var request = new CreateProductRequest("TEST-001", "Test Widget", 9.99m, "USD");

        var response = await _authedClient.PostAsJsonAsync("/v1/catalog/products", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateProductResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.ProductId);
        Assert.Equal("TEST-001", body.Sku);
        Assert.Equal("Test Widget", body.Name);
        Assert.Equal(9.99m, body.Price);
        Assert.Equal("USD", body.Currency);
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateSku_Returns409()
    {
        var request = new CreateProductRequest("TEST-001", "Test Widget", 9.99m, "USD");
        await _authedClient.PostAsJsonAsync("/v1/catalog/products", request);

        var response = await _authedClient.PostAsJsonAsync("/v1/catalog/products", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithEmptySku_Returns422()
    {
        var request = new CreateProductRequest("", "Test Widget", 9.99m, "USD");

        var response = await _authedClient.PostAsJsonAsync("/v1/catalog/products", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithNegativePrice_Returns422()
    {
        var request = new CreateProductRequest("TEST-002", "Widget", -1m, "USD");

        var response = await _authedClient.PostAsJsonAsync("/v1/catalog/products", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_Unauthenticated_Returns401()
    {
        var request = new CreateProductRequest("TEST-001", "Test Widget", 9.99m, "USD");

        var response = await _anonClient.PostAsJsonAsync("/v1/catalog/products", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
