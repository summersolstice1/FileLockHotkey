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
        Text = "设置快捷键";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, 230);

        var titleLabel = new Label
        {
            Text = "按下新的快捷键",
            AutoSize = false,
            Location = new Point(20, 18),
            Size = new Size(420, 28),
            Font = new Font(Font.FontFamily, 11, FontStyle.Bold)
        };

        _captureLabel.AutoSize = false;
        _captureLabel.BorderStyle = BorderStyle.FixedSingle;
        _captureLabel.Location = new Point(20, 58);
        _captureLabel.Size = new Size(420, 52);
        _captureLabel.TextAlign = ContentAlignment.MiddleCenter;
        _captureLabel.Font = new Font(Font.FontFamily, 16, FontStyle.Bold);

        _statusLabel.AutoSize = false;
        _statusLabel.Location = new Point(20, 120);
        _statusLabel.Size = new Size(420, 42);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        var defaultButton = new Button
        {
            Text = "恢复默认",
            Location = new Point(20, 178),
            Size = new Size(96, 32)
        };
        defaultButton.Click += (_, _) => SetSelectedHotkey(HotkeyConfig.Default);

        _okButton.Text = "保存";
        _okButton.Location = new Point(238, 178);
        _okButton.Size = new Size(96, 32);
        _okButton.DialogResult = DialogResult.OK;

        var cancelButton = new Button
        {
            Text = "取消",
            Location = new Point(344, 178),
            Size = new Size(96, 32),
            DialogResult = DialogResult.Cancel
        };

        AcceptButton = _okButton;
        CancelButton = cancelButton;

        Controls.Add(titleLabel);
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
        _okButton.Enabled = hotkey.IsValid(out _);
    }
}
