namespace VrcClipboardConverter.Logic;

public static class YtDlpArgs
{
    public static string[] Build(string url)
    {
        return new[]
        {
            "-g",
            "-f", "18",
            "--extractor-args", "youtube:player_client=android",
            url
        };
    }
}
