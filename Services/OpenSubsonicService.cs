using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Music2._0.Models;

namespace Music2._0.Services;

public sealed class OpenSubsonicService
{
    private const string ClientName = "Music2.0";
    private const string ApiVersion = "1.16.1";

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly OpenSubsonicOptions _options;
    private readonly ILogger<OpenSubsonicService> _logger;

    public OpenSubsonicService(
        HttpClient http,
        IMemoryCache cache,
        IOptions<OpenSubsonicOptions> options,
        ILogger<OpenSubsonicService> logger)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _logger = logger;

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Music2.0/2.0");
    }

    public bool IsConfigured =>
        Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _) &&
        (!string.IsNullOrWhiteSpace(_options.ApiKey) ||
         (!string.IsNullOrWhiteSpace(_options.Username) &&
          !string.IsNullOrWhiteSpace(_options.Password)));

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        const string cacheKey = "opensubsonic:ping";
        if (_cache.TryGetValue(cacheKey, out bool cached))
        {
            return cached;
        }

        var response = await GetJsonResponseAsync(
            "ping",
            null,
            cancellationToken,
            logFailure: false);
        var available = response is not null;
        _cache.Set(cacheKey, available, TimeSpan.FromSeconds(30));
        return available;
    }

    public async Task<IReadOnlyList<MusicTrack>> GetRandomSongsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Array.Empty<MusicTrack>();
        }

        limit = Math.Clamp(limit, 1, 500);
        var cacheKey = $"opensubsonic:random:{limit}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<MusicTrack>? cached) &&
            cached is not null)
        {
            return cached;
        }

        var root = await GetJsonResponseAsync(
            "getRandomSongs",
            new Dictionary<string, string?> { ["size"] = limit.ToString() },
            cancellationToken);

        var tracks = MapSongCollection(root, "randomSongs");
        _cache.Set(cacheKey, tracks, TimeSpan.FromMinutes(5));
        return tracks;
    }

    public async Task<IReadOnlyList<MusicTrack>> SearchAsync(
        string query,
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Array.Empty<MusicTrack>();
        }

        limit = Math.Clamp(limit, 1, 500);
        var normalized = query.Trim();
        var cacheKey = $"opensubsonic:search:{normalized.ToLowerInvariant()}:{limit}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<MusicTrack>? cached) &&
            cached is not null)
        {
            return cached;
        }

        var root = await GetJsonResponseAsync(
            "search3",
            new Dictionary<string, string?>
            {
                ["query"] = normalized,
                ["artistCount"] = "0",
                ["albumCount"] = "0",
                ["songCount"] = limit.ToString()
            },
            cancellationToken);

        var tracks = MapSongCollection(root, "searchResult3");
        _cache.Set(cacheKey, tracks, TimeSpan.FromMinutes(2));
        return tracks;
    }

    public async Task<MusicTrack?> GetTrackAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var cacheKey = $"opensubsonic:track:{sourceId}";
        if (_cache.TryGetValue(cacheKey, out MusicTrack? cached) &&
            cached is not null)
        {
            return cached;
        }

        var root = await GetJsonResponseAsync(
            "getSong",
            new Dictionary<string, string?> { ["id"] = sourceId },
            cancellationToken);

        if (root is null ||
            !root.Value.TryGetProperty("song", out var song) ||
            song.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var track = MapSong(song);
        if (track is not null)
        {
            _cache.Set(cacheKey, track, TimeSpan.FromMinutes(30));
        }

        return track;
    }

    public async Task<IReadOnlyList<LyricLine>> GetNativeLyricsAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Array.Empty<LyricLine>();
        }

        var cacheKey = $"opensubsonic:lyrics:{sourceId}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<LyricLine>? cached) &&
            cached is not null)
        {
            return cached;
        }

        var root = await GetJsonResponseAsync(
            "getLyricsBySongId",
            new Dictionary<string, string?> { ["id"] = sourceId },
            cancellationToken,
            logFailure: false);

        var lyrics = ParseStructuredLyrics(root);
        _cache.Set(cacheKey, lyrics, TimeSpan.FromHours(24));
        return lyrics;
    }

    public Task<HttpResponseMessage?> GetStreamResponseAsync(
        string sourceId,
        string? range,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["id"] = sourceId,
            ["format"] = "raw"
        };

        return SendBinaryRequestAsync(
            "stream",
            parameters,
            range,
            cancellationToken);
    }

    public Task<HttpResponseMessage?> GetCoverResponseAsync(
        string coverArtId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["id"] = coverArtId,
            ["size"] = "500"
        };

        return SendBinaryRequestAsync(
            "getCoverArt",
            parameters,
            null,
            cancellationToken);
    }

    private async Task<HttpResponseMessage?> SendBinaryRequestAsync(
        string endpoint,
        IDictionary<string, string?> parameters,
        string? range,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUrl(endpoint, parameters));

        if (!string.IsNullOrWhiteSpace(range))
        {
            request.Headers.TryAddWithoutValidation("Range", range);
        }

        try
        {
            var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            request.Dispose();
            return response;
        }
        catch (Exception ex)
        {
            request.Dispose();
            _logger.LogWarning(ex, "OpenSubsonic binary request failed: {Endpoint}", endpoint);
            return null;
        }
    }

    private async Task<JsonElement?> GetJsonResponseAsync(
        string endpoint,
        IDictionary<string, string?>? parameters,
        CancellationToken cancellationToken,
        bool logFailure = true)
    {
        if (!IsConfigured)
        {
            return null;
        }

        try
        {
            using var response = await _http.GetAsync(
                BuildUrl(endpoint, parameters),
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("subsonic-response", out var root) ||
                root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty("status", out var status) &&
                status.ValueKind == JsonValueKind.String &&
                !string.Equals(status.GetString(), "ok", StringComparison.OrdinalIgnoreCase))
            {
                if (logFailure)
                {
                    var message = root.TryGetProperty("error", out var error) &&
                                  error.TryGetProperty("message", out var errorMessage)
                        ? errorMessage.GetString()
                        : "Unknown OpenSubsonic error";
                    _logger.LogWarning(
                        "OpenSubsonic endpoint {Endpoint} failed: {Message}",
                        endpoint,
                        message);
                }

                return null;
            }

            return root.Clone();
        }
        catch (Exception ex)
        {
            if (logFailure)
            {
                _logger.LogWarning(ex, "OpenSubsonic request failed: {Endpoint}", endpoint);
            }

            return null;
        }
    }

    private string BuildUrl(
        string endpoint,
        IDictionary<string, string?>? parameters)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        if (baseUrl.EndsWith("/rest", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = baseUrl[..^5];
        }

        var url = $"{baseUrl}/rest/{endpoint}.view";
        var query = new Dictionary<string, string?>
        {
            ["v"] = ApiVersion,
            ["c"] = ClientName,
            ["f"] = "json"
        };

        AddAuthentication(query);

        if (parameters is not null)
        {
            foreach (var parameter in parameters)
            {
                query[parameter.Key] = parameter.Value;
            }
        }

        return QueryHelpers.AddQueryString(url, query);
    }

    private void AddAuthentication(IDictionary<string, string?> query)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            query["apiKey"] = _options.ApiKey;
            return;
        }

        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(8))
            .ToLowerInvariant();
        var input = Encoding.UTF8.GetBytes(_options.Password + salt);
        var token = Convert.ToHexString(MD5.HashData(input)).ToLowerInvariant();

        query["u"] = _options.Username;
        query["t"] = token;
        query["s"] = salt;
    }

    private static IReadOnlyList<MusicTrack> MapSongCollection(
        JsonElement? root,
        string collectionName)
    {
        if (root is null ||
            !root.Value.TryGetProperty(collectionName, out var collection) ||
            collection.ValueKind != JsonValueKind.Object ||
            !collection.TryGetProperty("song", out var songs) ||
            songs.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MusicTrack>();
        }

        return songs.EnumerateArray()
            .Select(MapSong)
            .Where(track => track is not null)
            .Cast<MusicTrack>()
            .ToArray();
    }

    private static MusicTrack? MapSong(JsonElement song)
    {
        if (!TryGetString(song, "id", out var id) ||
            !TryGetString(song, "title", out var title))
        {
            return null;
        }

        var artist = TryGetString(song, "artist", out var artistValue)
            ? artistValue
            : "Không rõ nghệ sĩ";
        var album = TryGetString(song, "album", out var albumValue)
            ? albumValue
            : null;

        var duration = 0;
        if (song.TryGetProperty("duration", out var durationElement) &&
            durationElement.TryGetInt32(out var seconds))
        {
            duration = seconds;
        }

        string? thumbnail = null;
        if (TryGetString(song, "coverArt", out var coverArt))
        {
            var coverId = ProviderTrackId.Create(
                ProviderTrackId.OpenSubsonicCover,
                coverArt);
            thumbnail = $"/api/cover/{coverId}";
        }

        return new MusicTrack
        {
            EncodeId = ProviderTrackId.Create(ProviderTrackId.OpenSubsonic, id),
            Title = title,
            ArtistsNames = artist,
            Thumbnail = thumbnail,
            ThumbnailM = thumbnail,
            Duration = duration,
            Artists = [new MusicArtist { Name = artist }],
            Source = ProviderTrackId.OpenSubsonic,
            Album = album
        };
    }

    private static IReadOnlyList<LyricLine> ParseStructuredLyrics(JsonElement? root)
    {
        if (root is null ||
            !root.Value.TryGetProperty("lyricsList", out var lyricsList) ||
            lyricsList.ValueKind != JsonValueKind.Object ||
            !lyricsList.TryGetProperty("structuredLyrics", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<LyricLine>();
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.TryGetProperty("kind", out var kind) &&
                kind.ValueKind == JsonValueKind.String &&
                !string.Equals(kind.GetString(), "main", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!entry.TryGetProperty("synced", out var synced) ||
                synced.ValueKind != JsonValueKind.True ||
                !entry.TryGetProperty("line", out var lines) ||
                lines.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var offset = 0L;
            if (entry.TryGetProperty("offset", out var offsetElement) &&
                offsetElement.TryGetInt64(out var parsedOffset))
            {
                offset = parsedOffset;
            }

            var parsed = new List<LyricLine>();
            foreach (var line in lines.EnumerateArray())
            {
                if (!line.TryGetProperty("start", out var startElement) ||
                    !startElement.TryGetInt64(out var start) ||
                    !TryGetString(line, "value", out var value) ||
                    string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                parsed.Add(new LyricLine
                {
                    Text = value.Trim(),
                    StartTime = Math.Max(0, start + offset)
                });
            }

            if (parsed.Count > 0)
            {
                return parsed.OrderBy(line => line.StartTime).ToArray();
            }
        }

        return Array.Empty<LyricLine>();
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return value.Length > 0;
    }
}
