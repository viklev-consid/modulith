using System.Net;
using System.Net.Http.Json;
using Modulith.Modules.Catalog.Features.CreateProduct;
using Modulith.Modules.Catalog.Features.ListProducts;

namespace Modulith.Modules.Catalog.IntegrationTests.Features;

[Collection("CatalogModule")]
[Trait("Category", "Integration")]
public sealed class ListProductsTests(CatalogApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anonClient = fixture.CreateAnonymousClient();
    private readonly HttpClient _authedClient = fixture.CreateAuthenticatedClient(
        Guid.NewGuid(), "admin@example.com", "Admin");

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ListProducts_WhenNoProducts_ReturnsEmptyList()
    {
        var response = await _anonClient.GetAsync("/v1/catalog/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ListProductsResponse>();
        Assert.NotNull(body);
        Assert.Empty(body.Products);
    }

    [Fact]
    public async Task ListProducts_AfterCreatingProducts_ReturnsThem()
    {
        await _authedClient.PostAsJsonAsync("/v1/catalog/products",
            new CreateProductRequest("WIDGET-001", "Widget One", 9.99m, "USD"));
        await _authedClient.PostAsJsonAsync("/v1/catalog/products",
            new CreateProductRequest("WIDGET-002", "Widget Two", 19.99m, "USD"));

        var response = await _anonClient.GetAsync("/v1/catalog/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ListProductsResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Products.Count);
    }

    [Fact]
    public async Task ListProducts_IsPublic_NoAuthRequired()
    {
        var response = await _anonClient.GetAsync("/v1/catalog/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
