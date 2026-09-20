using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GSAnalytics.Tests;

/// <summary>
/// The single most important security property of this API: one business's authenticated user
/// must never be able to read, modify, or reference (e.g. as a SaleItem's ProductId) another
/// business's data, no matter what ids they put in the request.
/// </summary>
public class BusinessIsolationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, Guid UserId, string Email, string DisplayName, Guid BusinessId, string BusinessName);
    private record CustomerResponse(Guid Id, string Name, string? Segment, string? Email, string? Phone, DateTime CreatedAtUtc);
    private record ProductResponse(Guid Id, string Name, string? Category, DateTime CreatedAtUtc);

    private async Task<(HttpClient Client, AuthResponse Auth)> RegisterNewBusinessAsync()
    {
        var client = factory.CreateClient();
        var email = $"iso-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "TestPass123!",
            DisplayName = "Isolation Test",
            BusinessName = $"Business {Guid.NewGuid():N}"
        });
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, auth);
    }

    [Fact]
    public async Task CustomerCreatedByBusinessA_IsInvisibleTo_BusinessB()
    {
        var (clientA, _) = await RegisterNewBusinessAsync();
        var (clientB, _) = await RegisterNewBusinessAsync();

        var createResponse = await clientA.PostAsJsonAsync("/api/customers", new { Name = "Business A's Customer" });
        var customer = await createResponse.Content.ReadFromJsonAsync<CustomerResponse>();

        var getFromB = await clientB.GetAsync($"/api/customers/{customer!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getFromB.StatusCode);

        var listFromB = await clientB.GetFromJsonAsync<List<CustomerResponse>>("/api/customers");
        Assert.DoesNotContain(listFromB!, c => c.Id == customer.Id);
    }

    [Fact]
    public async Task BusinessB_CannotDeleteOrUpdate_BusinessAsCustomer()
    {
        var (clientA, _) = await RegisterNewBusinessAsync();
        var (clientB, _) = await RegisterNewBusinessAsync();

        var createResponse = await clientA.PostAsJsonAsync("/api/customers", new { Name = "Protected Customer" });
        var customer = await createResponse.Content.ReadFromJsonAsync<CustomerResponse>();

        var updateFromB = await clientB.PutAsJsonAsync($"/api/customers/{customer!.Id}", new { Name = "Hijacked" });
        Assert.Equal(HttpStatusCode.NotFound, updateFromB.StatusCode);

        var deleteFromB = await clientB.DeleteAsync($"/api/customers/{customer.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteFromB.StatusCode);

        var stillThereForA = await clientA.GetAsync($"/api/customers/{customer.Id}");
        Assert.Equal(HttpStatusCode.OK, stillThereForA.StatusCode);
    }

    [Fact]
    public async Task Sale_CannotReference_AnotherBusinesssCustomerOrProduct()
    {
        var (clientA, _) = await RegisterNewBusinessAsync();
        var (clientB, _) = await RegisterNewBusinessAsync();

        var customerAResponse = await clientA.PostAsJsonAsync("/api/customers", new { Name = "A's Customer" });
        var customerA = await customerAResponse.Content.ReadFromJsonAsync<CustomerResponse>();

        var productBResponse = await clientB.PostAsJsonAsync("/api/products", new { Name = "B's Product" });
        var productB = await productBResponse.Content.ReadFromJsonAsync<ProductResponse>();

        // B tries to create a sale against A's customer.
        var saleAgainstForeignCustomer = await clientB.PostAsJsonAsync("/api/sales", new
        {
            CustomerId = customerA!.Id,
            SaleDate = "2026-01-01",
            Items = new[] { new { ProductId = productB!.Id, Quantity = 1, UnitPrice = 10.00m } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, saleAgainstForeignCustomer.StatusCode);

        var customerBResponse = await clientB.PostAsJsonAsync("/api/customers", new { Name = "B's Customer" });
        var customerB = await customerBResponse.Content.ReadFromJsonAsync<CustomerResponse>();

        var productAResponse = await clientA.PostAsJsonAsync("/api/products", new { Name = "A's Product" });
        var productA = await productAResponse.Content.ReadFromJsonAsync<ProductResponse>();

        // B tries to create a sale (against its own customer) referencing A's product.
        var saleAgainstForeignProduct = await clientB.PostAsJsonAsync("/api/sales", new
        {
            CustomerId = customerB!.Id,
            SaleDate = "2026-01-01",
            Items = new[] { new { ProductId = productA!.Id, Quantity = 1, UnitPrice = 10.00m } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, saleAgainstForeignProduct.StatusCode);
    }
}
