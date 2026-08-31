using System;
using Godot;
using GodotFileAccess = Godot.FileAccess;

namespace LocalMultiControl.Scripts.Runtime;

/// <summary>
/// Read-only access to the mod's settings file (user://dual_role_adventure_settings.json)
/// for feature flags that are edited by hand. Writers of that file must preserve keys
/// they do not own (see LocalGhostHandsRuntime.SaveConfig).
/// </summary>
internal static class LocalModSettings
{
    private const string ConfigPath = "user://dual_role_adventure_settings.json";

    private static Godot.Collections.Dictionary? _settings;
    private static bool _loaded;

    internal static bool GetBool(string key, bool defaultValue)
    {
        LoadIfNeeded();
        if (_settings != null && _settings.TryGetValue(key, out Variant value))
        {
            return value.AsBool();
        }

        return defaultValue;
    }

    private static void LoadIfNeeded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        try
        {
            if (!GodotFileAccess.FileExists(ConfigPath))
            {
                return;
            }

            using GodotFileAccess? file = GodotFileAccess.Open(ConfigPath, GodotFileAccess.ModeFlags.Read);
            if (file == null)
            {
                return;
            }

            Variant parsed = Json.ParseString(file.GetAsText());
            if (parsed.VariantType == Variant.Type.Dictionary)
            {
                _settings = parsed.AsGodotDictionary();
            }
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"Failed to read mod settings: {exception.Message}");
        }
    }
}
