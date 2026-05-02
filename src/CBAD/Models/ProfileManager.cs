using System.Text.Json;
using System.Text.Json.Serialization;

namespace CBAD.Models;

/// <summary>
/// Manages battery-profile persistence and the active-profile selection.
/// Profiles are serialised to <c>profiles.json</c> in the application's base directory
/// so they persist across sessions.
/// </summary>
public sealed class ProfileManager
{
    private static readonly string ProfilesFilePath =
        Path.Combine(AppContext.BaseDirectory, "profiles.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented        = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private List<BatteryProfile> _profiles = new();
    private string? _activeProfileName;

    // ── Public surface ────────────────────────────────────────────────────

    /// <summary>The currently active profile, or <c>null</c> if none is set.</summary>
    public BatteryProfile? ActiveProfile =>
        _activeProfileName is null
            ? null
            : _profiles.FirstOrDefault(p => p.ProfileName == _activeProfileName);

    /// <summary>Returns a read-only snapshot of all saved profiles.</summary>
    public IReadOnlyList<BatteryProfile> Profiles => _profiles.AsReadOnly();

    /// <summary>Name of the currently active profile, or <c>null</c>.</summary>
    public string? ActiveProfileName => _activeProfileName;

    public ProfileManager()
    {
        Load();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────

    /// <summary>Adds a new profile.  The name must be unique.</summary>
    public void Add(BatteryProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ProfileName))
            throw new ArgumentException("Profile name must not be empty.", nameof(profile));

        if (_profiles.Any(p => p.ProfileName == profile.ProfileName))
            throw new InvalidOperationException($"A profile named '{profile.ProfileName}' already exists.");

        _profiles.Add(profile);
        Save();
    }

    /// <summary>Replaces the stored profile that shares the same <see cref="BatteryProfile.ProfileName"/>.</summary>
    public void Update(BatteryProfile profile)
    {
        var idx = _profiles.FindIndex(p => p.ProfileName == profile.ProfileName);
        if (idx < 0)
            throw new InvalidOperationException($"Profile '{profile.ProfileName}' not found.");

        _profiles[idx] = profile;
        Save();
    }

    /// <summary>Deletes the profile with the given name.  Clears active if it matches.</summary>
    public void Delete(string profileName)
    {
        var removed = _profiles.RemoveAll(p => p.ProfileName == profileName);
        if (removed == 0)
            throw new InvalidOperationException($"Profile '{profileName}' not found.");

        if (_activeProfileName == profileName)
            _activeProfileName = null;

        Save();
    }

    /// <summary>Returns the profile with the given name, or <c>null</c> if not found.</summary>
    public BatteryProfile? Get(string profileName) =>
        _profiles.FirstOrDefault(p => p.ProfileName == profileName);

    /// <summary>Marks the named profile as active.  Throws if it does not exist.</summary>
    public void SetActive(string profileName)
    {
        if (!_profiles.Any(p => p.ProfileName == profileName))
            throw new InvalidOperationException($"Profile '{profileName}' not found.");

        _activeProfileName = profileName;
        Save();
    }

    /// <summary>Clears the active-profile selection.</summary>
    public void ClearActive()
    {
        _activeProfileName = null;
        Save();
    }

    // ── Persistence ───────────────────────────────────────────────────────

    private void Load()
    {
        if (!File.Exists(ProfilesFilePath))
            return;

        try
        {
            var json = File.ReadAllText(ProfilesFilePath);
            var data = JsonSerializer.Deserialize<ProfileData>(json, JsonOptions);
            if (data is not null)
            {
                _profiles          = data.Profiles ?? new();
                _activeProfileName = data.ActiveProfileName;
            }
        }
        catch (Exception ex)
        {
            // If the file is corrupt start fresh rather than crashing.
            AppLog.Warn($"Could not load profiles ({ex.GetType().Name}): {ex.Message}");
            _profiles          = new();
            _activeProfileName = null;
        }
    }

    private void Save()
    {
        var data = new ProfileData
        {
            Profiles          = _profiles,
            ActiveProfileName = _activeProfileName,
        };
        File.WriteAllText(ProfilesFilePath, JsonSerializer.Serialize(data, JsonOptions));
    }

    // ── Private DTO ───────────────────────────────────────────────────────

    private sealed class ProfileData
    {
        public List<BatteryProfile> Profiles { get; set; } = new();
        public string? ActiveProfileName { get; set; }
    }
}
