using System.Text.Json;
using System.Text.Json.Serialization;

namespace Music2._0.Services;

public class SoundCloudService
{
    private readonly HttpClient _http;
    private const string ClientId = "u2ydppvwXCUxV6VITwH4OXk8JBySpoNr";
    private const string BaseUrl = "https://api-v2.soundcloud.com";

    public SoundCloudService(HttpClient http)
    {
        _http = http;
        _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    private async Task<JsonElement?> GetAsync(string path, Dictionary<string, string> queryParams)
    {
        var query = string.Join("&", queryParams.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        var url = $"{BaseUrl}{path}?client_id={ClientId}&{query}";

        try
        {
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SoundCloud] Error: {ex.Message}");
            return null;
        }
    }

    public async Task<object?> GetHome()
    {
        // SoundCloud doesn't have a direct "Home" like Zing, so we fetch trending tracks
        var data = await GetAsync("/search/tracks", new Dictionary<string, string>
        {
            ["q"] = "trending",
            ["limit"] = "20"
        });

        if (data == null) return null;

        // Map to Zing Home Structure
        var items = new List<object>();
        if (data.Value.TryGetProperty("collection", out var collection))
        {
            var tracks = collection.EnumerateArray().Select(MapTrack).ToList();
            items.Add(new
            {
                sectionType = "new-release",
                title = "SoundCloud Trending",
                items = tracks
            });
        }

        return new { err = 0, data = new { items = items } };
    }

    public async Task<object?> Search(string q)
    {
        var data = await GetAsync("/search/tracks", new Dictionary<string, string>
        {
            ["q"] = q,
            ["limit"] = "20"
        });

        if (data == null) return null;

        var songs = new List<object>();
        if (data.Value.TryGetProperty("collection", out var collection))
        {
            songs = collection.EnumerateArray().Select(MapTrack).ToList();
        }

        return new { err = 0, data = new { songs = songs } };
    }

    public async Task<object?> GetSong(string id)
    {
        // id here is the SoundCloud track ID or permalink
        // We fetch the track info to get the streaming URL
        var data = await GetAsync($"/tracks/{id}", new Dictionary<string, string>());
        if (data == null) return null;

        if (data.Value.TryGetProperty("media", out var media) && 
            media.TryGetProperty("transcodings", out var transcodings))
        {
            // Try to find progressive mp3 first, otherwise HLS
            var transcoding = transcodings.EnumerateArray()
                .FirstOrDefault(t => t.GetProperty("format").GetProperty("protocol").GetString() == "progressive");
            
            if (transcoding.ValueKind == JsonValueKind.Undefined)
            {
                 transcoding = transcodings.EnumerateArray()
                    .FirstOrDefault(t => t.GetProperty("format").GetProperty("protocol").GetString() == "hls");
            }

            if (transcoding.ValueKind != JsonValueKind.Undefined)
            {
                var streamUrlResponse = await _http.GetAsync($"{transcoding.GetProperty("url").GetString()}?client_id={ClientId}");
                if (streamUrlResponse.IsSuccessStatusCode)
                {
                    var streamBody = await streamUrlResponse.Content.ReadAsStringAsync();
                    var streamJson = JsonSerializer.Deserialize<JsonElement>(streamBody);
                    if (streamJson.TryGetProperty("url", out var finalUrl))
                    {
                        return new { err = 0, data = new { @default = finalUrl.GetString() } };
                    }
                }
            }
        }

        return new { err = -1, msg = "Common error" };
    }

    public async Task<object?> GetSongInfo(string id)
    {
        var data = await GetAsync($"/tracks/{id}", new Dictionary<string, string>());
        if (data == null) return null;
        return new { err = 0, data = MapTrack(data.Value) };
    }

    public async Task<object?> GetCharts()
    {
        var data = await GetAsync("/search/tracks", new Dictionary<string, string>
        {
            ["q"] = "trending hits",
            ["limit"] = "20"
        });

        if (data == null) return null;

        var tracks = new List<object>();
        if (data.Value.TryGetProperty("collection", out var collection))
        {
            tracks = collection.EnumerateArray().Select(MapTrack).ToList();
        }

        return new { err = 0, data = new { RTChart = new { items = tracks } } };
    }

    public async Task<object?> GetNewRelease()
    {
        var data = await GetAsync("/search/tracks", new Dictionary<string, string>
        {
            ["q"] = "new release pop",
            ["limit"] = "30"
        });

        if (data == null) return null;

        var tracks = new List<object>();
        if (data.Value.TryGetProperty("collection", out var collection))
        {
            tracks = collection.EnumerateArray().Select(MapTrack).ToList();
        }

        return new { err = 0, data = tracks };
    }

    public async Task<object?> GetTop100()
    {
        var data = await GetAsync("/search/tracks", new Dictionary<string, string>
        {
            ["q"] = "top 100",
            ["limit"] = "100"
        });

        if (data == null) return null;

        var tracks = new List<object>();
        if (data.Value.TryGetProperty("collection", out var collection))
        {
            tracks = collection.EnumerateArray().Select(MapTrack).ToList();
        }

        var groups = new[]
        {
            new { title = "Top 100 SoundCloud", items = tracks }
        };

        return new { err = 0, data = groups };
    }

    private object MapTrack(JsonElement track)
    {
        return new
        {
            encodeId = track.GetProperty("id").ToString(),
            title = track.GetProperty("title").GetString(),
            artistsNames = track.GetProperty("user").GetProperty("username").GetString(),
            thumbnail = track.TryGetProperty("artwork_url", out var art) && art.ValueKind == JsonValueKind.String 
                ? art.GetString()?.Replace("-large", "-t500x500") 
                : track.GetProperty("user").GetProperty("avatar_url").GetString(),
            thumbnailM = track.TryGetProperty("artwork_url", out var artM) && artM.ValueKind == JsonValueKind.String 
                ? artM.GetString()?.Replace("-large", "-t500x500") 
                : track.GetProperty("user").GetProperty("avatar_url").GetString(),
            duration = (int)(track.GetProperty("duration").GetInt64() / 1000),
            artists = new[] { new { name = track.GetProperty("user").GetProperty("username").GetString() } }
        };
    }
}
