using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using vaultApi.Data;
using vaultApi.DTOs;
using vaultApi.Models;

namespace vaultApi.Services;

public class SourceService : ISourceService
{
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;

    public SourceService(AppDbContext context, HttpClient httpClient)
    {
        _context = context;
        _httpClient = httpClient;
    }

    public async Task<SourceResponseDto> AddAsync(int userId, AddSourceDto dto)
    {
        var exists = await _context.Sources.AnyAsync(s => s.UserId == userId && s.Url == dto.Url);
        if (exists)
            throw new InvalidOperationException("Source already added");

        var json = await FetchAndValidateAsync(dto.Url);
        var name = json.GetProperty("name").GetString() ?? "Unknown";

        var source = new Source
        {
            UserId = userId,
            Url = dto.Url,
            Name = name
        };

        _context.Sources.Add(source);
        await _context.SaveChangesAsync();

        return new SourceResponseDto
        {
            Id = source.Id,
            Url = source.Url,
            Name = source.Name,
            CreatedAt = source.CreatedAt
        };
    }

    public async Task<List<SourceResponseDto>> GetAllAsync(int userId)
    {
        return await _context.Sources
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SourceResponseDto
            {
                Id = s.Id,
                Url = s.Url,
                Name = s.Name,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();
    }

    public async Task DeleteAsync(int userId, int sourceId)
    {
        var source = await _context.Sources.FirstOrDefaultAsync(s => s.Id == sourceId && s.UserId == userId);
        if (source == null)
            throw new InvalidOperationException("Source not found");

        _context.Sources.Remove(source);
        await _context.SaveChangesAsync();
    }

    public async Task<PaginatedGamesDto> GetGamesAsync(int userId, int page = 1, int pageSize = 15, string? search = null)
    {
        var sources = await _context.Sources.Where(s => s.UserId == userId).ToListAsync();
        var allGames = new List<GameDto>();

        foreach (var source in sources)
        {
            try
            {
                var json = await FetchAndValidateAsync(source.Url);
                var sourceName = json.GetProperty("name").GetString() ?? source.Name;
                var downloads = json.GetProperty("downloads");
                int gameIndex = 0;

                foreach (var download in downloads.EnumerateArray())
                {
                    var uris = new List<string>();
                    foreach (var uri in download.GetProperty("uris").EnumerateArray())
                        uris.Add(uri.GetString() ?? "");

                    allGames.Add(new GameDto
                    {
                        Title = download.GetProperty("title").GetString() ?? "",
                        Uris = uris,
                        UploadDate = download.GetProperty("uploadDate").GetString() ?? "",
                        FileSize = download.GetProperty("fileSize").GetString() ?? "",
                        SourceName = sourceName,
                        SourceId = source.Id,
                        GameIndex = gameIndex
                    });
                    gameIndex++;
                }
            }
            catch
            {
                continue;
            }
        }

        var filtered = allGames.OrderByDescending(g => g.UploadDate).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(g => g.Title.Contains(search, StringComparison.OrdinalIgnoreCase));

        var filteredList = filtered.ToList();
        var totalItems = filteredList.Count;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = filteredList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PaginatedGamesDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    private async Task<JsonElement> FetchAndValidateAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        if (!root.TryGetProperty("name", out _))
            throw new InvalidOperationException("Invalid JSON: missing 'name' field");

        if (!root.TryGetProperty("downloads", out var downloads) || downloads.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Invalid JSON: missing or invalid 'downloads' array");

        if (downloads.GetArrayLength() > 0)
        {
            var first = downloads[0];
            if (!first.TryGetProperty("title", out _))
                throw new InvalidOperationException("Invalid JSON: downloads missing 'title'");
            if (!first.TryGetProperty("uris", out _))
                throw new InvalidOperationException("Invalid JSON: downloads missing 'uris'");
            if (!first.TryGetProperty("uploadDate", out _))
                throw new InvalidOperationException("Invalid JSON: downloads missing 'uploadDate'");
            if (!first.TryGetProperty("fileSize", out _))
                throw new InvalidOperationException("Invalid JSON: downloads missing 'fileSize'");
        }

        return root;
    }
}
