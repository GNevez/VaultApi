using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using vaultApi.DTOs;
using vaultApi.Services;

namespace vaultApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class LibraryController : ControllerBase
{
    private readonly ILibraryService _libraryService;

    public LibraryController(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Add(AddToLibraryDto dto)
    {
        try
        {
            var result = await _libraryService.AddAsync(GetUserId(), dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _libraryService.GetAllAsync(GetUserId());
        return Ok(items);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(int id)
    {
        try
        {
            await _libraryService.RemoveAsync(GetUserId(), id);
            return Ok(new { message = "Removed from library" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("exists")]
    public async Task<IActionResult> Exists([FromQuery] int sourceId, [FromQuery] int gameIndex)
    {
        var exists = await _libraryService.ExistsAsync(GetUserId(), sourceId, gameIndex);
        return Ok(new { exists });
    }
}
