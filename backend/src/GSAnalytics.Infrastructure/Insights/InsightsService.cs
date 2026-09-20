using GSAnalytics.Application.Insights;
using GSAnalytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GSAnalytics.Infrastructure.Insights;

public class InsightsService(GSAnalyticsDbContext db) : IInsightsService
{
    /// <summary>A customer needs this many times their average gap, overdue, to be flagged.</summary>
    private const double AttentionMultiplier = 1.5;

    /// <summary>+/-10% over the trailing-30-vs-previous-30-day window counts as a real trend.</summary>
    private const double TrendThresholdPercent = 10.0;

    public async Task<List<CustomerAttentionItem>> GetCustomersNeedingAttentionAsync(Guid businessId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var ordersByCustomer = await db.Sales
            .Where(s => s.BusinessId == businessId)
            .Select(s => new { s.CustomerId, CustomerName = s.Customer!.Name, s.SaleDate })
            .ToListAsync(ct);

        var items = new List<CustomerAttentionItem>();

        foreach (var group in ordersByCustomer.GroupBy(o => new { o.CustomerId, o.CustomerName }))
        {
            var orderDates = group.Select(o => o.SaleDate).Distinct().OrderBy(d => d).ToList();
            if (orderDates.Count < 2)
            {
                continue; // not enough history to establish a cadence
            }

            var gaps = new List<int>();
            for (var i = 1; i < orderDates.Count; i++)
            {
                gaps.Add(orderDates[i].DayNumber - orderDates[i - 1].DayNumber);
            }

            var averageCadence = gaps.Average();
            var lastOrderDate = orderDates[^1];
            var daysSinceLastOrder = today.DayNumber - lastOrderDate.DayNumber;
            var needsAttention = daysSinceLastOrder > averageCadence * AttentionMultiplier;

            items.Add(new CustomerAttentionItem(
                group.Key.CustomerId,
                group.Key.CustomerName,
                lastOrderDate,
                daysSinceLastOrder,
                Math.Round(averageCadence, 1),
                needsAttention));
        }

        return items
            .OrderByDescending(i => i.NeedsAttention)
            .ThenByDescending(i => i.DaysSinceLastOrder - i.AverageCadenceDays)
            .ToList();
    }

    public async Task<List<ProductTrendItem>> GetProductTrendsAsync(Guid businessId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentPeriodStart = today.AddDays(-30);
        var previousPeriodStart = today.AddDays(-60);

        var lineItems = await db.Sales
            .Where(s => s.BusinessId == businessId && s.SaleDate >= previousPeriodStart)
            .SelectMany(s => s.Items, (s, i) => new { s.SaleDate, i.ProductId, ProductName = i.Product!.Name, LineTotal = i.Quantity * i.UnitPrice })
            .ToListAsync(ct);

        var items = new List<ProductTrendItem>();

        foreach (var group in lineItems.GroupBy(l => new { l.ProductId, l.ProductName }))
        {
            var current = group.Where(l => l.SaleDate >= currentPeriodStart).Sum(l => l.LineTotal);
            var previous = group.Where(l => l.SaleDate < currentPeriodStart).Sum(l => l.LineTotal);

            double? percentChange = null;
            string trend;

            if (previous == 0 && current > 0)
            {
                trend = ProductTrend.New;
            }
            else if (previous > 0 && current == 0)
            {
                trend = ProductTrend.NoRecentActivity;
            }
            else if (previous == 0 && current == 0)
            {
                continue; // nothing happened in either window — not worth reporting
            }
            else
            {
                percentChange = (double)((current - previous) / previous) * 100.0;
                trend = percentChange > TrendThresholdPercent ? ProductTrend.Growing
                    : percentChange < -TrendThresholdPercent ? ProductTrend.Declining
                    : ProductTrend.Stable;
            }

            items.Add(new ProductTrendItem(group.Key.ProductId, group.Key.ProductName, current, previous, percentChange, trend));
        }

        return items.OrderByDescending(i => i.CurrentPeriodTotal).ToList();
    }
}
