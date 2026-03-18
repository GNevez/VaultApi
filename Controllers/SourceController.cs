using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using vaultApi.DTOs;
using vaultApi.Services;

namespace vaultApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SourceController : ControllerBase
{
    private readonly ISourceService _sourceService;

    public SourceController(ISourceService sourceService)
    {
        _sourceService = sourceService;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Add(AddSourceDto dto)
    {
        try
        {
            var result = await _sourceService.AddAsync(GetUserId(), dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (HttpRequestException)
        {
            return BadRequest(new { message = "Failed to fetch the URL" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var sources = await _sourceService.GetAllAsync(GetUserId());
        return Ok(sources);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _sourceService.DeleteAsync(GetUserId(), id);
            return Ok(new { message = "Source removed" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("games")]
    public async Task<IActionResult> GetGames([FromQuery] int page = 1, [FromQuery] int pageSize = 15, [FromQuery] string? search = null)
    {
        var result = await _sourceService.GetGamesAsync(GetUserId(), page, pageSize, search);
        return Ok(result);
    }
}
