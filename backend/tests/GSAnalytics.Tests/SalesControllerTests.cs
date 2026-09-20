using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GSAnalytics.Tests;

public class SalesControllerTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, Guid UserId, string Email, string DisplayName, Guid BusinessId, string BusinessName);
    private record CustomerResponse(Guid Id, string Name, string? Segment, string? Email, string? Phone, DateTime CreatedAtUtc);
    private record ProductResponse(Guid Id, string Name, string? Category, DateTime CreatedAtUtc);
    private record SaleItemResponse(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
    private record SaleResponse(Guid Id, Guid CustomerId, string CustomerName, DateOnly SaleDate, string? City, decimal Total, List<SaleItemResponse> Items);

    private async Task<HttpClient> NewAuthedClientAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = $"sales-{Guid.NewGuid():N}@example.com",
            Password = "TestPass123!",
            DisplayName = "Sales Test",
            BusinessName = "Sales Test Co"
        });
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    /// <summary>
    /// Regression test: PUT-ing a single-item sale with a replaced item list used to throw a
    /// DbUpdateConcurrencyException (EF treated the new item as Modified instead of Added), and
    /// once fixed, a naive tracked reload duplicated the line item in the response. Both are
    /// covered here.
    /// </summary>
    [Fact]
    public async Task UpdateSale_ReplacingItems_ReturnsExactlyOneCorrectItem_NoDuplication()
    {
        var client = await NewAuthedClientAsync();

        var customer = await (await client.PostAsJsonAsync("/api/customers", new { Name = "Test Customer" }))
            .Content.ReadFromJsonAsync<CustomerResponse>();
        var productA = await (await client.PostAsJsonAsync("/api/products", new { Name = "Product A" }))
            .Content.ReadFromJsonAsync<ProductResponse>();
        var productB = await (await client.PostAsJsonAsync("/api/products", new { Name = "Product B" }))
            .Content.ReadFromJsonAsync<ProductResponse>();

        var createResponse = await client.PostAsJsonAsync("/api/sales", new
        {
            CustomerId = customer!.Id,
            SaleDate = "2026-01-01",
            City = "Origin City",
            Items = new[] { new { ProductId = productA!.Id, Quantity = 1, UnitPrice = 10.00m } }
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.Single(created!.Items);

        var updateResponse = await client.PutAsJsonAsync($"/api/sales/{created.Id}", new
        {
            CustomerId = customer.Id,
            SaleDate = "2026-01-02",
            City = "New City",
            Items = new[] { new { ProductId = productB!.Id, Quantity = 1, UnitPrice = 99.99m } }
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.Single(updated!.Items);
        Assert.Equal(productB.Id, updated.Items[0].ProductId);
        Assert.Equal(99.99m, updated.Total);
        Assert.Equal("New City", updated.City);

        var refetched = await client.GetFromJsonAsync<SaleResponse>($"/api/sales/{created.Id}");
        Assert.Single(refetched!.Items);
        Assert.Equal(99.99m, refetched.Total);
    }

    [Fact]
    public async Task DeleteCustomer_WithExistingSales_ReturnsConflict()
    {
        var client = await NewAuthedClientAsync();

        var customer = await (await client.PostAsJsonAsync("/api/customers", new { Name = "Has Sales" }))
            .Content.ReadFromJsonAsync<CustomerResponse>();
        var product = await (await client.PostAsJsonAsync("/api/products", new { Name = "Some Product" }))
            .Content.ReadFromJsonAsync<ProductResponse>();

        await client.PostAsJsonAsync("/api/sales", new
        {
            CustomerId = customer!.Id,
            SaleDate = "2026-01-01",
            Items = new[] { new { ProductId = product!.Id, Quantity = 1, UnitPrice = 5.00m } }
        });

        var deleteResponse = await client.DeleteAsync($"/api/customers/{customer.Id}");

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task CreateSale_WithZeroQuantity_ReturnsBadRequest()
    {
        var client = await NewAuthedClientAsync();

        var customer = await (await client.PostAsJsonAsync("/api/customers", new { Name = "Test Customer" }))
            .Content.ReadFromJsonAsync<CustomerResponse>();
        var product = await (await client.PostAsJsonAsync("/api/products", new { Name = "Test Product" }))
            .Content.ReadFromJsonAsync<ProductResponse>();

        var response = await client.PostAsJsonAsync("/api/sales", new
        {
            CustomerId = customer!.Id,
            SaleDate = "2026-01-01",
            Items = new[] { new { ProductId = product!.Id, Quantity = 0, UnitPrice = 5.00m } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
