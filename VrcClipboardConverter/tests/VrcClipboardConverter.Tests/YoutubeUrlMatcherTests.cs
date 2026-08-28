using VrcClipboardConverter.Logic;
using Xunit;

namespace VrcClipboardConverter.Tests;

public class YoutubeUrlMatcherTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", true)]
    [InlineData("https://youtu.be/EDzLCP-zRvA", true)]
    [InlineData("http://youtube.com/watch?v=abc", true)]
    [InlineData("https://example.com/watch?v=abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    [InlineData("https://rr4---sn-jojp-obvel.googlevideo.com/videoplayback?itag=18", false)]
    public void IsYoutubeUrl_ReturnsExpected(string? text, bool expected)
    {
        var result = YoutubeUrlMatcher.IsYoutubeUrl(text);
        Assert.Equal(expected, result);
    }
}
