using System;
using System.Text.Json.Serialization;

namespace KroModIx.Plugin.Satisfactory.Services.Ficsit;

/// <summary>Katalog-Zeile aus <c>getMods</c>. Zusatz-Felder in
/// <see cref="FicsitModDetail"/> — die kommen aus <c>getModByIdOrReference</c>
/// beim Öffnen des Detail-Dialogs. Analog zu <c>NexusCatalogEntry</c> im
/// Icarus-Plugin.</summary>
public sealed class FicsitCatalogEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>ficsit-app-Mod-Reference (stabiler Identifier, z. B.
    /// „RefinedPower", „MicroManage"). Zusammen mit <c>id</c> nutzbar in
    /// <c>getModByIdOrReference</c>.</summary>
    [JsonPropertyName("mod_reference")]
    public string ModReference { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("short_description")]
    public string ShortDescription { get; set; } = "";

    /// <summary>Cover-URL. Für nicht gesetzte Logos liefert die API einen
    /// leeren String — nicht null.</summary>
    [JsonPropertyName("logo")]
    public string Logo { get; set; } = "";

    [JsonPropertyName("views")]
    public int Views { get; set; }

    [JsonPropertyName("downloads")]
    public int Downloads { get; set; }

    [JsonPropertyName("popularity")]
    public float Popularity { get; set; }

    [JsonPropertyName("hotness")]
    public float Hotness { get; set; }

    [JsonPropertyName("last_version_date")]
    public DateTime LastVersionDate { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>ficsit.app-URL zur Mod-Detail-Seite im Browser.</summary>
    public string DetailUrl => $"https://ficsit.app/mod/{Id}";
}

/// <summary>Snapshot des Katalogs auf Disk. Wird alle 24 h neu vom API
/// gepullt (Age-Check in <see cref="FicsitCatalogService"/>).</summary>
public sealed class FicsitCatalogSnapshot
{
    [JsonPropertyName("savedUtc")]
    public DateTime SavedUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("entries")]
    public System.Collections.Generic.List<FicsitCatalogEntry> Entries { get; set; } = new();
}
