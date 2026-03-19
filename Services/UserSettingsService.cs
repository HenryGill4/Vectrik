using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public class UserSettingsService : IUserSettingsService
{
    private readonly TenantDbContext _db;

    public UserSettingsService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<UserSettings> GetSettingsAsync(int userId)
    {
        var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (settings is not null) return settings;

        settings = new UserSettings { UserId = userId };
        _db.UserSettings.Add(settings);
        await _db.SaveChangesAsync();
        return settings;
    }

    public async Task SaveThemeAsync(int userId, string theme)
    {
        var settings = await GetSettingsAsync(userId);
        settings.Theme = theme;
        await _db.SaveChangesAsync();
    }

    public async Task SaveSettingsAsync(UserSettings settings)
    {
        _db.UserSettings.Update(settings);
        await _db.SaveChangesAsync();
    }
}
