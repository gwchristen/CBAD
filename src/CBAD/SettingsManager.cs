using System.Text.Json;

namespace CBAD;

internal static class SettingsManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CBAD",
        "settings.json");

    public static AppSettings Load()
        => Load(SettingsPath);

    public static void Save(AppSettings settings)
        => Save(settings, SettingsPath);

    internal static AppSettings Load(string settingsPath)
    {
        try
        {
            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Could not load settings ({ex.GetType().Name}): {ex.Message}");
            return new AppSettings();
        }
    }

    internal static void Save(AppSettings settings, string settingsPath)
    {
        string? tempPath = null;

        try
        {
            var directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            tempPath = $"{settingsPath}.{Guid.NewGuid():N}.tmp";
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, settingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Could not save settings ({ex.GetType().Name}): {ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                }
            }
        }
    }
}
