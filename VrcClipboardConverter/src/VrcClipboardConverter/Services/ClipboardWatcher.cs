using System.Runtime.InteropServices;
using System.Windows.Forms;
using VrcClipboardConverter.Logic;

namespace VrcClipboardConverter.Services;

public class ClipboardWatcher : NativeWindow, IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    private string? _lastWrittenText;

    public bool IsEnabled { get; set; }
    public event EventHandler<string>? YoutubeUrlDetected;

    public ClipboardWatcher()
    {
        CreateHandle(new CreateParams());
        AddClipboardFormatListener(Handle);
    }

    public void NotifyLastWrittenText(string text)
    {
        _lastWrittenText = text;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE && IsEnabled)
        {
            HandleClipboardChanged();
        }
        base.WndProc(ref m);
    }

    private void HandleClipboardChanged()
    {
        string? text;
        try
        {
            text = Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // 他アプリがクリップボードをロック中。今回のイベントは無視。
            return;
        }

        if (text == null || text == _lastWrittenText)
        {
            return;
        }

        if (YoutubeUrlMatcher.IsYoutubeUrl(text))
        {
            YoutubeUrlDetected?.Invoke(this, text);
        }
    }

    public void Dispose()
    {
        RemoveClipboardFormatListener(Handle);
        DestroyHandle();
    }
}
