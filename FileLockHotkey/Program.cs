using System.Threading;

namespace FileLockHotkey;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, "FileLockHotkey.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                $"文件占用查看器已经在后台运行。\n\n在资源管理器中选中文件，然后按 {HotkeyConfig.Default.DisplayText}。",
                "文件占用查看器",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new HotkeyApplicationContext());
    }
}
