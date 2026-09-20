using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GSAnalytics.Tests;

public class ImportsControllerTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, Guid UserId, string Email, string DisplayName, Guid BusinessId, string BusinessName);
    private record CommitRowBody(string CustomerName, string ProductName, string SaleDate, string? City, string Quantity, string UnitPrice);
    private record CommitBody(string FileName, string RawContent, Dictionary<string, string> ColumnMapping, List<CommitRowBody> Rows);
    private record ImportErrorResponse(int Row, string Reason);
    private record CommitResponse(Guid ImportId, bool IsDuplicateOfEarlierImport, int TotalRows, int SucceededRows, int FailedRows, List<ImportErrorResponse> Errors);
    private record CustomerResponse(Guid Id, string Name, string? Segment, string? Email, string? Phone, DateTime CreatedAtUtc);
    private record ProductResponse(Guid Id, string Name, string? Category, DateTime CreatedAtUtc);

    private async Task<HttpClient> NewAuthedClientAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = $"import-{Guid.NewGuid():N}@example.com",
            Password = "TestPass123!",
            DisplayName = "Import Test",
            BusinessName = "Import Test Co"
        });
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Commit_WithNewCustomerAndProductNames_CreatesThemAndTheSale()
    {
        var client = await NewAuthedClientAsync();

        var body = new CommitBody(
            "orders.csv",
            "Customer,Product,Date,City,Qty,Price\nBrand New Customer,Brand New Product,2026-03-01,Testville,2,15.50\n",
            new Dictionary<string, string> { ["Customer"] = "customerName", ["Product"] = "productName" },
            [new CommitRowBody("Brand New Customer", "Brand New Product", "2026-03-01", "Testville", "2", "15.50")]);

        var response = await client.PostAsJsonAsync("/api/imports/commit", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CommitResponse>();
        Assert.Equal(1, result!.SucceededRows);
        Assert.Equal(0, result.FailedRows);
        Assert.False(result.IsDuplicateOfEarlierImport);

        var customers = await client.GetFromJsonAsync<List<CustomerResponse>>("/api/customers");
        Assert.Contains(customers!, c => c.Name == "Brand New Customer");

        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products");
        Assert.Contains(products!, p => p.Name == "Brand New Product");
    }

    [Fact]
    public async Task Commit_WithInvalidRow_ReportsErrorButKeepsValidRows()
    {
        var client = await NewAuthedClientAsync();

        var body = new CommitBody(
            "mixed.csv",
            "raw content",
            [],
            [
                new CommitRowBody("Good Customer", "Good Product", "2026-03-01", null, "1", "10.00"),
                new CommitRowBody("Bad Row Customer", "Bad Row Product", "not-a-date", null, "1", "10.00")
            ]);

        var response = await client.PostAsJsonAsync("/api/imports/commit", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CommitResponse>();
        Assert.Equal(2, result!.TotalRows);
        Assert.Equal(1, result.SucceededRows);
        Assert.Equal(1, result.FailedRows);
        Assert.Single(result.Errors);
        Assert.Equal(1, result.Errors[0].Row);
    }

    [Fact]
    public async Task Commit_SameFileTwice_IsFlaggedAsDuplicateButStillImports()
    {
        var client = await NewAuthedClientAsync();

        var body = new CommitBody(
            "repeat.csv",
            "identical content for hashing",
            [],
            [new CommitRowBody("Customer A", "Product A", "2026-03-01", null, "1", "10.00")]);

        var first = await (await client.PostAsJsonAsync("/api/imports/commit", body)).Content.ReadFromJsonAsync<CommitResponse>();
        Assert.False(first!.IsDuplicateOfEarlierImport);

        var second = await (await client.PostAsJsonAsync("/api/imports/commit", body)).Content.ReadFromJsonAsync<CommitResponse>();
        Assert.True(second!.IsDuplicateOfEarlierImport);
        Assert.Equal(1, second.SucceededRows); // warned, not blocked
    }
}
