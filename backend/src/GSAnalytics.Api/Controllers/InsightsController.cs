using GSAnalytics.Application.Auth;
using GSAnalytics.Application.Insights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSAnalytics.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/insights")]
public class InsightsController(IInsightsService insightsService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("customers-needing-attention")]
    public async Task<ActionResult<List<CustomerAttentionItem>>> CustomersNeedingAttention(CancellationToken ct)
    {
        return Ok(await insightsService.GetCustomersNeedingAttentionAsync(currentUser.BusinessId, ct));
    }

    [HttpGet("product-trends")]
    public async Task<ActionResult<List<ProductTrendItem>>> ProductTrends(CancellationToken ct)
    {
        return Ok(await insightsService.GetProductTrendsAsync(currentUser.BusinessId, ct));
    }
}
