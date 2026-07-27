namespace Music2._0.Services;

public sealed class AudiusOptions
{
    public const string SectionName = "Audius";

    public string BaseUrl { get; set; } = "https://api.audius.co/v1/";
    public string ApiKey { get; set; } = string.Empty;
    public string[] ApiKeys { get; set; } = [];
}

public sealed class OpenSubsonicOptions
{
    public const string SectionName = "OpenSubsonic";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
