using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace KroModIx.Plugin.Satisfactory.Services.Ficsit;

/// <summary>Minimaler GraphQL-Client gegen die ficsit.app-API (v2). Kein extra
/// Package — reines <see cref="HttpClient"/> + <see cref="System.Text.Json"/>.
/// Endpoint: <c>https://api.ficsit.app/v2/query</c>. Kein OAuth nötig für
/// öffentliche Read-Queries (Katalog + Detail + Version-Download-URL).
///
/// <para>Fehler-Verhalten: bei Netz-/HTTP-Fehlern oder GraphQL-Errors wird
/// <c>null</c> zurückgegeben und der Fehler geloggt — die VMs zeigen dann
/// eine Fehler-Meldung im UI. Analog zu <c>NexusApiClient</c> im Icarus-Plugin.</para>
/// </summary>
public sealed class FicsitApiClient : IDisposable
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private const string Endpoint = "https://api.ficsit.app/v2/query";

    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public FicsitApiClient(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("KroModIx.Plugin.Satisfactory/0.1 (github.com/KroModIx)");
    }

    /// <summary>Paginierte Katalog-Query. <paramref name="limit"/> deckelt die
    /// API bei ~100 pro Request — für den Gesamt-Katalog iterieren wir in
    /// <see cref="FicsitCatalogService"/>.</summary>
    public async Task<FicsitModsPage?> GetModsAsync(int limit, int offset,
        string orderBy = "popularity", string order = "desc",
        CancellationToken ct = default)
    {
        const string query = """
            query Mods($limit: Int!, $offset: Int!, $order_by: ModFields!, $order: Order!) {
              mods: getMods(filter: {limit: $limit, offset: $offset, order_by: $order_by, order: $order}) {
                count
                mods {
                  id
                  mod_reference
                  name
                  short_description
                  logo
                  views
                  downloads
                  popularity
                  hotness
                  last_version_date
                  created_at
                }
              }
            }
            """;
        var vars = new Dictionary<string, object?>
        {
            ["limit"] = limit,
            ["offset"] = offset,
            ["order_by"] = orderBy,
            ["order"] = order,
        };
        var data = await ExecuteAsync<GetModsData>(query, vars, ct);
        return data?.Mods;
    }

    /// <summary>Vollständige Mod-Details inkl. Latest-Version + Download-Link.
    /// <paramref name="modIdOrRef"/> akzeptiert beides — die ficsit-API löst
    /// intern auf.</summary>
    public async Task<FicsitModDetail?> GetModDetailAsync(string modIdOrRef,
        CancellationToken ct = default)
    {
        // WICHTIG: ficsit-Schema erwartet String! als Typ für $modId, nicht
        // ModID! — obwohl `modIdOrReference` beides akzeptiert (Id oder
        // mod_reference). ModID! wirft GRAPHQL_VALIDATION_FAILED (HTTP 422).
        // Referenz: satisfactorymodding/ficsit-cli/ficsit/queries/mod.graphql
        const string query = """
            query Mod($modId: String!) {
              mod: getModByIdOrReference(modIdOrReference: $modId) {
                id
                mod_reference
                name
                short_description
                full_description
                logo
                source_url
                views
                downloads
                popularity
                hotness
                last_version_date
                created_at
                authors { role user { username } }
                compatibility { EA { state note } EXP { state note } }
                versions(filter: {limit: 1, order_by: created_at, order: desc}) {
                  id
                  version
                  link
                  hash
                  size
                  created_at
                }
              }
            }
            """;
        var vars = new Dictionary<string, object?> { ["modId"] = modIdOrRef };
        var data = await ExecuteAsync<GetModData>(query, vars, ct);
        return data?.Mod;
    }

    /// <summary>Ausführung eines GraphQL-Requests. Loggt bei Fehler und liefert
    /// <c>null</c> — die Aufrufer entscheiden ob sie den Toast zeigen.</summary>
    private async Task<T?> ExecuteAsync<T>(string query, Dictionary<string, object?> variables,
        CancellationToken ct) where T : class
    {
        try
        {
            var body = new GraphQlRequest(query, variables);
            using var resp = await _http.PostAsJsonAsync(Endpoint, body, JsonOpts, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var text = await resp.Content.ReadAsStringAsync(ct);
                Log.Warn("ficsit HTTP {Status}: {Body}", (int)resp.StatusCode, text);
                return null;
            }
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var envelope = await JsonSerializer.DeserializeAsync<GraphQlResponse<T>>(stream, JsonOpts, ct);
            if (envelope is null) return null;
            if (envelope.Errors is { Count: > 0 })
            {
                foreach (var err in envelope.Errors)
                    Log.Warn("ficsit GraphQL-Error: {Msg}", err.Message);
                return null;
            }
            return envelope.Data;
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "ficsit GraphQL-Request fehlgeschlagen");
            return null;
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed record GraphQlRequest(
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("variables")] Dictionary<string, object?> Variables);

    private sealed class GraphQlResponse<T>
    {
        [JsonPropertyName("data")] public T? Data { get; set; }
        [JsonPropertyName("errors")] public List<GraphQlError>? Errors { get; set; }
    }

    private sealed class GraphQlError
    {
        [JsonPropertyName("message")] public string Message { get; set; } = "";
    }

    private sealed class GetModsData
    {
        [JsonPropertyName("mods")] public FicsitModsPage? Mods { get; set; }
    }

    private sealed class GetModData
    {
        [JsonPropertyName("mod")] public FicsitModDetail? Mod { get; set; }
    }
}

/// <summary>Antwort von <c>getMods</c>: Count (Gesamt) + Page-Slice.</summary>
public sealed class FicsitModsPage
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("mods")] public List<FicsitCatalogEntry> Mods { get; set; } = new();
}
