using System.Collections;
using System.Runtime.InteropServices;

namespace FileLockHotkey;

internal sealed record ExplorerSelectionResult(IReadOnlyList<string> Paths);

internal enum ExplorerSelectionMode
{
    ForegroundOnly,
    AllowAnyExplorerWindow
}

internal static class ExplorerSelection
{
    private const uint GaRoot = 2;

    public static ExplorerSelectionResult GetSelectedPaths(ExplorerSelectionMode mode)
    {
        var foregroundWindow = GetForegroundWindow();
        var selections = GetSelections();
        var foregroundSelection = selections
            .Where(selection => IsRelatedToForegroundWindow(selection.Hwnd, foregroundWindow))
            .SelectMany(selection => selection.Paths)
            .ToList();

        if (foregroundSelection.Count > 0)
        {
            return new ExplorerSelectionResult(Deduplicate(foregroundSelection));
        }

        if (mode == ExplorerSelectionMode.ForegroundOnly)
        {
            return new ExplorerSelectionResult(Array.Empty<string>());
        }

        var allSelections = selections.SelectMany(selection => selection.Paths).ToList();
        return new ExplorerSelectionResult(Deduplicate(allSelections));
    }

    private static List<ShellWindowSelection> GetSelections()
    {
        var selections = new List<ShellWindowSelection>();
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null)
        {
            return selections;
        }

        object? shell = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return selections;
            }

            foreach (dynamic window in (IEnumerable)shellType.InvokeMember("Windows", System.Reflection.BindingFlags.InvokeMethod, null, shell, null)!)
            {
                object? selectedItems = null;
                try
                {
                    var hwnd = new IntPtr(Convert.ToInt64(window.HWND));
                    selectedItems = window.Document.SelectedItems();
                    var count = Convert.ToInt32(((dynamic)selectedItems).Count);
                    var paths = new List<string>();
                    for (var i = 0; i < count; i++)
                    {
                        string? path = ((dynamic)selectedItems).Item(i).Path;
                        if (IsExistingFileSystemPath(path))
                        {
                            paths.Add(path!);
                        }
                    }

                    if (paths.Count > 0)
                    {
                        selections.Add(new ShellWindowSelection(hwnd, paths));
                    }
                }
                catch
                {
                    // Some Shell windows are browser/control panels and do not expose file selections.
                }
                finally
                {
                    TryReleaseComObject(selectedItems);
                    TryReleaseComObject(window);
                }
            }
        }
        catch
        {
            return selections;
        }
        finally
        {
            TryReleaseComObject(shell);
        }

        return selections;
    }

    private static IReadOnlyList<string> Deduplicate(IEnumerable<string> paths)
    {
        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static bool IsExistingFileSystemPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return File.Exists(path) || Directory.Exists(path);
    }

    private static bool IsRelatedToForegroundWindow(IntPtr shellWindow, IntPtr foregroundWindow)
    {
        if (shellWindow == IntPtr.Zero || foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        var shellRoot = GetAncestor(shellWindow, GaRoot);
        var foregroundRoot = GetAncestor(foregroundWindow, GaRoot);
        return shellWindow == foregroundWindow
            || shellWindow == foregroundRoot
            || shellRoot == foregroundWindow
            || shellRoot == foregroundRoot
            || IsChild(shellWindow, foregroundWindow);
    }

    private static void TryReleaseComObject(object? value)
    {
        try
        {
            if (value is not null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }
        catch
        {
            // Releasing COM references is a best effort cleanup.
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

    private sealed record ShellWindowSelection(IntPtr Hwnd, IReadOnlyList<string> Paths);
}
