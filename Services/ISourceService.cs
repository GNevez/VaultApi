using vaultApi.DTOs;

namespace vaultApi.Services;

public interface ISourceService
{
    Task<SourceResponseDto> AddAsync(int userId, AddSourceDto dto);
    Task<List<SourceResponseDto>> GetAllAsync(int userId);
    Task DeleteAsync(int userId, int sourceId);
    Task<PaginatedGamesDto> GetGamesAsync(int userId, int page = 1, int pageSize = 15, string? search = null);
}
