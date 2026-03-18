namespace vaultApi.DTOs;

public class AddToLibraryDto
{
    public int SourceId { get; set; }
    public int GameIndex { get; set; }
}

public class LibraryItemResponseDto
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public int GameIndex { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<string> Uris { get; set; } = new();
    public string UploadDate { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
}
