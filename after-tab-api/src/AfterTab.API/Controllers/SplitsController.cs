using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Interfaces.Expenses;
using Microsoft.AspNetCore.Mvc;
namespace AfterTab.API.Controllers;
[ApiController]
[Route("api/splits")]
public sealed class SplitsController(ISplitCalculator calculator) : ControllerBase
{
    // Stateless preview: no database reads/writes and no user identity accepted.
    [HttpPost("preview")]
    [RequestSizeLimit(1_000_000)]
    public ActionResult<IReadOnlyList<SplitAmount>> Preview(SplitPreviewRequest request)
    {
        try { return Ok(calculator.CalculatePercentage(request.AmountMinor, request.Shares)); }
        catch (ArgumentException ex) { return Problem(statusCode: 400, title: ex.Message); }
    }
}
