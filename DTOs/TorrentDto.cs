namespace vaultApi.DTOs;

public class StartDownloadDto
{
    public required string MagnetUri { get; set; }
    public string? Title { get; set; }
}

public class DownloadStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string MagnetUri { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public double Progress { get; set; }
    public long DownloadSpeed { get; set; }
    public long UploadSpeed { get; set; }
    public long TotalSize { get; set; }
    public long DownloadedBytes { get; set; }
    public int Seeds { get; set; }
    public int Peers { get; set; }
    public string SavePath { get; set; } = string.Empty;
}
