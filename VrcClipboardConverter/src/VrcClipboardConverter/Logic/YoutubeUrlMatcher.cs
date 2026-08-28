using System.Text.RegularExpressions;

namespace VrcClipboardConverter.Logic;

public static class YoutubeUrlMatcher
{
    private static readonly Regex Pattern = new(
        @"^https?://(www\.)?(youtube\.com/watch\?[^\s]*v=|youtu\.be/)[^\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsYoutubeUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        return Pattern.IsMatch(text.Trim());
    }
}
