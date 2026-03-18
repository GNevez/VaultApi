namespace vaultApi.DTOs;

public class AddSourceDto
{
    public required string Url { get; set; }
}

public class SourceResponseDto
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class GameDto
{
    public string Title { get; set; } = string.Empty;
    public List<string> Uris { get; set; } = new();
    public string UploadDate { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public int SourceId { get; set; }
    public int GameIndex { get; set; }
}

public class PaginatedGamesDto
{
    public List<GameDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}
