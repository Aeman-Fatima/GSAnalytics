namespace GSAnalytics.Application.Insights;

/// <summary>
/// A customer "needs attention" when they've gone noticeably longer than their own historical
/// ordering rhythm without a new order. The threshold (1.5x their average gap between orders) is a
/// fixed, documented multiplier — not a learned/statistical model — so the result is always
/// explainable as "you usually hear from them every X days; it's been Y."
/// </summary>
public record CustomerAttentionItem(
    Guid CustomerId,
    string CustomerName,
    DateOnly LastOrderDate,
    int DaysSinceLastOrder,
    double AverageCadenceDays,
    bool NeedsAttention);

/// <summary>
/// Compares each product's total sales in the trailing 30 days against the 30 days before that.
/// Trend labels come from a fixed +/-10% threshold on that change — again a documented rule, not
/// a statistical or ML classification.
/// </summary>
public record ProductTrendItem(
    Guid ProductId,
    string ProductName,
    decimal CurrentPeriodTotal,
    decimal PreviousPeriodTotal,
    double? PercentChange,
    string Trend);

public static class ProductTrend
{
    public const string Growing = "Growing";
    public const string Declining = "Declining";
    public const string Stable = "Stable";
    public const string New = "New";
    public const string NoRecentActivity = "NoRecentActivity";
}
