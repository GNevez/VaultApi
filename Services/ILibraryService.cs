using vaultApi.DTOs;

namespace vaultApi.Services;

public interface ILibraryService
{
    Task<LibraryItemResponseDto> AddAsync(int userId, AddToLibraryDto dto);
    Task<List<LibraryItemResponseDto>> GetAllAsync(int userId);
    Task RemoveAsync(int userId, int libraryItemId);
    Task<bool> ExistsAsync(int userId, int sourceId, int gameIndex);
}
