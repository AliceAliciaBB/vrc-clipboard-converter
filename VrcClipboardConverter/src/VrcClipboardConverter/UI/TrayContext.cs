using System.Windows.Forms;
using VrcClipboardConverter.Logic;
using VrcClipboardConverter.Models;
using VrcClipboardConverter.Services;

namespace VrcClipboardConverter.UI;

public class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _autoStartMenuItem;
    private readonly VrcWatcher _vrcWatcher;
    private readonly ClipboardWatcher _clipboardWatcher;
    private readonly Converter _converter;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly HistoryForm _historyForm;
    private readonly List<HistoryEntry> _history = new();

    private const string StartupFolder =
        @"%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup";
    private const string ShortcutName = "VrcClipboardConverter";

    public TrayContext()
    {
        _historyForm = new HistoryForm(_history);

        _statusMenuItem = new ToolStripMenuItem("待機中") { Enabled = false };
        _autoStartMenuItem = new ToolStripMenuItem("Windows起動時に自動起動") { CheckOnClick = true };
        _autoStartMenuItem.Checked = StartupShortcut.IsEnabled(
            Environment.ExpandEnvironmentVariables(StartupFolder), ShortcutName);
        _autoStartMenuItem.CheckedChanged += OnAutoStartToggled;

        var openHistoryItem = new ToolStripMenuItem("履歴を開く");
        openHistoryItem.Click += (_, _) => _historyForm.ShowOrActivate();

        var exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += (_, _) => Application.Exit();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(openHistoryItem);
        menu.Items.Add(_autoStartMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu,
        };

        _vrcWatcher = new VrcWatcher(new RealProcessChecker());
        _vrcWatcher.RunningStateChanged += OnVrcRunningStateChanged;

        _clipboardWatcher = new ClipboardWatcher();
        _clipboardWatcher.YoutubeUrlDetected += OnYoutubeUrlDetected;

        var ytDlpPath = Path.Combine(AppContext.BaseDirectory, "yt-dlp_official.exe");
        _converter = new Converter(ytDlpPath);

        ApplyStatus(AppStatus.Idle);

        _pollTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _pollTimer.Tick += (_, _) => _vrcWatcher.Poll();
        _pollTimer.Start();
    }

    private void OnAutoStartToggled(object? sender, EventArgs e)
    {
        var folder = Environment.ExpandEnvironmentVariables(StartupFolder);
        var exePath = Path.Combine(AppContext.BaseDirectory, "VrcClipboardConverter.exe");
        if (_autoStartMenuItem.Checked)
        {
            StartupShortcut.Enable(folder, exePath, ShortcutName);
        }
        else
        {
            StartupShortcut.Disable(folder, ShortcutName);
        }
    }

    private void OnVrcRunningStateChanged(object? sender, bool running)
    {
        _clipboardWatcher.IsEnabled = running;
        ApplyStatus(running ? AppStatus.Watching : AppStatus.Idle);
    }

    private async void OnYoutubeUrlDetected(object? sender, string url)
    {
        ApplyStatus(AppStatus.Converting);
        var entry = await _converter.ConvertAsync(url);
        _history.Insert(0, entry);
        _historyForm.RefreshList();

        if (entry.Success && entry.DirectUrl != null)
        {
            _clipboardWatcher.NotifyLastWrittenText(entry.DirectUrl);
            Clipboard.SetText(entry.DirectUrl);
            ApplyStatus(AppStatus.Converted);
            await Task.Delay(3000);
            ApplyStatus(_vrcWatcher.IsVrcRunning ? AppStatus.Watching : AppStatus.Idle);
        }
        else
        {
            ApplyStatus(AppStatus.Error, entry.ErrorSummary);
        }
    }

    private void ApplyStatus(AppStatus status, string? errorDetail = null)
    {
        var display = StatusPresenterState.For(status, errorDetail);
        var iconPath = Path.Combine(AppContext.BaseDirectory, display.IconResourceName + ".ico");
        if (File.Exists(iconPath))
        {
            _trayIcon.Icon = new System.Drawing.Icon(iconPath);
        }
        _trayIcon.Text = display.TooltipText;
        _statusMenuItem.Text = display.LabelText;
        _historyForm.SetStatusLabel(display.LabelText);
    }
}
