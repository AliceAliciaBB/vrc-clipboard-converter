using VrcClipboardConverter.Logic;
using VrcClipboardConverter.Models;
using Xunit;

namespace VrcClipboardConverter.Tests;

public class StatusPresenterStateTests
{
    [Theory]
    [InlineData(AppStatus.Idle, "icon_idle", "待機中", "待機中")]
    [InlineData(AppStatus.Watching, "icon_watching", "監視中", "監視中")]
    [InlineData(AppStatus.Converting, "icon_converting", "変換中...", "変換中...")]
    [InlineData(AppStatus.Converted, "icon_watching", "監視中", "変換完了(コピー済み)")]
    public void For_ReturnsExpectedDisplay(AppStatus status, string icon, string tooltip, string label)
    {
        var display = StatusPresenterState.For(status);

        Assert.Equal(icon, display.IconResourceName);
        Assert.Equal(tooltip, display.TooltipText);
        Assert.Equal(label, display.LabelText);
    }

    [Fact]
    public void For_Error_IncludesDetailInLabel()
    {
        var display = StatusPresenterState.For(AppStatus.Error, "動画が非公開です");

        Assert.Equal("icon_error", display.IconResourceName);
        Assert.Equal("エラー", display.TooltipText);
        Assert.Equal("エラー: 動画が非公開です", display.LabelText);
    }
}
