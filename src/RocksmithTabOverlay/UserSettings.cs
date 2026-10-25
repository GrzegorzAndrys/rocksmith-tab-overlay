using System.IO;
using System.Text.Json;

namespace RocksmithTabOverlay;

internal sealed class UserSettings
{
    public bool StaticPageMode { get; set; } = true;
    public double NotationScale { get; set; } = 1.0;
    public int BarsVisible { get; set; } = 4;
    public string SnifferUrl { get; set; } = "http://127.0.0.1:9938/tab";
    public bool CustomPlacementEnabled { get; set; }
    public double CustomLeftRatio { get; set; }
    public double CustomTopRatio { get; set; } = 0.012;
    public double CustomWidthRatio { get; set; } = 1.0;
    public double CustomHeightRatio { get; set; } = 0.47;
    public double BackgroundOpacity { get; set; } = 0.84;
    public double OverlayOpacity { get; set; } = 1.0;

    private static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RocksmithTabOverlay");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "user-settings.json");

    // Older builds stored preferences next to the executable. Keep reading that
    // file so upgrading does not reset the user's layout, then migrate it once.
    private static string LegacySettingsPath => Path.Combine(AppContext.BaseDirectory, "user-settings.json");

    public static UserSettings Load()
    {
        var settings = LoadFrom(SettingsPath);
        if (settings != null) return settings;

        settings = LoadFrom(LegacySettingsPath);
        if (settings != null)
        {
            settings.Save();
            return settings;
        }

        return new UserSettings();
    }

    private static UserSettings? LoadFrom(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path)) ?? new UserSettings();
        }
        catch
        {
            // A damaged or temporarily locked preference file should never stop the overlay.
        }
        return null;
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Portable installs may not have a writable LocalAppData directory.
            try
            {
                File.WriteAllText(LegacySettingsPath, json);
            }
            catch
            {
                // Preferences are optional; rendering and synchronization take priority.
            }
        }
    }
}
