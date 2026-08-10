using System;
using System.IO;
using System.Text.Json;
using NLog;

namespace KroModIx.Plugin.Satisfactory.Services.Ficsit;

/// <summary>Lädt/speichert <see cref="FicsitSettings"/> als JSON. Kein Secret-
/// Handling nötig (kein API-Key). Analog zu <c>NexusSettingsService</c> minus
/// den Secrets-Teil.</summary>
public sealed class FicsitSettingsService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly SatisfactoryPaths _paths;

    public FicsitSettingsService(SatisfactoryPaths paths)
    {
        _paths = paths;
        Current = TryLoad() ?? new FicsitSettings();
    }

    public FicsitSettings Current { get; private set; }

    public void Save(FicsitSettings settings)
    {
        Current = settings;
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var tmp = _paths.FicsitSettingsPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _paths.FicsitSettingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "ficsit-Settings-Save fehlgeschlagen");
        }
    }

    private FicsitSettings? TryLoad()
    {
        try
        {
            if (!File.Exists(_paths.FicsitSettingsPath)) return null;
            var json = File.ReadAllText(_paths.FicsitSettingsPath);
            return JsonSerializer.Deserialize<FicsitSettings>(json);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "ficsit-Settings-Load fehlgeschlagen");
            return null;
        }
    }
}
