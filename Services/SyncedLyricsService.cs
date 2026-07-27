using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Music2._0.Models;

namespace Music2._0.Services;

public sealed partial class SyncedLyricsService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SyncedLyricsService> _logger;

    public SyncedLyricsService(
        HttpClient http,
        IMemoryCache cache,
        ILogger<SyncedLyricsService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        _http.BaseAddress = new Uri("https://lrclib.net/");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Music2.0/2.0 (local music player)");
        _http.DefaultRequestHeaders.TryAddWithoutValidation(
            "Lrclib-Client",
            "Music2.0/2.0");
    }

    public async Task<IReadOnlyList<LyricLine>> GetLyricsAsync(
        MusicTrack track,
        CancellationToken cancellationToken = default)
    {
        var searchTitle = GetSearchTitle(track.Title, track.ArtistsNames);
        var cacheKey =
            $"lyrics:v2:{Normalize(searchTitle)}:{Normalize(track.ArtistsNames)}:{track.Duration}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<LyricLine>? cached) &&
            cached is not null)
        {
            return cached;
        }

        try
        {
            var strictUrl =
                $"api/search?track_name={Uri.EscapeDataString(searchTitle)}" +
                $"&artist_name={Uri.EscapeDataString(track.ArtistsNames)}";
            var candidates = await SearchAsync(strictUrl, cancellationToken);

            if (!candidates.Any(HasSyncedLyrics))
            {
                var fallbackUrl =
                    $"api/search?q={Uri.EscapeDataString(
                        $"{searchTitle} {track.ArtistsNames}")}";
                candidates.AddRange(
                    await SearchAsync(fallbackUrl, cancellationToken));
            }

            if (!candidates.Any(HasSyncedLyrics) &&
                !string.Equals(searchTitle, track.Title, StringComparison.Ordinal))
            {
                var originalTitleUrl =
                    $"api/search?q={Uri.EscapeDataString(track.Title)}";
                candidates.AddRange(
                    await SearchAsync(originalTitleUrl, cancellationToken));
            }

            var best = candidates
                .Where(HasSyncedLyrics)
                .Select(item => new
                {
                    Item = item,
                    Score = Score(item, track, searchTitle)
                })
                .OrderByDescending(candidate => candidate.Score)
                .FirstOrDefault();

            if (best is null ||
                !best.Item.TryGetProperty("syncedLyrics", out var syncedLyrics) ||
                syncedLyrics.ValueKind != JsonValueKind.String)
            {
                _cache.Set(cacheKey, Array.Empty<LyricLine>(), TimeSpan.FromMinutes(30));
                return Array.Empty<LyricLine>();
            }

            var parsed = ParseLrc(syncedLyrics.GetString());
            _cache.Set(
                cacheKey,
                parsed,
                parsed.Count > 0 ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(30));
            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not load lyrics for {Title} - {Artist}",
                track.Title,
                track.ArtistsNames);
            return Array.Empty<LyricLine>();
        }
    }

    private async Task<List<JsonElement>> SearchAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(relativeUrl, cancellationToken);
        if ((int)response.StatusCode == 429)
        {
            _logger.LogWarning(
                "LRCLIB rate limit reached. Retry-After: {RetryAfter}",
                response.Headers.RetryAfter?.ToString());
            return [];
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        return document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray()
                .Select(item => item.Clone())
                .ToList()
            : [];
    }

    private static bool HasSyncedLyrics(JsonElement item)
    {
        return item.TryGetProperty("syncedLyrics", out var lyrics) &&
               lyrics.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(lyrics.GetString());
    }

    private static int Score(
        JsonElement item,
        MusicTrack track,
        string searchTitle)
    {
        var score = 0;
        var title = item.TryGetProperty("trackName", out var titleElement)
            ? titleElement.GetString()
            : null;
        var artist = item.TryGetProperty("artistName", out var artistElement)
            ? artistElement.GetString()
            : null;

        var wantedTitle = Normalize(searchTitle);
        var candidateTitle = Normalize(title);
        if (candidateTitle == wantedTitle)
        {
            score += 100;
        }
        else if (candidateTitle.Contains(wantedTitle, StringComparison.Ordinal) ||
                 wantedTitle.Contains(candidateTitle, StringComparison.Ordinal))
        {
            score += 50;
        }

        var wantedArtist = Normalize(track.ArtistsNames);
        var candidateArtist = Normalize(artist);
        if (candidateArtist == wantedArtist)
        {
            score += 60;
        }
        else if (candidateArtist.Contains(wantedArtist, StringComparison.Ordinal) ||
                 wantedArtist.Contains(candidateArtist, StringComparison.Ordinal))
        {
            score += 30;
        }
        else if (wantedTitle.Contains(candidateArtist, StringComparison.Ordinal))
        {
            score += 25;
        }

        if (track.Duration > 0 &&
            item.TryGetProperty("duration", out var durationElement) &&
            durationElement.TryGetDouble(out var duration))
        {
            var difference = Math.Abs(duration - track.Duration);
            score += difference switch
            {
                <= 2 => 40,
                <= 5 => 20,
                <= 15 => 5,
                _ => 0
            };
        }

        return score;
    }

    private static IReadOnlyList<LyricLine> ParseLrc(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc))
        {
            return Array.Empty<LyricLine>();
        }

        var result = new List<LyricLine>();
        foreach (var rawLine in lrc.Split('\n'))
        {
            var match = LrcLineRegex().Match(rawLine.TrimEnd('\r'));
            if (!match.Success)
            {
                continue;
            }

            var minutes = long.Parse(
                match.Groups["minutes"].Value,
                CultureInfo.InvariantCulture);
            var seconds = long.Parse(
                match.Groups["seconds"].Value,
                CultureInfo.InvariantCulture);
            var fractionText = match.Groups["fraction"].Value;
            var milliseconds = fractionText.Length == 0
                ? 0
                : long.Parse(
                    fractionText.PadRight(3, '0')[..3],
                    CultureInfo.InvariantCulture);
            var text = match.Groups["text"].Value.Trim();

            if (text.Length > 0)
            {
                result.Add(new LyricLine
                {
                    Text = text,
                    StartTime =
                        (minutes * 60_000) + (seconds * 1_000) + milliseconds
                });
            }
        }

        return result
            .OrderBy(line => line.StartTime)
            .GroupBy(line => line.StartTime)
            .Select(group => group.First())
            .ToArray();
    }

    private static string GetSearchTitle(string title, string artist)
    {
        var cleaned = VersionDescriptorRegex().Replace(title, " ").Trim();
        cleaned = TrailingDescriptorRegex().Replace(cleaned, string.Empty).Trim();

        if (!string.IsNullOrWhiteSpace(artist))
        {
            cleaned = Regex.Replace(
                    cleaned,
                    $@"^\s*{Regex.Escape(artist)}\s*[-–—|:]\s*",
                    string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Trim();
        }

        return string.IsNullOrWhiteSpace(cleaned) ? title : cleaned;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return string.Join(
            ' ',
            builder.ToString().Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    [GeneratedRegex(
        @"^\[(?<minutes>\d{1,3}):(?<seconds>\d{2})(?:\.(?<fraction>\d{1,3}))?\]\s*(?<text>.*)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex LrcLineRegex();

    [GeneratedRegex(
        @"\s*[\(\[]\s*(?:official\s+)?(?:music\s+)?(?:video|audio|mv|lyrics?(?:\s+video)?|visuali[sz]er|performance|karaoke|vietsub)[^\)\]]*[\)\]]\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionDescriptorRegex();

    [GeneratedRegex(
        @"\s*[-|]\s*(?:official\s+)?(?:music\s+)?(?:video|audio|mv|lyrics?|visuali[sz]er|performance|karaoke|vietsub).*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrailingDescriptorRegex();
}
