namespace VrcClipboardConverter.UI;

partial class HistoryForm
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.Label statusLabel;
    private System.Windows.Forms.DataGridView grid;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.statusLabel = new System.Windows.Forms.Label();
        this.grid = new System.Windows.Forms.DataGridView();
        ((System.ComponentModel.ISupportInitialize)(this.grid)).BeginInit();
        this.SuspendLayout();

        this.statusLabel.Dock = System.Windows.Forms.DockStyle.Top;
        this.statusLabel.Height = 28;
        this.statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        this.statusLabel.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
        this.statusLabel.Text = "待機中";

        this.grid.Dock = System.Windows.Forms.DockStyle.Fill;
        this.grid.AllowUserToAddRows = false;
        this.grid.AllowUserToDeleteRows = false;
        this.grid.ReadOnly = true;
        this.grid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
        this.grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;

        this.ClientSize = new System.Drawing.Size(700, 400);
        this.Controls.Add(this.grid);
        this.Controls.Add(this.statusLabel);
        this.Text = "変換履歴";
        this.FormClosing += HistoryForm_FormClosing;

        ((System.ComponentModel.ISupportInitialize)(this.grid)).EndInit();
        this.ResumeLayout(false);
    }
}
