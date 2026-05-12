using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace FileLockHotkey;

internal sealed record LockInfo(
    string FilePath,
    int ProcessId,
    string ProcessName,
    string? ExecutablePath,
    string AppType,
    bool Restartable);

internal sealed record LockScanFailure(
    string FilePath,
    string Stage,
    int? ErrorCode,
    string Message);

internal sealed record LockScanResult(
    IReadOnlyList<LockInfo> Locks,
    IReadOnlyList<LockScanFailure> Failures,
    int CheckedItemCount);

internal static class FileLockScanner
{
    private const int ErrorMoreData = 234;

    public static LockScanResult Scan(IReadOnlyList<string> selectedPaths)
    {
        var locks = new List<LockInfo>();
        var failures = new List<LockScanFailure>();
        var queryItems = NormalizeSelectedPaths(selectedPaths, failures);

        foreach (var queryItem in queryItems)
        {
            try
            {
                locks.AddRange(queryItem.IsDirectory
                    ? DirectoryHandleScanner.FindLocksForDirectory(queryItem.Path)
                    : FindLocksForPath(queryItem.Path));
            }
            catch (Win32Exception ex)
            {
                failures.Add(new LockScanFailure(
                    queryItem.Path,
                    "查询占用进程",
                    ex.NativeErrorCode,
                    BuildWin32Message(ex)));
            }
            catch (Exception ex)
            {
                failures.Add(new LockScanFailure(
                    queryItem.Path,
                    "查询占用进程",
                    null,
                    ex.Message));
            }
        }

        var sortedLocks = locks
            .GroupBy(item => new { item.FilePath, item.ProcessId })
            .Select(group => group.First())
            .OrderBy(item => Path.GetFileName(item.FilePath), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.ProcessName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var sortedFailures = failures
            .OrderBy(item => item.FilePath, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new LockScanResult(sortedLocks, sortedFailures, queryItems.Count);
    }

    private static List<LockTarget> NormalizeSelectedPaths(IReadOnlyList<string> selectedPaths, List<LockScanFailure> failures)
    {
        var queryItems = new List<LockTarget>();

        foreach (var selectedPath in selectedPaths)
        {
            if (File.Exists(selectedPath))
            {
                queryItems.Add(new LockTarget(NormalizePath(selectedPath), IsDirectory: false));
                continue;
            }

            if (Directory.Exists(selectedPath))
            {
                queryItems.Add(new LockTarget(NormalizePath(selectedPath), IsDirectory: true));
                continue;
            }

            failures.Add(new LockScanFailure(
                selectedPath,
                "读取路径",
                null,
                "路径不存在或不是本地文件系统路径。"));
        }

        return queryItems
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Path, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static string BuildWin32Message(Win32Exception ex)
    {
        return ex.NativeErrorCode switch
        {
            5 => "权限不足，通常是系统进程、管理员进程或受保护路径导致。可以尝试用管理员身份运行本工具。",
            32 => "文件正在被另一个进程使用，但系统没有返回可识别的进程信息。",
            87 => "系统不接受这个路径作为 Restart Manager 查询资源，常见于特殊路径、虚拟路径或权限受限路径。",
            1223 => "操作被取消。",
            _ => ex.Message
        };
    }

    private static IReadOnlyList<LockInfo> FindLocksForPath(string filePath)
    {
        var sessionKey = new StringBuilder(32);
        var result = RmStartSession(out var sessionHandle, 0, sessionKey);
        if (result != 0)
        {
            throw new Win32Exception(result, $"启动 Restart Manager 会话失败：{filePath}");
        }

        try
        {
            var resources = new[] { filePath };
            result = RmRegisterResources(sessionHandle, (uint)resources.Length, resources, 0, null, 0, null);
            if (result != 0)
            {
                throw new Win32Exception(result, $"注册文件资源失败：{filePath}");
            }

            uint procInfoNeeded = 0;
            uint procInfo = 0;
            uint rebootReasons = 0;
            var affectedApps = Array.Empty<RM_PROCESS_INFO>();

            result = RmGetList(sessionHandle, out procInfoNeeded, ref procInfo, affectedApps, ref rebootReasons);
            if (result == ErrorMoreData)
            {
                affectedApps = new RM_PROCESS_INFO[procInfoNeeded];
                procInfo = procInfoNeeded;
                result = RmGetList(sessionHandle, out procInfoNeeded, ref procInfo, affectedApps, ref rebootReasons);
            }

            if (result != 0)
            {
                throw new Win32Exception(result, $"查询占用进程失败：{filePath}");
            }

            return affectedApps
                .Take((int)procInfo)
                .Select(info => ToLockInfo(filePath, info))
                .ToList();
        }
        finally
        {
            RmEndSession(sessionHandle);
        }
    }

    private static LockInfo ToLockInfo(string filePath, RM_PROCESS_INFO info)
    {
        var processId = info.Process.dwProcessId;
        var processName = string.IsNullOrWhiteSpace(info.strAppName)
            ? $"PID {processId}"
            : info.strAppName;

        return new LockInfo(
            filePath,
            processId,
            processName,
            TryGetExecutablePath(processId),
            GetAppTypeName(info.ApplicationType),
            info.bRestartable);
    }

    internal static string? TryGetExecutablePath(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    internal static string GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return string.IsNullOrWhiteSpace(process.ProcessName)
                ? $"PID {processId}"
                : process.ProcessName;
        }
        catch
        {
            return $"PID {processId}";
        }
    }

    private static string GetAppTypeName(RM_APP_TYPE appType)
    {
        return appType switch
        {
            RM_APP_TYPE.RmMainWindow => "桌面程序",
            RM_APP_TYPE.RmOtherWindow => "窗口程序",
            RM_APP_TYPE.RmService => "服务",
            RM_APP_TYPE.RmExplorer => "资源管理器",
            RM_APP_TYPE.RmConsole => "控制台",
            RM_APP_TYPE.RmCritical => "系统关键进程",
            _ => "未知"
        };
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint pSessionHandle,
        uint nFiles,
        string[] rgsFilenames,
        uint nApplications,
        RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices,
        string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
        ref uint lpdwRebootReasons);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint pSessionHandle);

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strAppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string strServiceShortName;

        public RM_APP_TYPE ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    private enum RM_APP_TYPE
    {
        RmUnknownApp = 0,
        RmMainWindow = 1,
        RmOtherWindow = 2,
        RmService = 3,
        RmExplorer = 4,
        RmConsole = 5,
        RmCritical = 1000
    }

    private sealed record LockTarget(string Path, bool IsDirectory);
}
