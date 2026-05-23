namespace FileLockHotkey;

internal sealed class HotkeySettingsForm : Form
{
    private readonly Label _captureLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _okButton = new();

    public HotkeyConfig SelectedHotkey { get; private set; }

    public HotkeySettingsForm(HotkeyConfig currentHotkey)
    {
        SelectedHotkey = currentHotkey;
        InitializeComponent();
        SetSelectedHotkey(currentHotkey);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData is Keys.Escape or Keys.Enter)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        CaptureHotkey(keyData);
        return true;
    }

    private void InitializeComponent()
    {
        AppTheme.ApplyForm(this);
        Text = "设置快捷键";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(500, 270);

        var headerPanel = new Panel
        {
            BackColor = AppTheme.Header,
            Dock = DockStyle.Top,
            Height = 72,
            Padding = new Padding(20, 12, 20, 12)
        };

        var iconBox = new PictureBox
        {
            Image = AppIcons.CreateBitmap(40),
            Location = new Point(20, 16),
            Size = new Size(40, 40),
            SizeMode = PictureBoxSizeMode.CenterImage
        };

        var titleLabel = new Label
        {
            Text = "设置快捷键",
            AutoSize = false,
            Location = new Point(72, 12),
            Size = new Size(390, 26),
            Font = AppTheme.CreateFont(12F, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.BottomLeft
        };

        var tipLabel = new Label
        {
            Text = "按下新的组合键，保存后立即生效",
            AutoSize = false,
            Location = new Point(72, 39),
            Size = new Size(390, 20),
            ForeColor = Color.FromArgb(213, 224, 235),
            TextAlign = ContentAlignment.TopLeft
        };

        headerPanel.Controls.Add(iconBox);
        headerPanel.Controls.Add(titleLabel);
        headerPanel.Controls.Add(tipLabel);

        _captureLabel.AutoSize = false;
        _captureLabel.BackColor = AppTheme.Surface;
        _captureLabel.BorderStyle = BorderStyle.FixedSingle;
        _captureLabel.ForeColor = AppTheme.AccentDark;
        _captureLabel.Location = new Point(20, 96);
        _captureLabel.Size = new Size(460, 58);
        _captureLabel.TextAlign = ContentAlignment.MiddleCenter;
        _captureLabel.Font = AppTheme.CreateFont(17F, FontStyle.Bold);

        _statusLabel.AutoSize = false;
        _statusLabel.ForeColor = AppTheme.Muted;
        _statusLabel.Location = new Point(20, 164);
        _statusLabel.Size = new Size(460, 38);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        var defaultButton = new Button
        {
            Text = "恢复默认",
            Location = new Point(20, 218),
            Size = new Size(104, 36)
        };
        AppTheme.StyleButton(defaultButton);
        defaultButton.Click += (_, _) => SetSelectedHotkey(HotkeyConfig.Default);

        _okButton.Text = "保存";
        _okButton.Location = new Point(268, 218);
        _okButton.Size = new Size(100, 36);
        _okButton.DialogResult = DialogResult.OK;
        AppTheme.StyleButton(_okButton, ButtonRole.Primary);

        var cancelButton = new Button
        {
            Text = "取消",
            Location = new Point(380, 218),
            Size = new Size(100, 36),
            DialogResult = DialogResult.Cancel
        };
        AppTheme.StyleButton(cancelButton);

        AcceptButton = _okButton;
        CancelButton = cancelButton;

        Controls.Add(headerPanel);
        Controls.Add(_captureLabel);
        Controls.Add(_statusLabel);
        Controls.Add(defaultButton);
        Controls.Add(_okButton);
        Controls.Add(cancelButton);
    }

    private void CaptureHotkey(Keys keyData)
    {
        var hotkey = HotkeyConfig.FromKeyData(keyData);
        if (!hotkey.IsValid(out var error))
        {
            _captureLabel.Text = hotkey.DisplayText;
            _statusLabel.Text = error;
            _statusLabel.ForeColor = AppTheme.Danger;
            _okButton.Enabled = false;
            return;
        }

        SetSelectedHotkey(hotkey);
    }

    private void SetSelectedHotkey(HotkeyConfig hotkey)
    {
        SelectedHotkey = hotkey;
        _captureLabel.Text = hotkey.DisplayText;
        _statusLabel.Text = "保存后立即生效。";
        _statusLabel.ForeColor = AppTheme.Muted;
        _okButton.Enabled = hotkey.IsValid(out _);
    }
}
