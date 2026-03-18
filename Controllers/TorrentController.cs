using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using vaultApi.DTOs;
using vaultApi.Services;

namespace vaultApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TorrentController : ControllerBase
{
    private readonly ITorrentService _torrentService;

    public TorrentController(ITorrentService torrentService)
    {
        _torrentService = torrentService;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start(StartDownloadDto dto)
    {
        try
        {
            var result = await _torrentService.StartDownloadAsync(dto.MagnetUri, dto.Title);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var downloads = await _torrentService.GetAllDownloadsAsync();
        return Ok(downloads);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var download = await _torrentService.GetDownloadAsync(id);
        if (download == null)
            return NotFound(new { message = "Download not found" });
        return Ok(download);
    }

    [HttpPost("{id}/pause")]
    public async Task<IActionResult> Pause(string id)
    {
        await _torrentService.PauseAsync(id);
        return Ok(new { message = "Download paused" });
    }

    [HttpPost("{id}/resume")]
    public async Task<IActionResult> Resume(string id)
    {
        await _torrentService.ResumeAsync(id);
        return Ok(new { message = "Download resumed" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancel(string id)
    {
        await _torrentService.CancelAsync(id);
        return Ok(new { message = "Download cancelled" });
    }
}
