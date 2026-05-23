using System.Diagnostics;
using System.Text;

namespace FileLockHotkey;

internal sealed class ResultsForm : Form
{
    private readonly Label _titleLabel = new();
    private readonly Label _pathLabel = new();
    private readonly Label _statusBadgeLabel = new();
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
        AppTheme.ApplyForm(this);
        Text = "文件占用查看器";
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = true;
        MaximizeBox = true;
        MinimumSize = new Size(840, 420);
        Size = new Size(1080, 560);
        Padding = new Padding(16);

        var headerPanel = new Panel
        {
            BackColor = AppTheme.Header,
            Dock = DockStyle.Top,
            Height = 92,
            Padding = new Padding(18, 14, 18, 14)
        };

        var headerLayout = new TableLayoutPanel
        {
            ColumnCount = 3,
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var iconBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = AppIcons.CreateBitmap(42),
            SizeMode = PictureBoxSizeMode.CenterImage
        };

        _titleLabel.AutoSize = false;
        _titleLabel.Dock = DockStyle.Fill;
        _titleLabel.Font = AppTheme.CreateFont(13F, FontStyle.Bold);
        _titleLabel.ForeColor = Color.White;
        _titleLabel.Text = "文件占用查看器";
        _titleLabel.TextAlign = ContentAlignment.BottomLeft;

        _pathLabel.AutoEllipsis = true;
        _pathLabel.AutoSize = false;
        _pathLabel.Dock = DockStyle.Fill;
        _pathLabel.Font = AppTheme.CreateFont(9F);
        _pathLabel.ForeColor = Color.FromArgb(213, 224, 235);
        _pathLabel.TextAlign = ContentAlignment.TopLeft;

        _statusBadgeLabel.AutoSize = false;
        _statusBadgeLabel.BackColor = AppTheme.Accent;
        _statusBadgeLabel.Dock = DockStyle.Fill;
        _statusBadgeLabel.Font = AppTheme.CreateFont(9F, FontStyle.Bold);
        _statusBadgeLabel.ForeColor = Color.White;
        _statusBadgeLabel.Margin = new Padding(12, 10, 0, 10);
        _statusBadgeLabel.Text = "准备就绪";
        _statusBadgeLabel.TextAlign = ContentAlignment.MiddleCenter;

        headerLayout.Controls.Add(iconBox, 0, 0);
        headerLayout.SetRowSpan(iconBox, 2);
        headerLayout.Controls.Add(_titleLabel, 1, 0);
        headerLayout.Controls.Add(_pathLabel, 1, 1);
        headerLayout.Controls.Add(_statusBadgeLabel, 2, 0);
        headerLayout.SetRowSpan(_statusBadgeLabel, 2);
        headerPanel.Controls.Add(headerLayout);

        _listView.Dock = DockStyle.Fill;
        _listView.View = View.Details;
        _listView.FullRowSelect = true;
        _listView.GridLines = false;
        _listView.MultiSelect = true;
        _listView.HideSelection = false;
        _listView.BorderStyle = BorderStyle.None;
        _listView.BackColor = AppTheme.Surface;
        _listView.ForeColor = AppTheme.Text;
        _listView.Font = AppTheme.CreateFont(9F);
        _listView.Columns.Add("文件", 260);
        _listView.Columns.Add("进程/状态", 180);
        _listView.Columns.Add("PID", 80);
        _listView.Columns.Add("类型/阶段", 100);
        _listView.Columns.Add("可重启", 80);
        _listView.Columns.Add("程序路径/原因", 400);
        _listView.SelectedIndexChanged += (_, _) => UpdateButtonStates();

        var listPanel = new Panel
        {
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Padding = new Padding(1)
        };
        listPanel.Controls.Add(_listView);

        var footerPanel = new TableLayoutPanel
        {
            BackColor = AppTheme.Background,
            ColumnCount = 2,
            Dock = DockStyle.Bottom,
            Height = 58,
            Padding = new Padding(0, 12, 0, 0)
        };
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 470));

        _statusLabel.AutoEllipsis = true;
        _statusLabel.AutoSize = false;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = AppTheme.Muted;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        ConfigureButton(_closeButton, "关闭", ButtonRole.Secondary, 92, (_, _) => Close());
        ConfigureButton(_killButton, "结束进程", ButtonRole.Danger, 108, (_, _) => KillSelectedProcesses());
        ConfigureButton(_copyButton, "复制结果", ButtonRole.Secondary, 104, (_, _) => CopyResults());
        ConfigureButton(_refreshButton, "刷新", ButtonRole.Primary, 92, (_, _) => RefreshLocks());

        buttonPanel.Controls.Add(_closeButton);
        buttonPanel.Controls.Add(_killButton);
        buttonPanel.Controls.Add(_copyButton);
        buttonPanel.Controls.Add(_refreshButton);

        footerPanel.Controls.Add(_statusLabel, 0, 0);
        footerPanel.Controls.Add(buttonPanel, 1, 0);

        Controls.Add(listPanel);
        Controls.Add(footerPanel);
        Controls.Add(headerPanel);
    }

    private static void ConfigureButton(Button button, string text, ButtonRole role, int width, EventHandler clickHandler)
    {
        button.Text = text;
        button.Width = width;
        AppTheme.StyleButton(button, role);
        button.Click += clickHandler;
    }

    private void RefreshLocks()
    {
        _titleLabel.Text = "文件占用查看器";
        _pathLabel.Text = _paths.Count == 1
            ? _paths[0]
            : $"正在检查 {_paths.Count} 个项目";
        _statusLabel.Text = "查询中...";
        _statusBadgeLabel.Text = "查询中";
        _statusBadgeLabel.BackColor = AppTheme.Accent;
        _listView.Items.Clear();
        UpdateButtonStates();

        UseWaitCursor = true;
        try
        {
            var result = FileLockScanner.Scan(_paths);
            _locks = result.Locks;
            _failures = result.Failures;
            _checkedItemCount = result.CheckedItemCount;
            PopulateListView();
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void PopulateListView()
    {
        _listView.BeginUpdate();
        try
        {
            _listView.Items.Clear();
            var rowIndex = 0;
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
                ApplyRowStyle(listItem, rowIndex++);
                _listView.Items.Add(listItem);
            }

            foreach (var failure in _failures)
            {
                var listItem = new ListViewItem(GetDisplayPath(failure.FilePath))
                {
                    ForeColor = AppTheme.Danger
                };
                listItem.SubItems.Add("查询失败");
                listItem.SubItems.Add(failure.ErrorCode?.ToString() ?? "-");
                listItem.SubItems.Add(failure.Stage);
                listItem.SubItems.Add("-");
                listItem.SubItems.Add(failure.Message);
                ApplyRowStyle(listItem, rowIndex++);
                _listView.Items.Add(listItem);
            }
        }
        finally
        {
            _listView.EndUpdate();
        }

        _statusLabel.Text = BuildStatusText();
        UpdateStatusBadge();
        UpdateButtonStates();
    }

    private static void ApplyRowStyle(ListViewItem item, int rowIndex)
    {
        item.UseItemStyleForSubItems = true;
        item.BackColor = rowIndex % 2 == 0 ? AppTheme.Surface : AppTheme.SurfaceAlt;
    }

    private void UpdateStatusBadge()
    {
        if (_failures.Count > 0)
        {
            _statusBadgeLabel.Text = "有失败";
            _statusBadgeLabel.BackColor = AppTheme.Warning;
            return;
        }

        if (_locks.Count > 0)
        {
            _statusBadgeLabel.Text = "发现占用";
            _statusBadgeLabel.BackColor = AppTheme.Danger;
            return;
        }

        _statusBadgeLabel.Text = "未占用";
        _statusBadgeLabel.BackColor = AppTheme.Accent;
    }

    private void CopyResults()
    {
        if (_locks.Count == 0 && _failures.Count == 0)
        {
            Clipboard.SetText("未发现占用进程。");
            _statusLabel.Text = "结果已复制到剪贴板。";
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
        _statusLabel.Text = "结果已复制到剪贴板。";
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
