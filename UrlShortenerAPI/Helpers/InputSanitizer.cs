using System.Text.RegularExpressions;
using UrlShortenerAPI.Models;

namespace UrlShortenerAPI.Helpers
{
    public static class InputSanitizer
    {
        public static string SanitizeAlias(string alias)
        {
            return Regex.Replace(alias ?? "", @"[^a-zA-Z0-9\-_]", "");
        }
        public static string SanitizeUrl(string url)
        {
            url = url?.Trim() ?? "";
            return Regex.Replace(url, @"[^\u0020-\u007E]+", "");
        }
    }
}
