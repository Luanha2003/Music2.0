using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Music2._0.Services;

public class ZingMp3Service
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://zingmp3.vn";
    private const string ApiKey = "88265e23d4284f25963e6eedac8fbfa3";
    private const string SecretKey = "2aa2d1c561e809b267f3638c4a307aab";
    private const string Version = "1.6.34";

    // API paths
    private const string PathHome = "/api/v2/page/get/home";
    private const string PathSong = "/api/v2/song/get/streaming";
    private const string PathSongInfo = "/api/v2/song/get/info";
    private const string PathLyric = "/api/v2/lyric/get/lyric";
    private const string PathChartHome = "/api/v2/page/get/chart-home";
    private const string PathNewRelease = "/api/v2/page/get/newrelease-chart";
    private const string PathTop100 = "/api/v2/page/get/top-100";
    private const string PathArtist = "/api/v2/page/get/artist";
    private const string PathPlaylist = "/api/v2/page/get/playlist";
    private const string PathSearch = "/api/v2/search/multi";
    private const string PathSuggestion = "/api/v2/app/get/suggest-keyword";
    private const string PathHubHome = "/api/v2/page/get/hub-home";
    private const string PathHubDetail = "/api/v2/page/get/hub-detail";

    private string? _cookie;

    public ZingMp3Service(HttpClient http)
    {
        _http = http;
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Referer", "https://zingmp3.vn/");
    }

    // ── Crypto helpers ──
    private static string HashSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    private static string HmacSha512(string data, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var hash = HMACSHA512.HashData(keyBytes, dataBytes);
        return Convert.ToHexStringLower(hash);
    }

    // ── Signature variants matching the JS source ──
    private static string GetCtime() =>
        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

    // For endpoints WITHOUT id (e.g. chart, top100, search, artist)
    private static string HashParamNoId(string path, string ctime)
    {
        var sha = HashSha256($"ctime={ctime}version={Version}");
        return HmacSha512(path + sha, SecretKey);
    }

    // For endpoints WITH id (e.g. song, playlist, lyric)
    private static string HashParamWithId(string path, string id, string ctime)
    {
        var sha = HashSha256($"ctime={ctime}id={id}version={Version}");
        return HmacSha512(path + sha, SecretKey);
    }

    // For home endpoint specifically
    private static string HashParamHome(string path, string ctime)
    {
        var sha = HashSha256($"count=30ctime={ctime}page=1version={Version}");
        return HmacSha512(path + sha, SecretKey);
    }

    // ── Get cookie from zingmp3.vn ──
    private async Task EnsureCookieAsync()
    {
        if (!string.IsNullOrEmpty(_cookie)) return;

        try
        {
            var handler = new HttpClientHandler { UseCookies = true };
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            var response = await client.GetAsync(BaseUrl);
            
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                _cookie = string.Join("; ", cookies.Select(c => c.Split(';')[0]));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZingMp3] Cookie error: {ex.Message}");
        }
    }

    // ── Generic request ──
    private async Task<JsonElement?> RequestAsync(string apiPath, Dictionary<string, string> queryParams)
    {
        await EnsureCookieAsync();

        var sb = new StringBuilder(apiPath);
        sb.Append('?');

        var allParams = new Dictionary<string, string>(queryParams)
        {
            ["ctime"] = queryParams.ContainsKey("ctime") ? queryParams["ctime"] : GetCtime(),
            ["version"] = Version,
            ["apiKey"] = ApiKey
        };

        var first = true;
        foreach (var (k, v) in allParams)
        {
            if (!first) sb.Append('&');
            sb.Append($"{k}={Uri.EscapeDataString(v)}");
            first = false;
        }

        var url = BaseUrl + sb.ToString();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(_cookie))
            {
                request.Headers.Add("Cookie", _cookie);
            }

            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ZingMp3] HTTP {(int)response.StatusCode} from {apiPath}");
                return null;
            }

            var result = JsonSerializer.Deserialize<JsonElement>(body);
            
            // Log API errors
            if (result.TryGetProperty("err", out var errProp) && errProp.GetInt32() != 0)
            {
                Console.WriteLine($"[ZingMp3] API error {errProp.GetInt32()} from {apiPath}: " +
                    (result.TryGetProperty("msg", out var msgProp) ? msgProp.GetString() : "unknown"));
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZingMp3] Error calling {apiPath}: {ex.Message}");
            return null;
        }
    }

    // ═══ Public API Methods ═══

    public Task<JsonElement?> GetHome()
    {
        var ctime = GetCtime();
        return RequestAsync(PathHome, new Dictionary<string, string>
        {
            ["page"] = "1",
            ["segmentId"] = "-1",
            ["count"] = "30",
            ["sig"] = HashParamHome(PathHome, ctime),
            ["ctime"] = ctime
        });
    }

    public Task<JsonElement?> GetSong(string id)
    {
        var ctime = GetCtime();
        return RequestAsync(PathSong, new Dictionary<string, string>
        {
            ["id"] = id,
            ["sig"] = HashParamWithId(PathSong, id, ctime),
            ["ctime"] = ctime
        });
    }

    public Task<JsonElement?> GetSongInfo(string id)
    {
        var ctime = GetCtime();
        return RequestAsync(PathSongInfo, new Dictionary<string, string>
        {
            ["id"] = id,
            ["sig"] = HashParamWithId(PathSongInfo, id, ctime),
            ["ctime"] = ctime
        });
    }

    public Task<JsonElement?> GetLyric(string id)
    {
        var ctime = GetCtime();
        return RequestAsync(PathLyric, new Dictionary<string, string>
        {
            ["id"] = id,
            ["sig"] = HashParamWithId(PathLyric, id, ctime),
            ["ctime"] = ctime
        });
    }

    public Task<JsonElement?> GetChartHome()
    {
        var ctime = GetCtime();
        return RequestAsync(PathChartHome, new Dictionary<string, string>
        {
            ["sig"] = HashParamNoId(PathChartHome, ctime),
            ["ctime"] = ctime
        });
    }

    public Task<JsonElement?> GetNewReleaseChart()
    {
        var ctime = GetCtime();
        return RequestAsync(PathNewRelease, new Dictionary<string, string>
        {
            ["sig"] = HashParamNoId(PathNewRelease, ctime),
            ["ctime"] = ctime
        });
    }

    public Task<JsonElement?> GetTop100()
    {
        var ctime = GetCtime();
        return RequestAsync(PathTop100, new Dictionary<string, string>
        {
            ["sig"] = HashParamNoId(PathTop100, ctime),
            ["ctime"] = ctime
        });
    }

    public Task<JsonElement?> GetArtist(string alias)
    {
        var ctime = GetCtime();
        return RequestAsync(PathArtist, new Dictionary<string, string>
        {
            ["alias"] = alias,
            ["sig"] = HashParamNoId(PathArtist, ctime),
            ["ctime"] = ctime
        });
    }

    public Task<JsonElement?> GetPlaylist(string id)
    {
        var ctime = GetCtime();
        return RequestAsync(PathPlaylist, new Dictionary<string, string>
        {
            ["id"] = id,
            ["sig"] = HashParamWithId(PathPlaylist, id, ctime),
            ["ctime"] = ctime
        });
    }

    public Task<JsonElement?> SearchAll(string keyword)
    {
        var ctime = GetCtime();
        return RequestAsync(PathSearch, new Dictionary<string, string>
        {
            ["q"] = keyword,
            ["sig"] = HashParamNoId(PathSearch, ctime),
            ["ctime"] = ctime
        });
    }

    public Task<JsonElement?> GetSuggestion(string keyword)
    {
        var ctime = GetCtime();
        return RequestAsync(PathSuggestion, new Dictionary<string, string>
        {
            ["keyword"] = keyword,
            ["sig"] = HashParamNoId(PathSuggestion, ctime),
            ["ctime"] = ctime
        });
    }

    public Task<JsonElement?> GetHubHome()
    {
        var ctime = GetCtime();
        return RequestAsync(PathHubHome, new Dictionary<string, string>
        {
            ["sig"] = HashParamNoId(PathHubHome, ctime),
            ["ctime"] = ctime
        });
    }

    public Task<JsonElement?> GetHubDetail(string id)
    {
        var ctime = GetCtime();
        return RequestAsync(PathHubDetail, new Dictionary<string, string>
        {
            ["id"] = id,
            ["sig"] = HashParamWithId(PathHubDetail, id, ctime),
            ["ctime"] = ctime
        });
    }
}
