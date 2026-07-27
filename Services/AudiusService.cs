using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Music2._0.Models;

namespace Music2._0.Services;

public sealed class AudiusService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly AudiusOptions _options;
    private readonly ILogger<AudiusService> _logger;
    private readonly string[] _apiKeys;
    private int _requestCounter;

    public AudiusService(
        HttpClient http,
        IMemoryCache cache,
        IOptions<AudiusOptions> options,
        ILogger<AudiusService> logger)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
        _apiKeys = _options.ApiKeys
            .Prepend(_options.ApiKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Music2.0/2.0");
    }

    public Task<IReadOnlyList<MusicTrack>> GetTrendingAsync(
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        return GetTracksAsync(
            $"tracks/trending?limit={limit}&time=week",
            $"audius:trending:{limit}",
            TimeSpan.FromMinutes(10),
            cancellationToken);
    }

    public Task<IReadOnlyList<MusicTrack>> GetNewReleasesAsync(
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        // Audius currently exposes the most reliable public release feed through
        // weekly trending. Keep a distinct cache entry so this can be replaced by
        // a dedicated release endpoint without changing callers.
        return GetTracksAsync(
            $"tracks/trending?limit={limit}&time=week",
            $"audius:new-releases:{limit}",
            TimeSpan.FromMinutes(10),
            cancellationToken);
    }

    public Task<IReadOnlyList<MusicTrack>> SearchAsync(
        string query,
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var normalized = query.Trim();
        return GetTracksAsync(
            $"tracks/search?query={Uri.EscapeDataString(normalized)}&limit={limit}",
            $"audius:search:{normalized.ToLowerInvariant()}:{limit}",
            TimeSpan.FromMinutes(5),
            cancellationToken);
    }

    public async Task<IReadOnlyList<MusicTrack>> GetVietnameseTracksAsync(
        CancellationToken cancellationToken = default)
    {
        const string cacheKey = "audius:vietnamese";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<MusicTrack>? cached) &&
            cached is not null)
        {
            return cached;
        }

        var searches = new[]
        {
            new VietnameseSearch("nhac viet", ["viet", "vietnamese", "nhac"]),
            new VietnameseSearch("vpop", ["vpop"]),
            new VietnameseSearch("Son Tung M-TP", ["son tung"]),
            new VietnameseSearch("Hoang Thuy Linh", ["hoang thuy linh"]),
            new VietnameseSearch("Bich Phuong", ["bich phuong"]),
            new VietnameseSearch("Amee Vietnam", ["amee"])
        };

        var tasks = searches
            .Select(search => SearchAsync(search.Query, 15, cancellationToken))
            .ToArray();
        await Task.WhenAll(tasks);

        var tracks = tasks
            .SelectMany((task, index) =>
                task.Result.Where(track => MatchesTerms(track, searches[index].Terms)))
            .GroupBy(track => NormalizeForSearch(track.Title))
            .Select(group => group.First())
            .Take(30)
            .ToArray();

        _cache.Set(cacheKey, tracks, TimeSpan.FromMinutes(15));
        return tracks;
    }

    public async Task<MusicTrack?> GetTrackAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        var result = await GetTrackResultAsync(sourceId, cancellationToken);
        return result?.Track;
    }

    public async Task<string?> GetStreamUrlAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        var result = await GetTrackResultAsync(sourceId, cancellationToken);
        return result?.StreamUrl;
    }

    private async Task<IReadOnlyList<MusicTrack>> GetTracksAsync(
        string relativeUrl,
        string cacheKey,
        TimeSpan cacheDuration,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<MusicTrack>? cached) &&
            cached is not null)
        {
            return cached;
        }

        try
        {
            using var response = await SendWithKeyFallbackAsync(
                relativeUrl,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<MusicTrack>();
            }

            var tracks = data.EnumerateArray()
                .Select(element => MapTrack(element, out _))
                .Where(track => track is not null)
                .Cast<MusicTrack>()
                .ToArray();

            _cache.Set(cacheKey, tracks, cacheDuration);
            return tracks;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audius request failed: {RelativeUrl}", relativeUrl);
            return Array.Empty<MusicTrack>();
        }
    }

    private async Task<AudiusTrackResult?> GetTrackResultAsync(
        string sourceId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"audius:track:{sourceId}";
        if (_cache.TryGetValue(cacheKey, out AudiusTrackResult? cached) &&
            cached is not null)
        {
            return cached;
        }

        try
        {
            using var response = await SendWithKeyFallbackAsync(
                $"tracks/{Uri.EscapeDataString(sourceId)}",
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("data", out var data))
            {
                return null;
            }

            var element = data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray().FirstOrDefault()
                : data;

            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var track = MapTrack(element, out var streamUrl);
            if (track is null)
            {
                return null;
            }

            var result = new AudiusTrackResult(track, streamUrl);
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load Audius track {TrackId}", sourceId);
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendWithKeyFallbackAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        if (_apiKeys.Length == 0)
        {
            using var anonymousRequest = CreateRequest(relativeUrl, null);
            return await _http.SendAsync(anonymousRequest, cancellationToken);
        }

        var startIndex = Math.Abs(
            Interlocked.Increment(ref _requestCounter) % _apiKeys.Length);
        for (var attempt = 0; attempt < _apiKeys.Length; attempt++)
        {
            var keyIndex = (startIndex + attempt) % _apiKeys.Length;
            using var request = CreateRequest(relativeUrl, _apiKeys[keyIndex]);
            var response = await _http.SendAsync(request, cancellationToken);

            if (response.StatusCode is not
                (System.Net.HttpStatusCode.Unauthorized or
                 System.Net.HttpStatusCode.Forbidden or
                 System.Net.HttpStatusCode.TooManyRequests))
            {
                return response;
            }

            if (attempt == _apiKeys.Length - 1)
            {
                return response;
            }

            _logger.LogWarning(
                "Audius key {KeyNumber} returned HTTP {StatusCode}; trying another key",
                keyIndex + 1,
                (int)response.StatusCode);
            response.Dispose();
        }

        throw new HttpRequestException("No Audius API key was accepted.");
    }

    private static HttpRequestMessage CreateRequest(
        string relativeUrl,
        string? apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.TryAddWithoutValidation("X-API-Key", apiKey);
        }

        return request;
    }

    private static MusicTrack? MapTrack(JsonElement element, out string? streamUrl)
    {
        streamUrl = null;

        if (!TryGetString(element, "id", out var id) ||
            !TryGetString(element, "title", out var title))
        {
            return null;
        }

        if (element.TryGetProperty("is_streamable", out var streamable) &&
            streamable.ValueKind == JsonValueKind.False)
        {
            return null;
        }

        var artist = "Audius";
        if (element.TryGetProperty("user", out var user) &&
            user.ValueKind == JsonValueKind.Object &&
            TryGetString(user, "name", out var userName))
        {
            artist = userName;
        }

        string? thumbnail = null;
        if (element.TryGetProperty("artwork", out var artwork) &&
            artwork.ValueKind == JsonValueKind.Object)
        {
            if (!TryGetString(artwork, "480x480", out thumbnail))
            {
                TryGetString(artwork, "1000x1000", out thumbnail);
            }
        }

        if (element.TryGetProperty("stream", out var stream) &&
            stream.ValueKind == JsonValueKind.Object)
        {
            TryGetString(stream, "url", out streamUrl);
        }

        var duration = 0;
        if (element.TryGetProperty("duration", out var durationElement) &&
            durationElement.TryGetInt32(out var seconds))
        {
            duration = seconds;
        }

        TryGetString(element, "album", out var album);

        return new MusicTrack
        {
            EncodeId = ProviderTrackId.Create(ProviderTrackId.Audius, id),
            Title = title,
            ArtistsNames = artist,
            Thumbnail = thumbnail,
            ThumbnailM = thumbnail,
            Duration = duration,
            Artists = [new MusicArtist { Name = artist }],
            Source = ProviderTrackId.Audius,
            Album = album
        };
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

    private static bool MatchesTerms(
        MusicTrack track,
        IReadOnlyList<string> terms)
    {
        var haystack = $"{NormalizeForSearch(track.Title)} " +
                       NormalizeForSearch(track.ArtistsNames);
        return terms.Any(term =>
            haystack.Contains(NormalizeForSearch(term), StringComparison.Ordinal));
    }

    private static string NormalizeForSearch(string value)
    {
        var normalized = value.ToLowerInvariant();
        return string.Join(
            ' ',
            normalized.Split(
                [' ', '-', '_', '|', '/', '\\', '(', ')', '[', ']'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private sealed record VietnameseSearch(
        string Query,
        IReadOnlyList<string> Terms);

    private sealed record AudiusTrackResult(MusicTrack Track, string? StreamUrl);
}
