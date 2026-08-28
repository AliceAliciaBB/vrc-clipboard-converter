using System.Windows.Forms;
using VrcClipboardConverter.Models;

namespace VrcClipboardConverter.UI;

public partial class HistoryForm : Form
{
    private readonly List<HistoryEntry> _history;

    public HistoryForm(List<HistoryEntry> history)
    {
        _history = history;
        InitializeComponent();

        grid.Columns.Add("Timestamp", "時刻");
        grid.Columns.Add("OriginalUrl", "元URL");
        grid.Columns.Add("DirectUrl", "直リンク");
        grid.Columns.Add("Status", "状態");

        var recopyItem = new ToolStripMenuItem("直リンクを再コピー");
        recopyItem.Click += RecopyItem_Click;
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(recopyItem);
        grid.ContextMenuStrip = contextMenu;

        RefreshList();
    }

    public void ShowOrActivate()
    {
        if (!Visible)
        {
            Show();
        }
        Activate();
    }

    public void RefreshList()
    {
        grid.Rows.Clear();
        foreach (var entry in _history)
        {
            var directDisplay = entry.DirectUrl == null
                ? ""
                : (entry.DirectUrl.Length > 60 ? entry.DirectUrl[..60] + "..." : entry.DirectUrl);
            grid.Rows.Add(
                entry.Timestamp.ToString("HH:mm:ss"),
                entry.OriginalUrl,
                directDisplay,
                entry.Success ? "成功" : "失敗");
        }
    }

    public void SetStatusLabel(string text)
    {
        if (statusLabel.InvokeRequired)
        {
            statusLabel.Invoke(() => statusLabel.Text = text);
        }
        else
        {
            statusLabel.Text = text;
        }
    }

    private void RecopyItem_Click(object? sender, EventArgs e)
    {
        if (grid.CurrentRow == null)
        {
            return;
        }
        var index = grid.CurrentRow.Index;
        if (index < 0 || index >= _history.Count)
        {
            return;
        }
        var entry = _history[index];
        if (entry.Success && entry.DirectUrl != null)
        {
            Clipboard.SetText(entry.DirectUrl);
        }
    }

    private void HistoryForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // ウィンドウを閉じても常駐アプリ自体は終了させず、非表示にするだけにする
        e.Cancel = true;
        Hide();
    }
}
