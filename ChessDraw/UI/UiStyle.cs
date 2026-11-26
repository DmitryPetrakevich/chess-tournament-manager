namespace ChessDraw.UI;

internal static class UiStyle
{
    public static readonly Color Light = Color.FromArgb(16, 20, 29);
    public static readonly Color Surface = Color.FromArgb(24, 30, 42);
    public static readonly Color Navy = Color.FromArgb(31, 39, 55);
    public static readonly Color Blue = Color.FromArgb(76, 111, 235);
    public static readonly Color Text = Color.FromArgb(232, 237, 247);
    public static readonly Color Muted = Color.FromArgb(155, 169, 193);
    public static readonly Color Border = Color.FromArgb(49, 61, 81);
    public static readonly Color Accent = Color.FromArgb(106, 217, 184);

    public static Button Button(string text, EventHandler? click = null, bool primary = false, int width = 145)
    {
        var b = new DarkButton { Text = text, Width = width, Height = 36, AutoSize = false,
            Margin = new Padding(3, 3, 7, 3), FlatStyle = FlatStyle.Flat,
            ForeColor = Text, BackColor = primary ? Blue : Navy, Cursor = Cursors.Hand };
        b.FlatAppearance.BorderColor = primary ? Blue : Border;
        b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(93, 128, 250) : Border;
        if (click != null) b.Click += click;
        return b;
    }

    public static DataGridView Grid()
    {
        var g = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, MultiSelect = false, ReadOnly = true,
            RowHeadersVisible = false, BackgroundColor = Surface, BorderStyle = BorderStyle.None,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false, AllowUserToResizeRows = false,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            GridColor = Border, ColumnHeadersHeight = 44, RowTemplate = { Height = 38 } };
        g.ColumnHeadersDefaultCellStyle.BackColor = Navy;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Navy;
        g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        g.DefaultCellStyle.BackColor = Surface;
        g.DefaultCellStyle.ForeColor = Text;
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(44, 62, 100);
        g.DefaultCellStyle.SelectionForeColor = Color.White;
        g.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(27, 34, 47);
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        return g;
    }

    public static void Apply(Control root)
    {
        root.ForeColor = Text;
        if (root is not System.Windows.Forms.Button && root is not DataGridView)
            root.BackColor = root is TextBoxBase or ComboBox or NumericUpDown ? Navy : Surface;
        foreach (Control c in root.Controls) Apply(c);
    }

    public static Control TabBar(TabControl tabs)
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 51, BackColor = Light, WrapContents = false };
        foreach (TabPage page in tabs.TabPages)
        {
            var button = Button(page.Text, (_, _) => tabs.SelectedTab = page, width: page.Text.Length > 20 ? 235 : 195);
            button.Height = 43; button.Tag = page; bar.Controls.Add(button);
        }
        void RefreshButtons()
        {
            foreach (Button b in bar.Controls)
            { b.BackColor = b.Tag == tabs.SelectedTab ? Navy : Light; b.ForeColor = b.Tag == tabs.SelectedTab ? Accent : Muted; }
        }
        tabs.SelectedIndexChanged += (_, _) => RefreshButtons();
        RefreshButtons();
        return bar;
    }

    private sealed class DarkButton : Button
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Enabled) return;
            using var brush = new SolidBrush(Surface);
            e.Graphics.FillRectangle(brush, ClientRectangle);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}

internal sealed class WorkspaceTabs : TabControl
{
    protected override void WndProc(ref Message m)
    {
        // Navigation is drawn by TabBar, so the native light tab strip is omitted.
        if (m.Msg == 0x1328 && !DesignMode)
        {
            m.Result = (IntPtr)1;
            return;
        }
        base.WndProc(ref m);
    }
}
