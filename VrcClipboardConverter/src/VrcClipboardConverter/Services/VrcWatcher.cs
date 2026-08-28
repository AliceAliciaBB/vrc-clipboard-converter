using System.Diagnostics;

namespace VrcClipboardConverter.Services;

public interface IProcessChecker
{
    bool IsProcessRunning(string processName);
}

public class RealProcessChecker : IProcessChecker
{
    public bool IsProcessRunning(string processName)
    {
        return Process.GetProcessesByName(processName).Length > 0;
    }
}

public class VrcWatcher
{
    private readonly IProcessChecker _checker;
    private readonly string _processName;

    public bool IsVrcRunning { get; private set; }
    public event EventHandler<bool>? RunningStateChanged;

    public VrcWatcher(IProcessChecker checker, string processName = "VRChat")
    {
        _checker = checker;
        _processName = processName;
    }

    public void Poll()
    {
        var running = _checker.IsProcessRunning(_processName);
        if (running != IsVrcRunning)
        {
            IsVrcRunning = running;
            RunningStateChanged?.Invoke(this, running);
        }
    }
}
