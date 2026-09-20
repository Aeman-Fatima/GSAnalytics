using Microsoft.AspNetCore.Mvc;

namespace GSAnalytics.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            timestampUtc = DateTime.UtcNow
        });
    }
}
