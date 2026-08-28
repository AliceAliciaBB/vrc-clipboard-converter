using VrcClipboardConverter.Logic;
using Xunit;

namespace VrcClipboardConverter.Tests;

public class YtDlpArgsTests
{
    [Fact]
    public void Build_ReturnsFixedFormatAndAndroidClient()
    {
        var args = YtDlpArgs.Build("https://youtu.be/EDzLCP-zRvA");

        Assert.Equal(new[]
        {
            "-g",
            "-f", "18",
            "--extractor-args", "youtube:player_client=android",
            "https://youtu.be/EDzLCP-zRvA"
        }, args);
    }
}
