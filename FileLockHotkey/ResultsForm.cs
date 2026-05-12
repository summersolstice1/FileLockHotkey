using System.Diagnostics;
using System.Text;

namespace FileLockHotkey;

internal sealed class ResultsForm : Form
{
    private readonly Label _titleLabel = new();
    private readonly Label _statusLabel = new();
    private readonly ListView _listView = new();
    private readonly Button _refreshButton = new();
    private readonly Button _copyButton = new();
    private readonly Button _killButton = new();
    private readonly Button _closeButton = new();

    private IReadOnlyList<string> _paths;
    private IReadOnlyList<LockInfo> _locks = Array.Empty<LockInfo>();
    private IReadOnlyList<LockScanFailure> _failures = Array.Empty<LockScanFailure>();
    private int _checkedItemCount;

    public ResultsForm(IReadOnlyList<string> paths)
    {
        _paths = paths;
        InitializeComponent();
        Load += (_, _) => RefreshLocks();
    }

    public void LoadPaths(IReadOnlyList<string> paths)
    {
        _paths = paths;
        RefreshLocks();
    }

    private void InitializeComponent()
    {
        Text = "文件占用查看器";
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = true;
        MaximizeBox = true;
        MinimumSize = new Size(840, 420);
        Size = new Size(1080, 560);

        _titleLabel.AutoSize = false;
        _titleLabel.Dock = DockStyle.Top;
        _titleLabel.Height = 44;
        _titleLabel.Font = new Font(Font.FontFamily, 11, FontStyle.Bold);
        _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        _titleLabel.Padding = new Padding(12, 0, 12, 0);

        _statusLabel.AutoSize = false;
        _statusLabel.Dock = DockStyle.Top;
        _statusLabel.Height = 34;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Padding = new Padding(12, 0, 12, 0);

        _listView.Dock = DockStyle.Fill;
        _listView.View = View.Details;
        _listView.FullRowSelect = true;
        _listView.GridLines = true;
        _listView.MultiSelect = true;
        _listView.HideSelection = false;
        _listView.Columns.Add("文件", 260);
        _listView.Columns.Add("进程/状态", 170);
        _listView.Columns.Add("PID", 80);
        _listView.Columns.Add("类型/阶段", 100);
        _listView.Columns.Add("可重启", 80);
        _listView.Columns.Add("程序路径/原因", 360);
        _listView.SelectedIndexChanged += (_, _) => UpdateButtonStates();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 8, 12, 8),
            WrapContents = false
        };

        ConfigureButton(_closeButton, "关闭", (_, _) => Close());
        ConfigureButton(_killButton, "结束选中进程", (_, _) => KillSelectedProcesses());
        ConfigureButton(_copyButton, "复制结果", (_, _) => CopyResults());
        ConfigureButton(_refreshButton, "刷新", (_, _) => RefreshLocks());

        buttonPanel.Controls.Add(_closeButton);
        buttonPanel.Controls.Add(_killButton);
        buttonPanel.Controls.Add(_copyButton);
        buttonPanel.Controls.Add(_refreshButton);

        Controls.Add(_listView);
        Controls.Add(_statusLabel);
        Controls.Add(_titleLabel);
        Controls.Add(buttonPanel);
    }

    private static void ConfigureButton(Button button, string text, EventHandler clickHandler)
    {
        button.Text = text;
        button.AutoSize = true;
        button.MinimumSize = new Size(96, 32);
        button.Margin = new Padding(6, 0, 0, 0);
        button.Click += clickHandler;
    }

    private void RefreshLocks()
    {
        _titleLabel.Text = _paths.Count == 1
            ? $"正在检查：{_paths[0]}"
            : $"正在检查 {_paths.Count} 个项目";
        _statusLabel.Text = "查询中...";
        _listView.Items.Clear();
        UpdateButtonStates();

        var result = FileLockScanner.Scan(_paths);
        _locks = result.Locks;
        _failures = result.Failures;
        _checkedItemCount = result.CheckedItemCount;
        PopulateListView();
    }

    private void PopulateListView()
    {
        _listView.BeginUpdate();
        try
        {
            _listView.Items.Clear();
            foreach (var item in _locks)
            {
                var listItem = new ListViewItem(GetDisplayPath(item.FilePath))
                {
                    Tag = item
                };
                listItem.SubItems.Add(item.ProcessName);
                listItem.SubItems.Add(item.ProcessId.ToString());
                listItem.SubItems.Add(item.AppType);
                listItem.SubItems.Add(item.Restartable ? "是" : "否");
                listItem.SubItems.Add(item.ExecutablePath ?? "无法读取");
                _listView.Items.Add(listItem);
            }

            foreach (var failure in _failures)
            {
                var listItem = new ListViewItem(GetDisplayPath(failure.FilePath))
                {
                    ForeColor = Color.DarkRed
                };
                listItem.SubItems.Add("查询失败");
                listItem.SubItems.Add(failure.ErrorCode?.ToString() ?? "-");
                listItem.SubItems.Add(failure.Stage);
                listItem.SubItems.Add("-");
                listItem.SubItems.Add(failure.Message);
                _listView.Items.Add(listItem);
            }
        }
        finally
        {
            _listView.EndUpdate();
        }

        _statusLabel.Text = BuildStatusText();
        UpdateButtonStates();
    }

    private void CopyResults()
    {
        if (_locks.Count == 0 && _failures.Count == 0)
        {
            Clipboard.SetText("未发现占用进程。");
            return;
        }

        var builder = new StringBuilder();
        foreach (var item in _locks)
        {
            builder.AppendLine($"文件：{item.FilePath}");
            builder.AppendLine($"进程：{item.ProcessName}");
            builder.AppendLine($"PID：{item.ProcessId}");
            builder.AppendLine($"类型：{item.AppType}");
            builder.AppendLine($"程序路径：{item.ExecutablePath ?? "无法读取"}");
            builder.AppendLine();
        }

        foreach (var failure in _failures)
        {
            builder.AppendLine($"文件：{failure.FilePath}");
            builder.AppendLine($"状态：查询失败");
            builder.AppendLine($"阶段：{failure.Stage}");
            builder.AppendLine($"错误码：{failure.ErrorCode?.ToString() ?? "-"}");
            builder.AppendLine($"原因：{failure.Message}");
            builder.AppendLine();
        }

        Clipboard.SetText(builder.ToString());
    }

    private void KillSelectedProcesses()
    {
        var selectedLocks = _listView.SelectedItems
            .Cast<ListViewItem>()
            .Select(item => item.Tag)
            .OfType<LockInfo>()
            .GroupBy(item => item.ProcessId)
            .Select(group => group.First())
            .ToList();

        if (selectedLocks.Count == 0)
        {
            return;
        }

        var processNames = string.Join(Environment.NewLine, selectedLocks.Select(item => $"{item.ProcessName} (PID {item.ProcessId})"));
        var result = MessageBox.Show(
            $"确定要结束以下进程吗？\n\n{processNames}",
            "结束进程确认",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
        {
            return;
        }

        var failures = new List<string>();
        foreach (var item in selectedLocks)
        {
            try
            {
                using var process = Process.GetProcessById(item.ProcessId);
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                failures.Add($"{item.ProcessName} (PID {item.ProcessId})：{ex.Message}");
            }
        }

        if (failures.Count > 0)
        {
            MessageBox.Show(
                string.Join(Environment.NewLine, failures),
                "部分进程结束失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        RefreshLocks();
    }

    private void UpdateButtonStates()
    {
        _copyButton.Enabled = true;
        _killButton.Enabled = _listView.SelectedItems
            .Cast<ListViewItem>()
            .Any(item => item.Tag is LockInfo);
        _refreshButton.Enabled = _paths.Count > 0;
    }

    private string BuildStatusText()
    {
        if (_locks.Count == 0 && _failures.Count == 0)
        {
            return _checkedItemCount == 0
                ? "没有可查询的项目。"
                : $"已检查 {_checkedItemCount} 个项目，未发现占用进程。";
        }

        if (_failures.Count == 0)
        {
            return $"已检查 {_checkedItemCount} 个项目，发现 {_locks.Count} 条占用记录。";
        }

        return $"已检查 {_checkedItemCount} 个项目，发现 {_locks.Count} 条占用记录，{_failures.Count} 项查询失败。";
    }

    private static string GetDisplayPath(string path)
    {
        var fileName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(fileName) ? path : fileName;
    }
}
