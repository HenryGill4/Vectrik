using Vectrik.Models;

namespace Vectrik.Services;

public interface IUserSettingsService
{
    Task<UserSettings> GetSettingsAsync(int userId);
    Task SaveThemeAsync(int userId, string theme);
    Task SaveDebugFabAsync(int userId, bool enabled);
    Task SaveSettingsAsync(UserSettings settings);
}
