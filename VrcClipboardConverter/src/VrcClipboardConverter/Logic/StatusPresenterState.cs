using VrcClipboardConverter.Models;

namespace VrcClipboardConverter.Logic;

public record StatusDisplay(string IconResourceName, string TooltipText, string LabelText);

public static class StatusPresenterState
{
    public static StatusDisplay For(AppStatus status, string? errorDetail = null)
    {
        return status switch
        {
            AppStatus.Idle => new StatusDisplay("icon_idle", "待機中", "待機中"),
            AppStatus.Watching => new StatusDisplay("icon_watching", "監視中", "監視中"),
            AppStatus.Converting => new StatusDisplay("icon_converting", "変換中...", "変換中..."),
            AppStatus.Converted => new StatusDisplay("icon_watching", "監視中", "変換完了(コピー済み)"),
            AppStatus.Error => new StatusDisplay("icon_error", "エラー", $"エラー: {errorDetail}"),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }
}
