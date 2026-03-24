namespace Opcentrix_V3.Services;

public interface ITenantFeatureService
{
    bool IsEnabled(string featureKey);
    Task<List<(string Key, bool Enabled)>> GetAllFeaturesAsync();
    Task SetFeatureAsync(string featureKey, bool enabled);
    Task InitializeAsync();
}
