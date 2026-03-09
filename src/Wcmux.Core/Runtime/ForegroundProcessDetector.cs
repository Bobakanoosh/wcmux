using System.Runtime.InteropServices;

namespace Wcmux.Core.Runtime;

/// <summary>
/// Detects the foreground (deepest child) process name for a given shell PID
/// using the ToolHelp32 snapshot API. This avoids WMI overhead (~1ms vs 50-200ms).
/// </summary>
public static class ForegroundProcessDetector
{
    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    /// <summary>
    /// Returns the name of the deepest child process (without .exe extension)
    /// for the given shell process ID. Returns null for invalid/zero PIDs or
    /// if the process tree cannot be walked.
    /// </summary>
    public static string? GetForegroundProcessName(int shellPid)
    {
        if (shellPid <= 0)
            return null;

        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE)
            return null;

        try
        {
            // Build parent -> children map from a single snapshot
            var children = new Dictionary<uint, List<(uint pid, string name)>>();
            string? shellName = null;

            var entry = new PROCESSENTRY32
            {
                dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>(),
            };

            if (!Process32First(snapshot, ref entry))
                return null;

            do
            {
                var parentPid = entry.th32ParentProcessID;
                var pid = entry.th32ProcessID;
                var name = entry.szExeFile;

                if (pid == (uint)shellPid)
                    shellName = name;

                if (!children.TryGetValue(parentPid, out var list))
                {
                    list = new List<(uint, string)>();
                    children[parentPid] = list;
                }
                list.Add((pid, name));
            }
            while (Process32Next(snapshot, ref entry));

            if (shellName is null)
                return null;

            // Walk from shellPid to deepest child
            var currentPid = (uint)shellPid;
            var currentName = shellName;

            while (children.TryGetValue(currentPid, out var childList) && childList.Count > 0)
            {
                // Take the first child (most common case: single child chain)
                var child = childList[0];
                currentPid = child.pid;
                currentName = child.name;
            }

            return Path.GetFileNameWithoutExtension(currentName);
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}
