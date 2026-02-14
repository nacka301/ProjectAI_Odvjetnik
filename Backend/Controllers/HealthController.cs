using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// Health check endpoint for monitoring.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// GET /api/health — verifies the backend is alive.
    /// Does NOT expose service configuration details.
    /// </summary>
    [HttpGet]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
        });
    }
}
