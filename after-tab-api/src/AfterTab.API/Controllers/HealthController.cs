using Microsoft.AspNetCore.Mvc;
namespace AfterTab.API.Controllers;
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", application = "AfterTab.API" });
}
