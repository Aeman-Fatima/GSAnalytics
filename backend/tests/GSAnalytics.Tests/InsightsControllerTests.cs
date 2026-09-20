using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GSAnalytics.Tests;

public class InsightsControllerTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, Guid UserId, string Email, string DisplayName, Guid BusinessId, string BusinessName);
    private record CustomerResponse(Guid Id, string Name, string? Segment, string? Email, string? Phone, DateTime CreatedAtUtc);
    private record ProductResponse(Guid Id, string Name, string? Category, DateTime CreatedAtUtc);
    private record CustomerAttentionItem(Guid CustomerId, string CustomerName, DateOnly LastOrderDate, int DaysSinceLastOrder, double AverageCadenceDays, bool NeedsAttention);
    private record ProductTrendItem(Guid ProductId, string ProductName, decimal CurrentPeriodTotal, decimal PreviousPeriodTotal, double? PercentChange, string Trend);

    private async Task<HttpClient> NewAuthedClientAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = $"insights-{Guid.NewGuid():N}@example.com",
            Password = "TestPass123!",
            DisplayName = "Insights Test",
            BusinessName = "Insights Test Co"
        });
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    [Fact]
    public async Task CustomersNeedingAttention_FlagsCustomerFarPastTheirUsualCadence()
    {
        var client = await NewAuthedClientAsync();
        var customer = await (await client.PostAsJsonAsync("/api/customers", new { Name = "Regular Customer" })).Content.ReadFromJsonAsync<CustomerResponse>();
        var product = await (await client.PostAsJsonAsync("/api/products", new { Name = "Regular Product" })).Content.ReadFromJsonAsync<ProductResponse>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Regular 10-day cadence for several orders, then a long silence (~60 days) since the last one.
        var dates = new[] { today.AddDays(-100), today.AddDays(-90), today.AddDays(-80), today.AddDays(-70), today.AddDays(-60) };
        foreach (var date in dates)
        {
            await client.PostAsJsonAsync("/api/sales", new
            {
                CustomerId = customer!.Id,
                SaleDate = date.ToString("yyyy-MM-dd"),
                Items = new[] { new { ProductId = product!.Id, Quantity = 1, UnitPrice = 10.00m } }
            });
        }

        var attention = await client.GetFromJsonAsync<List<CustomerAttentionItem>>("/api/insights/customers-needing-attention");

        var item = Assert.Single(attention!);
        Assert.Equal(customer!.Id, item.CustomerId);
        Assert.True(item.NeedsAttention);
        Assert.Equal(10, item.AverageCadenceDays);
    }

    [Fact]
    public async Task CustomersNeedingAttention_ExcludesCustomersWithOnlyOneOrder()
    {
        var client = await NewAuthedClientAsync();
        var customer = await (await client.PostAsJsonAsync("/api/customers", new { Name = "One Time Customer" })).Content.ReadFromJsonAsync<CustomerResponse>();
        var product = await (await client.PostAsJsonAsync("/api/products", new { Name = "One Time Product" })).Content.ReadFromJsonAsync<ProductResponse>();

        await client.PostAsJsonAsync("/api/sales", new
        {
            CustomerId = customer!.Id,
            SaleDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-200).ToString("yyyy-MM-dd"),
            Items = new[] { new { ProductId = product!.Id, Quantity = 1, UnitPrice = 10.00m } }
        });

        var attention = await client.GetFromJsonAsync<List<CustomerAttentionItem>>("/api/insights/customers-needing-attention");

        Assert.Empty(attention!);
    }

    [Fact]
    public async Task ProductTrends_DetectsGrowth_WhenRecentSalesFarExceedPriorPeriod()
    {
        var client = await NewAuthedClientAsync();
        var customer = await (await client.PostAsJsonAsync("/api/customers", new { Name = "Trend Customer" })).Content.ReadFromJsonAsync<CustomerResponse>();
        var product = await (await client.PostAsJsonAsync("/api/products", new { Name = "Trending Product" })).Content.ReadFromJsonAsync<ProductResponse>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Previous 30-day window: one small sale. Current 30-day window: a much bigger one.
        await client.PostAsJsonAsync("/api/sales", new
        {
            CustomerId = customer!.Id,
            SaleDate = today.AddDays(-45).ToString("yyyy-MM-dd"),
            Items = new[] { new { ProductId = product!.Id, Quantity = 1, UnitPrice = 10.00m } }
        });
        await client.PostAsJsonAsync("/api/sales", new
        {
            CustomerId = customer.Id,
            SaleDate = today.AddDays(-5).ToString("yyyy-MM-dd"),
            Items = new[] { new { ProductId = product!.Id, Quantity = 10, UnitPrice = 10.00m } }
        });

        var trends = await client.GetFromJsonAsync<List<ProductTrendItem>>("/api/insights/product-trends");

        var item = Assert.Single(trends!);
        Assert.Equal("Growing", item.Trend);
        Assert.Equal(100.00m, item.CurrentPeriodTotal);
        Assert.Equal(10.00m, item.PreviousPeriodTotal);
    }
}
