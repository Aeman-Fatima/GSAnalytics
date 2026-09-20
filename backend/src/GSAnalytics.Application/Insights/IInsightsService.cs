namespace GSAnalytics.Application.Insights;

public interface IInsightsService
{
    Task<List<CustomerAttentionItem>> GetCustomersNeedingAttentionAsync(Guid businessId, CancellationToken ct = default);

    Task<List<ProductTrendItem>> GetProductTrendsAsync(Guid businessId, CancellationToken ct = default);
}
