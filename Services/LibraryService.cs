using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using vaultApi.Data;
using vaultApi.DTOs;
using vaultApi.Models;

namespace vaultApi.Services;

public class LibraryService : ILibraryService
{
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;

    public LibraryService(AppDbContext context, HttpClient httpClient)
    {
        _context = context;
        _httpClient = httpClient;
    }

    public async Task<LibraryItemResponseDto> AddAsync(int userId, AddToLibraryDto dto)
    {
        var source = await _context.Sources.FirstOrDefaultAsync(s => s.Id == dto.SourceId && s.UserId == userId);
        if (source == null)
            throw new InvalidOperationException("Source not found");

        var exists = await _context.LibraryItems.AnyAsync(l =>
            l.UserId == userId && l.SourceId == dto.SourceId && l.GameIndex == dto.GameIndex);
        if (exists)
            throw new InvalidOperationException("Game already in library");

        // Validate that the game index exists
        var gameData = await FetchGameAtIndexAsync(source.Url, dto.GameIndex);

        var item = new LibraryItem
        {
            UserId = userId,
            SourceId = dto.SourceId,
            GameIndex = dto.GameIndex
        };

        _context.LibraryItems.Add(item);
        await _context.SaveChangesAsync();

        return new LibraryItemResponseDto
        {
            Id = item.Id,
            SourceId = source.Id,
            GameIndex = dto.GameIndex,
            Title = gameData.Title,
            Uris = gameData.Uris,
            UploadDate = gameData.UploadDate,
            FileSize = gameData.FileSize,
            SourceName = source.Name,
            AddedAt = item.AddedAt
        };
    }

    public async Task<List<LibraryItemResponseDto>> GetAllAsync(int userId)
    {
        var items = await _context.LibraryItems
            .Include(l => l.Source)
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.AddedAt)
            .ToListAsync();

        var result = new List<LibraryItemResponseDto>();

        // Group by source to avoid fetching the same URL multiple times
        var grouped = items.GroupBy(i => i.SourceId);

        foreach (var group in grouped)
        {
            var source = group.First().Source;
            JsonElement? jsonRoot = null;

            try
            {
                var response = await _httpClient.GetAsync(source.Url);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                jsonRoot = JsonDocument.Parse(content).RootElement;
            }
            catch
            {
                // If source URL fails, skip these items
                continue;
            }

            var downloads = jsonRoot.Value.GetProperty("downloads");
            var sourceName = jsonRoot.Value.GetProperty("name").GetString() ?? source.Name;

            foreach (var item in group)
            {
                try
                {
                    if (item.GameIndex < 0 || item.GameIndex >= downloads.GetArrayLength())
                        continue;

                    var download = downloads[item.GameIndex];
                    var uris = new List<string>();
                    foreach (var uri in download.GetProperty("uris").EnumerateArray())
                        uris.Add(uri.GetString() ?? "");

                    result.Add(new LibraryItemResponseDto
                    {
                        Id = item.Id,
                        SourceId = item.SourceId,
                        GameIndex = item.GameIndex,
                        Title = download.GetProperty("title").GetString() ?? "",
                        Uris = uris,
                        UploadDate = download.GetProperty("uploadDate").GetString() ?? "",
                        FileSize = download.GetProperty("fileSize").GetString() ?? "",
                        SourceName = sourceName,
                        AddedAt = item.AddedAt
                    });
                }
                catch
                {
                    continue;
                }
            }
        }

        return result.OrderByDescending(r => r.AddedAt).ToList();
    }

    public async Task RemoveAsync(int userId, int libraryItemId)
    {
        var item = await _context.LibraryItems.FirstOrDefaultAsync(l => l.Id == libraryItemId && l.UserId == userId);
        if (item == null)
            throw new InvalidOperationException("Library item not found");

        _context.LibraryItems.Remove(item);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(int userId, int sourceId, int gameIndex)
    {
        return await _context.LibraryItems.AnyAsync(l =>
            l.UserId == userId && l.SourceId == sourceId && l.GameIndex == gameIndex);
    }

    private async Task<GameDto> FetchGameAtIndexAsync(string url, int index)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(content).RootElement;
        var downloads = root.GetProperty("downloads");

        if (index < 0 || index >= downloads.GetArrayLength())
            throw new InvalidOperationException("Invalid game index");

        var download = downloads[index];
        var uris = new List<string>();
        foreach (var uri in download.GetProperty("uris").EnumerateArray())
            uris.Add(uri.GetString() ?? "");

        return new GameDto
        {
            Title = download.GetProperty("title").GetString() ?? "",
            Uris = uris,
            UploadDate = download.GetProperty("uploadDate").GetString() ?? "",
            FileSize = download.GetProperty("fileSize").GetString() ?? ""
        };
    }
}
