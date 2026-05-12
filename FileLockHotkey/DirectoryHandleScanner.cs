using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace FileLockHotkey;

internal static class DirectoryHandleScanner
{
    private const int SystemExtendedHandleInformation = 64;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    private const uint ProcessDuplicateHandle = 0x0040;
    private const uint DuplicateSameAccess = 0x00000002;
    private const uint FileTypeDisk = 0x0001;
    private const uint FileNameNormalized = 0x0;
    private const uint VolumeNameDos = 0x0;

    public static IReadOnlyList<LockInfo> FindLocksForDirectory(string directoryPath)
    {
        var normalizedDirectory = NormalizeComparablePath(directoryPath);
        var results = new Dictionary<(string Path, int ProcessId), LockInfo>();
        using var handleSnapshot = SystemHandleSnapshot.Create();
        var processHandles = new Dictionary<int, IntPtr>();

        try
        {
            foreach (var handle in handleSnapshot.Handles)
            {
                var processId = handle.ProcessId;
                if (processId <= 0)
                {
                    continue;
                }

                var sourceProcess = GetCachedProcessHandle(processId, processHandles);
                if (sourceProcess == IntPtr.Zero)
                {
                    continue;
                }

                if (!DuplicateHandle(sourceProcess, handle.HandleValue, GetCurrentProcess(), out var duplicatedHandle, 0, false, DuplicateSameAccess))
                {
                    continue;
                }

                try
                {
                    if (GetFileType(duplicatedHandle) != FileTypeDisk)
                    {
                        continue;
                    }

                    var handlePath = TryGetHandlePath(duplicatedHandle);
                    if (handlePath is null)
                    {
                        continue;
                    }

                    var comparableHandlePath = NormalizeComparablePath(handlePath);
                    if (!IsDirectoryOrChildPath(comparableHandlePath, normalizedDirectory))
                    {
                        continue;
                    }

                    var key = (comparableHandlePath, processId);
                    results.TryAdd(key, new LockInfo(
                        comparableHandlePath,
                        processId,
                        FileLockScanner.GetProcessName(processId),
                        FileLockScanner.TryGetExecutablePath(processId),
                        "句柄扫描",
                        Restartable: false));
                }
                finally
                {
                    CloseHandle(duplicatedHandle);
                }
            }
        }
        finally
        {
            foreach (var processHandle in processHandles.Values)
            {
                if (processHandle != IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                }
            }
        }

        return results.Values
            .OrderBy(item => item.FilePath, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.ProcessName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static IntPtr GetCachedProcessHandle(int processId, Dictionary<int, IntPtr> processHandles)
    {
        if (processHandles.TryGetValue(processId, out var processHandle))
        {
            return processHandle;
        }

        processHandle = OpenProcess(ProcessDuplicateHandle, false, processId);
        processHandles[processId] = processHandle;
        return processHandle;
    }

    private static string? TryGetHandlePath(IntPtr handle)
    {
        var builder = new StringBuilder(1024);
        var length = GetFinalPathNameByHandle(handle, builder, (uint)builder.Capacity, FileNameNormalized | VolumeNameDos);
        if (length == 0)
        {
            return null;
        }

        if (length >= builder.Capacity)
        {
            builder.EnsureCapacity((int)length + 1);
            length = GetFinalPathNameByHandle(handle, builder, (uint)builder.Capacity, FileNameNormalized | VolumeNameDos);
            if (length == 0 || length >= builder.Capacity)
            {
                return null;
            }
        }

        return CleanDevicePath(builder.ToString());
    }

    private static string CleanDevicePath(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + path[8..];
        }

        if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
        {
            return path[4..];
        }

        if (path.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase))
        {
            return path[4..];
        }

        return path;
    }

    private static string NormalizeComparablePath(string path)
    {
        var cleaned = CleanDevicePath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        try
        {
            cleaned = Path.GetFullPath(cleaned);
        }
        catch
        {
            // Some kernel paths cannot be normalized by Path.GetFullPath; string comparison is still useful.
        }

        return cleaned.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsDirectoryOrChildPath(string handlePath, string directoryPath)
    {
        if (string.Equals(handlePath, directoryPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return handlePath.Length > directoryPath.Length
            && handlePath.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase)
            && (handlePath[directoryPath.Length] == Path.DirectorySeparatorChar
                || handlePath[directoryPath.Length] == Path.AltDirectorySeparatorChar);
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(
        int systemInformationClass,
        IntPtr systemInformation,
        int systemInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DuplicateHandle(
        IntPtr sourceProcessHandle,
        IntPtr sourceHandle,
        IntPtr targetProcessHandle,
        out IntPtr targetHandle,
        uint desiredAccess,
        bool inheritHandle,
        uint options);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern uint GetFileType(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(IntPtr handle, StringBuilder filePath, uint filePathLength, uint flags);

    private readonly struct SystemHandle
    {
        public SystemHandle(int processId, IntPtr handleValue)
        {
            ProcessId = processId;
            HandleValue = handleValue;
        }

        public int ProcessId { get; }
        public IntPtr HandleValue { get; }
    }

    private sealed class SystemHandleSnapshot : IDisposable
    {
        private readonly IntPtr _buffer;

        private SystemHandleSnapshot(IntPtr buffer, IReadOnlyList<SystemHandle> handles)
        {
            _buffer = buffer;
            Handles = handles;
        }

        public IReadOnlyList<SystemHandle> Handles { get; }

        public static SystemHandleSnapshot Create()
        {
            var bufferSize = 0x10000;
            var buffer = Marshal.AllocHGlobal(bufferSize);

            try
            {
                int result;
                while ((result = NtQuerySystemInformation(SystemExtendedHandleInformation, buffer, bufferSize, out var returnLength)) == StatusInfoLengthMismatch)
                {
                    Marshal.FreeHGlobal(buffer);
                    bufferSize = Math.Max(bufferSize * 2, returnLength);
                    buffer = Marshal.AllocHGlobal(bufferSize);
                }

                if (result < 0)
                {
                    throw new Win32Exception(result, "无法枚举系统句柄。");
                }

                var handleCount = Marshal.ReadIntPtr(buffer).ToInt64();
                var entrySize = Marshal.SizeOf<SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>();
                var entryPointer = IntPtr.Add(buffer, IntPtr.Size * 2);
                var handles = new List<SystemHandle>();

                for (long i = 0; i < handleCount; i++)
                {
                    var entry = Marshal.PtrToStructure<SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>(entryPointer);
                    var processId = entry.UniqueProcessId.ToInt64();
                    if (processId > 0 && processId <= int.MaxValue)
                    {
                        handles.Add(new SystemHandle((int)processId, entry.HandleValue));
                    }

                    entryPointer = IntPtr.Add(entryPointer, entrySize);
                }

                return new SystemHandleSnapshot(buffer, handles);
            }
            catch
            {
                Marshal.FreeHGlobal(buffer);
                throw;
            }
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(_buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX
    {
        public IntPtr Object;
        public IntPtr UniqueProcessId;
        public IntPtr HandleValue;
        public uint GrantedAccess;
        public ushort CreatorBackTraceIndex;
        public ushort ObjectTypeIndex;
        public uint HandleAttributes;
        public uint Reserved;
    }
}
