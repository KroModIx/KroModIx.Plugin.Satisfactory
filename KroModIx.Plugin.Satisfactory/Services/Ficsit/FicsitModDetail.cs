using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KroModIx.Plugin.Satisfactory.Services.Ficsit;

/// <summary>Vollständige Mod-Details aus <c>getModByIdOrReference</c>.
/// Enthält Long-Description, Autoren-Liste, Compatibility-Status
/// (Early Access / Experimental) und Latest-Version mit Download-Link.</summary>
public sealed class FicsitModDetail
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("mod_reference")]
    public string ModReference { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("short_description")]
    public string ShortDescription { get; set; } = "";

    /// <summary>Markdown-Beschreibung — enthält typischerweise Markdown-Syntax
    /// mit ficsit-Markup. Für unsere Anzeige stripen wir das via
    /// <see cref="HtmlStrip"/> zu Plain-Text.</summary>
    [JsonPropertyName("full_description")]
    public string FullDescription { get; set; } = "";

    [JsonPropertyName("logo")]
    public string Logo { get; set; } = "";

    [JsonPropertyName("source_url")]
    public string SourceUrl { get; set; } = "";

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

    [JsonPropertyName("authors")]
    public List<FicsitAuthor> Authors { get; set; } = new();

    [JsonPropertyName("compatibility")]
    public FicsitCompatibility? Compatibility { get; set; }

    /// <summary>Neueste Version — in unserem API-Call via
    /// <c>versions(filter: {limit: 1, order: desc, order_by: created_at})</c>
    /// abgefragt. Enthält den Direct-Download-<see cref="FicsitVersion.Link"/>.</summary>
    [JsonPropertyName("versions")]
    public List<FicsitVersion> Versions { get; set; } = new();

    public string AuthorsDisplay
    {
        get
        {
            if (Authors.Count == 0) return "";
            var names = new List<string>();
            foreach (var a in Authors)
                if (!string.IsNullOrWhiteSpace(a.User?.Username)) names.Add(a.User.Username);
            return string.Join(", ", names);
        }
    }

    public FicsitVersion? LatestVersion => Versions.Count > 0 ? Versions[0] : null;
}

public sealed class FicsitAuthor
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("user")]
    public FicsitUser? User { get; set; }
}

public sealed class FicsitUser
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";
}

public sealed class FicsitCompatibility
{
    [JsonPropertyName("EA")]
    public FicsitCompatibilityState? EA { get; set; }

    [JsonPropertyName("EXP")]
    public FicsitCompatibilityState? EXP { get; set; }
}

public sealed class FicsitCompatibilityState
{
    /// <summary>„Works", „Damaged", „Broken", „Unknown". Frei-Text vom API.</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("note")]
    public string Note { get; set; } = "";
}

public sealed class FicsitVersion
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>SemVer, z. B. „1.2.3". Wird im UI als „v1.2.3" gezeigt.</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    /// <summary>Direct-Download-URL fürs .smod-File. Kein OAuth nötig — das
    /// funktioniert für alle User (im Gegensatz zu Nexus wo Direct-URLs
    /// Premium brauchen).</summary>
    [JsonPropertyName("link")]
    public string Link { get; set; } = "";

    /// <summary>SHA256-Hash der .smod-Datei — für Integritäts-Prüfung nach
    /// dem Download. Wir loggen ihn aktuell nur; Verifikation optional in v0.2.</summary>
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
