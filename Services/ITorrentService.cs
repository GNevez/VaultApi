using vaultApi.DTOs;

namespace vaultApi.Services;

public interface ITorrentService
{
    Task<DownloadStatusDto> StartDownloadAsync(string magnetUri, string? title = null);
    Task<List<DownloadStatusDto>> GetAllDownloadsAsync();
    Task<DownloadStatusDto?> GetDownloadAsync(string id);
    Task PauseAsync(string id);
    Task ResumeAsync(string id);
    Task CancelAsync(string id);
}
