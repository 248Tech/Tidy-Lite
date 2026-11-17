using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TidyLite
{
    // All Win32 calls in one place.
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME { public uint Low; public uint High; public ulong Value { get { return ((ulong)High << 32) | Low; } } }

        [DllImport("kernel32.dll")]
        public static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

        public static MEMORYSTATUSEX Memory()
        {
            var m = new MEMORYSTATUSEX();
            m.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            GlobalMemoryStatusEx(ref m);
            return m;
        }

        [DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();

        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const uint PROCESS_TERMINATE = 0x0001;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr h);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder name, ref int size);

        [DllImport("kernel32.dll")]
        public static extern bool GetProcessTimes(IntPtr h, out FILETIME create, out FILETIME exit, out FILETIME kernel, out FILETIME user);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool TerminateProcess(IntPtr h, uint code);

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr h);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr h);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr h, uint cmd);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr h, int index);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

        public const uint WM_CLOSE = 0x0010;

        // Visible, titled, unowned, non-tool top-level windows per process id.
        public static Dictionary<int, List<IntPtr>> AppWindows()
        {
            var map = new Dictionary<int, List<IntPtr>>();
            EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (!IsWindowVisible(h) || GetWindow(h, 4) != IntPtr.Zero || GetWindowTextLength(h) == 0) return true;
                if ((GetWindowLong(h, -20) & 0x80) != 0) return true; // WS_EX_TOOLWINDOW
                int pid;
                GetWindowThreadProcessId(h, out pid);
                List<IntPtr> list;
                if (!map.TryGetValue(pid, out list)) { list = new List<IntPtr>(); map[pid] = list; }
                list.Add(h);
                return true;
            }, IntPtr.Zero);
            return map;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

        [StructLayout(LayoutKind.Sequential)]
        public struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHQueryRecycleBin(string root, ref SHQUERYRBINFO info);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string root, uint flags);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void DarkTitleBar(IntPtr hwnd, bool dark)
        {
            try
            {
                int v = dark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, 20, ref v, 4) != 0) DwmSetWindowAttribute(hwnd, 19, ref v, 4);
            }
            catch { }
        }

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, int count);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr h);
    }
}
