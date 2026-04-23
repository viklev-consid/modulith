using System.Net;
using System.Net.Http.Json;
using Modulith.Modules.Catalog.Features.CreateProduct;
using Modulith.Modules.Catalog.Features.GetProductById;

namespace Modulith.Modules.Catalog.IntegrationTests.Features;

[Collection("CatalogModule")]
[Trait("Category", "Integration")]
public sealed class GetProductByIdTests(CatalogApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anonClient = fixture.CreateAnonymousClient();
    private readonly HttpClient _authedClient = fixture.CreateAuthenticatedClient(
        Guid.NewGuid(), "admin@example.com", "Admin", "admin");

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetProductById_WithExistingProduct_Returns200()
    {
        var createResponse = await _authedClient.PostAsJsonAsync("/v1/catalog/products",
            new CreateProductRequest("WIDGET-001", "Widget One", 9.99m, "USD"));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateProductResponse>();

        var response = await _anonClient.GetAsync($"/v1/catalog/products/{created!.ProductId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetProductByIdResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.ProductId, body.Id);
        Assert.Equal("WIDGET-001", body.Sku);
        Assert.Equal("Widget One", body.Name);
    }

    [Fact]
    public async Task GetProductById_WithUnknownId_Returns404()
    {
        var response = await _anonClient.GetAsync($"/v1/catalog/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProductById_IsPublic_NoAuthRequired()
    {
        var createResponse = await _authedClient.PostAsJsonAsync("/v1/catalog/products",
            new CreateProductRequest("WIDGET-001", "Widget One", 9.99m, "USD"));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateProductResponse>();

        var response = await _anonClient.GetAsync($"/v1/catalog/products/{created!.ProductId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
