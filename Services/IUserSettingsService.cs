using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IUserSettingsService
{
    Task<UserSettings> GetSettingsAsync(int userId);
    Task SaveThemeAsync(int userId, string theme);
    Task SaveDebugFabAsync(int userId, bool enabled);
    Task SaveSettingsAsync(UserSettings settings);
}
