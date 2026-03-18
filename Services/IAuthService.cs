using vaultApi.DTOs;

namespace vaultApi.Services;

public interface IAuthService
{
    Task RegisterAsync(RegisterDto dto);
    Task<string> LoginAsync(LoginDto dto);
}
