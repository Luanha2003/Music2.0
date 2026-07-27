using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Music2._0.Services;

public static class ProviderTrackId
{
    public const string Audius = "audius";
    public const string OpenSubsonic = "opensubsonic";
    public const string OpenSubsonicCover = "opensubsonic-cover";
    public const string YouTube = "youtube";

    public static string Create(string provider, string sourceId)
    {
        var encodedId = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(sourceId));
        return $"{provider}:{encodedId}";
    }

    public static bool TryParse(string value, out string provider, out string sourceId)
    {
        provider = string.Empty;
        sourceId = string.Empty;

        var separator = value.IndexOf(':');
        if (separator <= 0 || separator == value.Length - 1)
        {
            return false;
        }

        provider = value[..separator];

        try
        {
            sourceId = Encoding.UTF8.GetString(
                WebEncoders.Base64UrlDecode(value[(separator + 1)..]));
            return !string.IsNullOrWhiteSpace(sourceId);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
