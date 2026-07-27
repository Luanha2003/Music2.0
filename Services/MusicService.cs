using Music2._0.Models;

namespace Music2._0.Services;

public sealed class MusicService
{
    private readonly AudiusService _audius;
    private readonly OpenSubsonicService _openSubsonic;
    private readonly SyncedLyricsService _lyrics;
    private readonly YouTubeMusicService _youtube;

    public MusicService(
        AudiusService audius,
        OpenSubsonicService openSubsonic,
        SyncedLyricsService lyrics,
        YouTubeMusicService youtube)
    {
        _audius = audius;
        _openSubsonic = openSubsonic;
        _lyrics = lyrics;
        _youtube = youtube;
    }

    public async Task<object?> GetHomeAsync(
        CancellationToken cancellationToken = default)
    {
        var openSubsonicTask = _openSubsonic.GetRandomSongsAsync(20, cancellationToken);
        var vietnameseTask = _audius.GetVietnameseTracksAsync(cancellationToken);
        var youtubeTask = _youtube.GetVietnameseTracksAsync(cancellationToken);
        var audiusTask = _audius.GetTrendingAsync(30, cancellationToken);
        await Task.WhenAll(
            openSubsonicTask,
            vietnameseTask,
            youtubeTask,
            audiusTask);

        var sections = new List<object>();
        var localTracks = await openSubsonicTask;
        if (localTracks.Count > 0)
        {
            sections.Add(new
            {
                sectionType = "new-release",
                title = "Thư viện OpenSubsonic",
                items = localTracks
            });
        }

        var vietnameseTracks = await vietnameseTask;
        if (vietnameseTracks.Count > 0)
        {
            sections.Add(new
            {
                sectionType = "new-release",
                title = "Nhạc Việt trên Audius",
                items = vietnameseTracks
            });
        }

        var youtubeTracks = await youtubeTask;
        if (youtubeTracks.Count > 0)
        {
            sections.Add(new
            {
                sectionType = "new-release",
                title = "Nhạc Việt bổ sung",
                items = youtubeTracks
            });
        }

        var audiusTracks = await audiusTask;
        if (audiusTracks.Count > 0)
        {
            sections.Add(new
            {
                sectionType = "new-release",
                title = "Audius Trending",
                items = audiusTracks
            });
        }

        return sections.Count == 0
            ? null
            : new { err = 0, data = new { items = sections } };
    }

    public async Task<object?> GetChartsAsync(
        CancellationToken cancellationToken = default)
    {
        var tracks = await _audius.GetTrendingAsync(30, cancellationToken);
        return tracks.Count == 0
            ? null
            : new { err = 0, data = new { RTChart = new { items = tracks } } };
    }

    public async Task<object?> GetNewReleasesAsync(
        CancellationToken cancellationToken = default)
    {
        var tracks = await _audius.GetNewReleasesAsync(30, cancellationToken);
        return tracks.Count == 0
            ? null
            : new { err = 0, data = new { items = tracks } };
    }

    public async Task<object?> GetTop100Async(
        CancellationToken cancellationToken = default)
    {
        var tracks = await _audius.GetTrendingAsync(100, cancellationToken);
        return tracks.Count == 0
            ? null
            : new
            {
                err = 0,
                data = new[]
                {
                    new { title = "Top Audius", items = tracks }
                }
            };
    }

    public async Task<object?> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var openSubsonicTask = _openSubsonic.SearchAsync(query, 30, cancellationToken);
        var audiusTask = _audius.SearchAsync(query, 30, cancellationToken);
        var youtubeTask = _youtube.SearchAsync(query, 40, cancellationToken);
        await Task.WhenAll(openSubsonicTask, audiusTask, youtubeTask);

        // OpenSubsonic is intentionally first: the user's own library has the
        // strongest availability and rights guarantees.
        var songs = (await openSubsonicTask)
            .Concat(await audiusTask)
            .Concat(await youtubeTask)
            .GroupBy(
                track => $"{Normalize(track.Title)}|{Normalize(track.ArtistsNames)}",
                StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(80)
            .ToArray();

        return songs.Length == 0
            ? null
            : new { err = 0, data = new { songs } };
    }

    public async Task<object?> GetSongAsync(
        string encodedId,
        CancellationToken cancellationToken = default)
    {
        if (!ProviderTrackId.TryParse(encodedId, out var provider, out var sourceId))
        {
            return null;
        }

        if (provider == ProviderTrackId.OpenSubsonic)
        {
            if (!_openSubsonic.IsConfigured)
            {
                return null;
            }

            var proxyUrl = $"/api/stream/{Uri.EscapeDataString(encodedId)}";
            return new { err = 0, data = new { @default = proxyUrl } };
        }

        if (provider == ProviderTrackId.Audius)
        {
            var streamUrl = await _audius.GetStreamUrlAsync(sourceId, cancellationToken);
            return string.IsNullOrWhiteSpace(streamUrl)
                ? null
                : new { err = 0, data = new { @default = streamUrl } };
        }

        if (provider == ProviderTrackId.YouTube)
        {
            return new
            {
                err = 0,
                data = new { youtubeVideoId = sourceId }
            };
        }

        return null;
    }

    public async Task<object?> GetSongInfoAsync(
        string encodedId,
        CancellationToken cancellationToken = default)
    {
        var track = await GetTrackAsync(encodedId, cancellationToken);
        return track is null ? null : new { err = 0, data = track };
    }

    public async Task<object> GetLyricsAsync(
        string encodedId,
        CancellationToken cancellationToken = default)
    {
        if (!ProviderTrackId.TryParse(encodedId, out var provider, out var sourceId))
        {
            return EmptyLyrics();
        }

        IReadOnlyList<LyricLine> lyrics = Array.Empty<LyricLine>();
        if (provider == ProviderTrackId.OpenSubsonic)
        {
            lyrics = await _openSubsonic.GetNativeLyricsAsync(
                sourceId,
                cancellationToken);
        }

        if (lyrics.Count == 0)
        {
            var track = await GetTrackAsync(encodedId, cancellationToken);
            if (track is not null)
            {
                lyrics = await _lyrics.GetLyricsAsync(track, cancellationToken);
            }
        }

        var sentences = lyrics.Select(line => new
        {
            words = new[]
            {
                new { data = line.Text, startTime = line.StartTime }
            }
        });

        return new { err = 0, data = new { sentences } };
    }

    public async Task<object> GetProviderStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var audiusTask = _audius.GetTrendingAsync(1, cancellationToken);
        var openSubsonicTask = _openSubsonic.PingAsync(cancellationToken);
        await Task.WhenAll(audiusTask, openSubsonicTask);

        return new
        {
            err = 0,
            data = new
            {
                audius = new
                {
                    configured = true,
                    available = (await audiusTask).Count > 0
                },
                openSubsonic = new
                {
                    configured = _openSubsonic.IsConfigured,
                    available = await openSubsonicTask
                },
                lyrics = new
                {
                    primary = "OpenSubsonic",
                    fallback = "LRCLIB"
                },
                youtube = new
                {
                    role = "catalog-fallback",
                    playback = "iframe"
                }
            }
        };
    }

    private async Task<MusicTrack?> GetTrackAsync(
        string encodedId,
        CancellationToken cancellationToken)
    {
        if (!ProviderTrackId.TryParse(encodedId, out var provider, out var sourceId))
        {
            return null;
        }

        return provider switch
        {
            ProviderTrackId.OpenSubsonic =>
                await _openSubsonic.GetTrackAsync(sourceId, cancellationToken),
            ProviderTrackId.Audius =>
                await _audius.GetTrackAsync(sourceId, cancellationToken),
            ProviderTrackId.YouTube =>
                await _youtube.GetTrackAsync(sourceId, cancellationToken),
            _ => null
        };
    }

    private static object EmptyLyrics()
    {
        return new { err = 0, data = new { sentences = Array.Empty<object>() } };
    }

    private static string Normalize(string value)
    {
        return string.Join(
            ' ',
            value.ToLowerInvariant().Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
