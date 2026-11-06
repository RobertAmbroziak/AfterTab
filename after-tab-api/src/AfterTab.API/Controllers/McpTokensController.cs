using AfterTab.API.Mcp;
using AfterTab.Mcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AfterTab.API.Controllers;

// Only the ordinary PWA session can manage tokens. MCP bearer tokens use a separate scheme.
[ApiController, Authorize, Route("api/profile")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class McpTokensController(McpTokenService tokens, IOptions<McpOptions> options) : ControllerBase
{
    [HttpGet("mcp-tokens")]
    public async Task<IActionResult> List(CancellationToken ct)
        => options.Value.Enabled ? Ok(await tokens.ListAsync(ct)) : NotFound();
    [HttpPost("mcp-tokens")]
    public async Task<IActionResult> Create(CreateMcpToken input, CancellationToken ct)
        => options.Value.Enabled ? Ok(await tokens.CreateAsync(input, ct)) : NotFound();
    [HttpDelete("mcp-tokens/{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        if (!options.Value.Enabled) return NotFound();
        await tokens.RevokeAsync(id, ct);
        return NoContent();
    }

    [HttpGet("mcp-instructions")]
    public async Task<IActionResult> GetInstructions(CancellationToken ct)
        => options.Value.Enabled ? Ok(await tokens.GetInstructionsAsync(ct)) : NotFound();

    [HttpPut("mcp-instructions")]
    public async Task<IActionResult> UpdateInstructions(McpInstructionsDto input, CancellationToken ct)
        => options.Value.Enabled ? Ok(await tokens.UpdateInstructionsAsync(input.Instructions, ct)) : NotFound();
}
