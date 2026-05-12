using System.Runtime.InteropServices;

namespace FileLockHotkey;

internal sealed class HotkeyApplicationContext : ApplicationContext
{
    private const int HotkeyId = 0x4c01;
    private const int WmHotkey = 0x0312;

    private readonly MessageWindow _window;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _hotkeyMenuItem;
    private HotkeyConfig _hotkey;
    private bool _hotkeyRegistered;
    private ResultsForm? _resultsForm;

    public HotkeyApplicationContext()
    {
        _window = new MessageWindow(OnHotkeyPressed);
        _hotkey = HotkeySettingsStore.Load();

        var menu = new ContextMenuStrip();
        menu.Items.Add("检测当前选中文件", null, (_, _) => ShowLocksForExplorerSelection(ExplorerSelectionMode.AllowAnyExplorerWindow));
        _hotkeyMenuItem = new ToolStripMenuItem();
        _hotkeyMenuItem.Click += (_, _) => ShowHotkeySettings();
        menu.Items.Add(_hotkeyMenuItem);
        menu.Items.Add("关于快捷键", null, (_, _) => ShowHotkeyHelp());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = BuildNotifyIconText(),
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowLocksForExplorerSelection(ExplorerSelectionMode.AllowAnyExplorerWindow);
        UpdateHotkeyLabels();

        if (!TryRegisterLoadedHotkey())
        {
            MessageBox.Show(
                "注册快捷键失败，可能已经被其他软件占用。\n\n请在托盘图标右键菜单中重新设置快捷键。",
                "文件占用查看器",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        _notifyIcon.ShowBalloonTip(
            2500,
            "文件占用查看器已运行",
            _hotkeyRegistered
                ? $"在资源管理器中选中文件，然后按 {_hotkey.DisplayText}。"
                : "在托盘图标右键菜单中设置快捷键。",
            ToolTipIcon.Info);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_hotkeyRegistered)
            {
                UnregisterHotKey(_window.Handle, HotkeyId);
            }

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _window.DestroyHandle();
        }

        base.Dispose(disposing);
    }

    private void OnHotkeyPressed()
    {
        ShowLocksForExplorerSelection(ExplorerSelectionMode.ForegroundOnly);
    }

    private void ShowHotkeyHelp()
    {
        MessageBox.Show(
            $"使用方法：\n\n1. 在 Windows 资源管理器中选中一个或多个文件。\n2. 按 {_hotkey.DisplayText}。\n3. 查看正在占用文件的进程。\n\n托盘图标右键可以手动检测，也可以设置快捷键。",
            "文件占用查看器",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ShowHotkeySettings()
    {
        using var form = new HotkeySettingsForm(_hotkey);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        ApplyHotkey(form.SelectedHotkey);
    }

    private void ShowLocksForExplorerSelection(ExplorerSelectionMode selectionMode)
    {
        var selection = ExplorerSelection.GetSelectedPaths(selectionMode);
        if (selection.Paths.Count == 0)
        {
            MessageBox.Show(
                $"没有找到当前选中的文件。\n\n请先在 Windows 资源管理器中单击选中文件，再按 {_hotkey.DisplayText}。",
                "文件占用查看器",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ShowResults(selection.Paths);
    }

    private void ShowResults(IReadOnlyList<string> paths)
    {
        if (_resultsForm is { IsDisposed: false })
        {
            _resultsForm.LoadPaths(paths);
            _resultsForm.Show();
            _resultsForm.Activate();
            return;
        }

        _resultsForm = new ResultsForm(paths);
        _resultsForm.FormClosed += (_, _) => _resultsForm = null;
        _resultsForm.Show();
    }

    private bool TryRegisterLoadedHotkey()
    {
        if (TryRegisterHotkey(_hotkey))
        {
            _hotkeyRegistered = true;
            return true;
        }

        if (_hotkey == HotkeyConfig.Default)
        {
            return false;
        }

        var failedHotkey = _hotkey;
        _hotkey = HotkeyConfig.Default;
        if (TryRegisterHotkey(_hotkey))
        {
            _hotkeyRegistered = true;
            HotkeySettingsStore.Save(_hotkey);
            UpdateHotkeyLabels();
            MessageBox.Show(
                $"之前保存的快捷键 {failedHotkey.DisplayText} 注册失败，已恢复为 {_hotkey.DisplayText}。",
                "文件占用查看器",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return true;
        }

        _hotkey = failedHotkey;
        UpdateHotkeyLabels();
        return false;
    }

    private void ApplyHotkey(HotkeyConfig newHotkey)
    {
        if (!newHotkey.IsValid(out var error))
        {
            MessageBox.Show(error, "文件占用查看器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var previousHotkey = _hotkey;
        var wasRegistered = _hotkeyRegistered;
        if (_hotkeyRegistered)
        {
            UnregisterHotKey(_window.Handle, HotkeyId);
            _hotkeyRegistered = false;
        }

        _hotkey = newHotkey;
        if (TryRegisterHotkey(_hotkey))
        {
            _hotkeyRegistered = true;
            HotkeySettingsStore.Save(_hotkey);
            UpdateHotkeyLabels();
            _notifyIcon.ShowBalloonTip(2000, "快捷键已更新", $"当前快捷键：{_hotkey.DisplayText}", ToolTipIcon.Info);
            return;
        }

        _hotkey = previousHotkey;
        if (wasRegistered && TryRegisterHotkey(_hotkey))
        {
            _hotkeyRegistered = true;
        }

        UpdateHotkeyLabels();
        MessageBox.Show(
            $"注册快捷键 {newHotkey.DisplayText} 失败，可能已经被其他软件占用。",
            "文件占用查看器",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private bool TryRegisterHotkey(HotkeyConfig hotkey)
    {
        return RegisterHotKey(_window.Handle, HotkeyId, hotkey.RegisterModifiers, (uint)hotkey.Key);
    }

    private void UpdateHotkeyLabels()
    {
        _hotkeyMenuItem.Text = $"设置快捷键...（当前：{_hotkey.DisplayText}）";
        _notifyIcon.Text = BuildNotifyIconText();
    }

    private string BuildNotifyIconText()
    {
        const int maxNotifyIconTextLength = 63;
        var text = $"文件占用查看器 - {_hotkey.DisplayText}";
        return text.Length <= maxNotifyIconTextLength
            ? text
            : text[..maxNotifyIconTextLength];
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private sealed class MessageWindow : NativeWindow
    {
        private readonly Action _onHotkeyPressed;

        public MessageWindow(Action onHotkeyPressed)
        {
            _onHotkeyPressed = onHotkeyPressed;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
            {
                _onHotkeyPressed();
                return;
            }

            base.WndProc(ref m);
        }
    }
}
