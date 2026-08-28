using VrcClipboardConverter.Services;
using Xunit;

namespace VrcClipboardConverter.Tests;

public class FakeProcessChecker : IProcessChecker
{
    public bool Running { get; set; }
    public bool IsProcessRunning(string processName) => Running;
}

public class VrcWatcherTests
{
    [Fact]
    public void Poll_WhenProcessAppears_RaisesTrueEventOnce()
    {
        var checker = new FakeProcessChecker { Running = false };
        var watcher = new VrcWatcher(checker);
        var events = new List<bool>();
        watcher.RunningStateChanged += (_, running) => events.Add(running);

        watcher.Poll(); // まだ未検出のまま
        checker.Running = true;
        watcher.Poll(); // 検出
        watcher.Poll(); // 継続検出、イベントは増えない

        Assert.Equal(new[] { true }, events);
        Assert.True(watcher.IsVrcRunning);
    }

    [Fact]
    public void Poll_WhenProcessDisappears_RaisesFalseEvent()
    {
        var checker = new FakeProcessChecker { Running = true };
        var watcher = new VrcWatcher(checker);
        watcher.Poll();
        var events = new List<bool>();
        watcher.RunningStateChanged += (_, running) => events.Add(running);

        checker.Running = false;
        watcher.Poll();

        Assert.Equal(new[] { false }, events);
        Assert.False(watcher.IsVrcRunning);
    }
}
