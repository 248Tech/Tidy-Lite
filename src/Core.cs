using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace TidyLite
{
    internal static class Fmt
    {
        public static string Bytes(double n)
        {
            if (n <= 0) return "0 B";
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (n >= 1024 && i < u.Length - 1) { n /= 1024; i++; }
            string f = i == 0 ? "0" : n >= 100 ? "0" : n >= 10 ? "0.0" : "0.0";
            return n.ToString(f) + " " + u[i];
        }

        public static string Ago(DateTime t)
        {
            var s = (DateTime.Now - t).TotalSeconds;
            if (s < 3600) return "this hour";
            if (s < 86400) return ((int)(s / 3600)) + "h ago";
            if (s < 86400 * 60) return ((int)(s / 86400)) + " days ago";
            if (s < 86400 * 730) return ((int)(s / 2592000)) + " months ago";
            return ((int)(s / 31536000)) + " years ago";
        }

        public static string Uptime(ulong sec)
        {
            ulong d = sec / 86400, h = (sec % 86400) / 3600, m = (sec % 3600) / 60;
            if (d > 0) return d + "d " + h + "h";
            if (h > 0) return h + "h " + m + "m";
            return m + "m";
        }
    }

    internal static class Protect
    {
        static readonly Dictionary<string, string> Blocked = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "system", "This is the heart of Windows. Stopping it would crash your PC." },
            { "idle", "This isn't a real program - it's unused CPU time." },
            { "registry", "Windows needs this to keep its settings." },
            { "secure system", "A protected part of Windows security." },
            { "memory compression", "Windows uses this to fit more into memory. It's helping you!" },
            { "smss", "Starts your Windows session. Stopping it would crash your PC." },
            { "csrss", "A core part of Windows. Stopping it would crash your PC." },
            { "wininit", "A core part of Windows. Stopping it would crash your PC." },
            { "winlogon", "Handles signing in. Stopping it would sign you out or crash." },
            { "services", "Runs every Windows service. Stopping it would crash your PC." },
            { "lsass", "Keeps your passwords secure. Stopping it forces a restart." },
            { "lsaiso", "Keeps your passwords secure." },
            { "fontdrvhost", "Draws text on screen for Windows." },
        };

        static readonly Dictionary<string, string> Caution = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "svchost", "Runs Windows services. Stopping it may break Wi-Fi, sound or updates until you restart." },
            { "dwm", "Draws your windows. The screen will flicker while it restarts." },
            { "explorer", "Runs your taskbar and desktop. They will disappear until it restarts." },
            { "audiodg", "Plays your sound. Audio will stop briefly." },
            { "msmpeng", "Microsoft Defender antivirus - it protects your PC." },
            { "ctfmon", "Handles keyboard and typing input." },
            { "sihost", "Runs parts of the Windows shell." },
            { "spoolsv", "Handles printing." },
        };

        public static readonly string WinDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');

        // Returns "blocked", "caution" or null, with a plain-English reason.
        public static string Check(string name, string exe, int pid, out string reason)
        {
            reason = null;
            if (pid == Process.GetCurrentProcess().Id) { reason = "That's Tidy Lite itself!"; return "blocked"; }
            if (pid <= 4) { reason = Blocked["system"]; return "blocked"; }
            if (Blocked.TryGetValue(name, out reason)) return "blocked";
            if (Caution.TryGetValue(name, out reason)) return "caution";
            if (!string.IsNullOrEmpty(exe) && exe.StartsWith(WinDir + "\\", StringComparison.OrdinalIgnoreCase))
            {
                reason = "This is part of Windows. Stopping it might make something stop working until you restart.";
                return "caution";
            }
            return null;
        }

        public static string PathProblem(string path)
        {
            if (string.IsNullOrEmpty(path) || !Path.IsPathRooted(path)) return "Invalid path.";
            string p = Path.GetFullPath(path).TrimEnd('\\');
            string root = Path.GetPathRoot(p).TrimEnd('\\');
            if (p.Length <= root.Length) return "You can't delete a whole drive.";
            string[] exact = {
                WinDir, root + "\\Program Files", root + "\\Program Files (x86)", root + "\\ProgramData", root + "\\Users",
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\'), root + "\\Recovery", root + "\\Boot"
            };
            foreach (var e in exact) if (string.Equals(p, e, StringComparison.OrdinalIgnoreCase)) return "This is an important system folder.";
            if (p.StartsWith(WinDir + "\\", StringComparison.OrdinalIgnoreCase) && !p.StartsWith(WinDir + "\\Temp\\", StringComparison.OrdinalIgnoreCase))
                return "Files inside Windows keep your PC running.";
            string name = Path.GetFileName(p).ToLowerInvariant();
            if (string.Equals(Path.GetDirectoryName(p).TrimEnd('\\'), root, StringComparison.OrdinalIgnoreCase) &&
                (name == "pagefile.sys" || name == "hiberfil.sys" || name == "swapfile.sys" || name == "bootmgr"))
                return "Windows manages this file itself.";
            if (p.IndexOf("\\System Volume Information", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("\\$Recycle.Bin", StringComparison.OrdinalIgnoreCase) >= 0)
                return "This is managed by Windows.";
            return null;
        }
    }

    // ───────── system metrics ─────────
    internal class Metrics
    {
        ulong prevIdle, prevTotal;
        public double Cpu;
        public ulong MemUsed, MemTotal;

        public void Sample()
        {
            Native.FILETIME idle, kern, user;
            if (Native.GetSystemTimes(out idle, out kern, out user))
            {
                ulong i = idle.Value, t = kern.Value + user.Value;
                if (prevTotal != 0 && t > prevTotal) Cpu = Math.Max(0, Math.Min(100, 100.0 * (1 - (double)(i - prevIdle) / (t - prevTotal))));
                prevIdle = i; prevTotal = t;
            }
            var m = Native.Memory();
            MemTotal = m.ullTotalPhys;
            MemUsed = m.ullTotalPhys - m.ullAvailPhys;
        }
    }

    // ───────── processes ─────────
    internal class AppGroup
    {
        public string Key, Name, Exe, Publisher, Kind, Protect, Reason;
        public long Mem;
        public double Cpu;
        public List<int> Pids = new List<int>();
    }

    internal class ProcMonitor
    {
        class Seen { public long CpuTicks; public string Exe; }
        readonly Dictionary<int, Seen> prev = new Dictionary<int, Seen>();
        readonly Dictionary<string, string[]> meta = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Image> icons = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        DateTime prevAt = DateTime.MinValue;
        public int ProcessCount;

        static string ExePath(int pid)
        {
            IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return "";
            try
            {
                var sb = new StringBuilder(1024);
                int n = sb.Capacity;
                return Native.QueryFullProcessImageName(h, 0, sb, ref n) ? sb.ToString(0, n) : "";
            }
            finally { Native.CloseHandle(h); }
        }

        static long CpuTicks(int pid)
        {
            IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return -1;
            try
            {
                Native.FILETIME c, e, k, u;
                return Native.GetProcessTimes(h, out c, out e, out k, out u) ? (long)(k.Value + u.Value) : -1;
            }
            finally { Native.CloseHandle(h); }
        }

        string[] Meta(string exe)
        {
            string[] m;
            if (meta.TryGetValue(exe, out m)) return m;
            m = new[] { "", "" };
            try
            {
                var v = FileVersionInfo.GetVersionInfo(exe);
                m[0] = (v.FileDescription ?? "").Trim();
                if (m[0] == "" && v.ProductName != null && v.ProductName.IndexOf("Operating System", StringComparison.Ordinal) < 0) m[0] = v.ProductName.Trim();
                m[1] = (v.CompanyName ?? "").Trim();
            }
            catch { }
            meta[exe] = m;
            return m;
        }

        public Image Icon(string exe)
        {
            if (string.IsNullOrEmpty(exe)) return null;
            Image img;
            if (icons.TryGetValue(exe, out img)) return img;
            img = null;
            try
            {
                var large = new IntPtr[1];
                if (Native.ExtractIconEx(exe, 0, large, null, 1) > 0 && large[0] != IntPtr.Zero)
                {
                    using (var ic = System.Drawing.Icon.FromHandle(large[0])) img = ic.ToBitmap();
                    Native.DestroyIcon(large[0]);
                }
            }
            catch { }
            icons[exe] = img;
            return img;
        }

        static readonly Dictionary<string, string> Friendly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "svchost", "Windows Service Host" }, { "explorer", "File Explorer & Taskbar" }, { "dwm", "Desktop Window Manager" },
            { "msmpeng", "Microsoft Defender" }, { "system", "Windows Kernel" }, { "memory compression", "Memory Compression" },
        };

        public List<AppGroup> Snapshot()
        {
            var now = DateTime.UtcNow;
            double wall = prevAt == DateTime.MinValue ? 0 : (now - prevAt).TotalSeconds * 1e7 * Environment.ProcessorCount;
            var windows = Native.AppWindows();
            var groups = new Dictionary<string, AppGroup>(StringComparer.OrdinalIgnoreCase);
            var seenNow = new Dictionary<int, Seen>();
            Process[] procs = Process.GetProcesses();
            ProcessCount = procs.Length;
            foreach (var p in procs)
            {
                try
                {
                    int pid = p.Id;
                    if (pid == 0) continue;
                    string name = p.ProcessName;
                    Seen old;
                    prev.TryGetValue(pid, out old);
                    string exe = old != null ? old.Exe : (pid > 4 ? ExePath(pid) : "");
                    long ticks = CpuTicks(pid);
                    double cpu = 0;
                    if (old != null && ticks >= 0 && old.CpuTicks >= 0 && wall > 0) cpu = Math.Max(0, Math.Min(100, 100.0 * (ticks - old.CpuTicks) / wall));
                    seenNow[pid] = new Seen { CpuTicks = ticks, Exe = exe };

                    string key = exe != "" ? exe : name;
                    AppGroup g;
                    if (!groups.TryGetValue(key, out g))
                    {
                        g = new AppGroup { Key = key, Exe = exe, Kind = "background" };
                        if (exe != "") { var m = Meta(exe); g.Name = m[0]; g.Publisher = m[1]; }
                        if (string.IsNullOrEmpty(g.Name)) g.Name = name.Length > 0 ? char.ToUpper(name[0]) + name.Substring(1) : "Unknown";
                        string f;
                        if (Friendly.TryGetValue(name, out f)) g.Name = f;
                        groups[key] = g;
                    }
                    g.Pids.Add(pid);
                    g.Mem += p.WorkingSet64;
                    g.Cpu += cpu;
                    if (windows.ContainsKey(pid)) g.Kind = "app";
                    else if (g.Kind != "app" && (p.SessionId == 0 || exe == "" || exe.StartsWith(Protect.WinDir + "\\", StringComparison.OrdinalIgnoreCase))) g.Kind = "system";
                    string reason;
                    string level = Protect.Check(name, exe, pid, out reason);
                    if (Rank(level) > Rank(g.Protect)) { g.Protect = level; g.Reason = reason; }
                }
                catch { }
                finally { p.Dispose(); }
            }
            prev.Clear();
            foreach (var kv in seenNow) prev[kv.Key] = kv.Value;
            prevAt = now;
            return groups.Values.OrderByDescending(g => g.Mem).ToList();
        }

        static int Rank(string p) { return p == "blocked" ? 2 : p == "caution" ? 1 : 0; }

        // Graceful close (WM_CLOSE to the app's windows) or force (terminate every process).
        public static string Stop(AppGroup g, bool force, out int count)
        {
            count = 0;
            if (g.Protect == "blocked") return g.Reason;
            if (!force)
            {
                var wins = Native.AppWindows();
                foreach (int pid in g.Pids)
                {
                    List<IntPtr> list;
                    if (wins.TryGetValue(pid, out list)) foreach (var h in list) if (Native.PostMessage(h, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero)) count++;
                }
                return count == 0 ? "This app has no window to close. Try Force quit." : null;
            }
            int denied = 0;
            foreach (int pid in g.Pids)
            {
                if (pid <= 4 || pid == Process.GetCurrentProcess().Id) continue;
                IntPtr h = Native.OpenProcess(Native.PROCESS_TERMINATE, false, pid);
                if (h == IntPtr.Zero) { denied++; continue; }
                if (Native.TerminateProcess(h, 1)) count++; else denied++;
                Native.CloseHandle(h);
            }
            if (count == 0 && denied > 0) return "Windows needs admin rights to stop this one. Restart Tidy Lite as admin.";
            return null;
        }
    }

    // ───────── disk scanner ─────────
    internal class DirNode
    {
        public string Name, Path;
        public DirNode Parent;
        public long Size, Own;
        public int Files;
        public List<DirNode> Children = new List<DirNode>();
    }

    internal class FileItem
    {
        public string Path, Name;
        public long Size;
        public DateTime Modified;
        public bool IsDir;
        public bool Checked;
    }

    internal class Scanner
    {
        public volatile bool Running, Cancel;
        public long Files, Bytes, Denied;
        public string Current = "";
        public DirNode Root;
        readonly object bigLock = new object();
        readonly List<FileItem> big = new List<FileItem>();
        long bigMin;
        const int Keep = 400;

        public void Start(string path, Action done)
        {
            Cancel = false; Running = true; Files = 0; Bytes = 0; Denied = 0;
            lock (bigLock) { big.Clear(); bigMin = 0; }
            Root = new DirNode { Name = path, Path = path };
            var t = new Thread(() =>
            {
                try { Walk(Root); Sum(Root); }
                finally { Running = false; done(); }
            });
            t.IsBackground = true;
            t.Priority = ThreadPriority.BelowNormal;
            t.Start();
        }

        void Walk(DirNode node)
        {
            if (Cancel) return;
            Current = node.Path;
            DirectoryInfo di = new DirectoryInfo(node.Path);
            FileSystemInfo[] entries;
            try { entries = di.GetFileSystemInfos(); }
            catch { Interlocked.Increment(ref Denied); return; }
            foreach (var e in entries)
            {
                try
                {
                    if ((e.Attributes & FileAttributes.ReparsePoint) != 0) continue; // junctions & links
                    if ((e.Attributes & FileAttributes.Directory) != 0)
                    {
                        node.Children.Add(new DirNode { Name = e.Name, Path = e.FullName, Parent = node });
                        continue;
                    }
                    var f = (FileInfo)e;
                    long size = ((int)f.Attributes & 0x441000) != 0 ? 0 : f.Length; // cloud-only placeholders take no space
                    node.Own += size; node.Files++;
                    Files++; Bytes += size;
                    if (size > bigMin) AddBig(new FileItem { Path = f.FullName, Name = f.Name, Size = size, Modified = f.LastWriteTime });
                }
                catch { }
            }
            foreach (var c in node.Children) Walk(c);
        }

        void AddBig(FileItem f)
        {
            lock (bigLock)
            {
                big.Add(f);
                if (big.Count > Keep * 2)
                {
                    big.Sort((a, b) => b.Size.CompareTo(a.Size));
                    big.RemoveRange(Keep, big.Count - Keep);
                    bigMin = big[big.Count - 1].Size;
                }
            }
        }

        static long Sum(DirNode n)
        {
            n.Size = n.Own;
            foreach (var c in n.Children) n.Size += Sum(c);
            n.Children.Sort((a, b) => b.Size.CompareTo(a.Size));
            if (n.Size < 512 * 1024 && n.Parent != null) n.Children = new List<DirNode>(); // forget tiny detail
            return n.Size;
        }

        public List<FileItem> Biggest(int n)
        {
            lock (bigLock) return big.OrderByDescending(f => f.Size).Take(n).Select(f => new FileItem { Path = f.Path, Name = f.Name, Size = f.Size, Modified = f.Modified }).ToList();
        }

        public void Forget(string path, long size)
        {
            lock (bigLock) big.RemoveAll(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase) || f.Path.StartsWith(path + "\\", StringComparison.OrdinalIgnoreCase));
            var n = Find(System.IO.Path.GetDirectoryName(path));
            if (n == null) return;
            n.Children.RemoveAll(c => c.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            for (var x = n; x != null; x = x.Parent) x.Size = Math.Max(0, x.Size - size);
        }

        public DirNode Find(string path)
        {
            if (Root == null || path == null) return null;
            if (path.TrimEnd('\\').Equals(Root.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return Root;
            var parent = Find(System.IO.Path.GetDirectoryName(path));
            if (parent == null) return null;
            return parent.Children.FirstOrDefault(c => c.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ───────── Recycle Bin ─────────
    internal static class Trash
    {
        // Moves to the Recycle Bin (or deletes forever). Returns null on success, or a friendly error.
        public static string Delete(string path, bool permanent)
        {
            string why = Protect.PathProblem(path);
            if (why != null) return why;
            if (!File.Exists(path) && !Directory.Exists(path)) return "It's already gone.";
            var op = new Native.SHFILEOPSTRUCT
            {
                wFunc = 3,
                pFrom = path + "\0\0",
                fFlags = (ushort)(0x0004 | 0x0010 | 0x0400 | 0x0200 | (permanent ? 0 : 0x0040)),
            };
            int r = Native.SHFileOperation(ref op);
            if (r != 0) return r == 5 ? "Access denied - try running as admin." : "It's open in another program, or Windows couldn't remove it.";
            if (op.fAnyOperationsAborted) return "Cancelled.";
            if (File.Exists(path) || Directory.Exists(path)) return "Windows couldn't remove it (it may be in use).";
            return null;
        }

        // The Recycle Bin silently deletes items bigger than its quota; refuse those unless permanent.
        public static bool TooBigForBin(string path, long size)
        {
            try { return size > new DriveInfo(Path.GetPathRoot(path)).TotalSize / 20; } catch { return false; }
        }

        public static string Restore(string original)
        {
            if (File.Exists(original) || Directory.Exists(original)) return "Something with that name is already there.";
            string sid = WindowsIdentity.GetCurrent().User.Value;
            string dir = Path.Combine(Path.GetPathRoot(original), "$Recycle.Bin", sid);
            string bestI = null; long bestTime = 0;
            try
            {
                foreach (var f in Directory.GetFiles(dir, "$I*"))
                {
                    byte[] d;
                    try { d = File.ReadAllBytes(f); } catch { continue; }
                    if (d.Length < 28) continue;
                    long ver = BitConverter.ToInt64(d, 0), when = BitConverter.ToInt64(d, 16);
                    string name;
                    if (ver == 2) { int n = BitConverter.ToInt32(d, 24); if (28 + n * 2 > d.Length) continue; name = Encoding.Unicode.GetString(d, 28, n * 2).TrimEnd('\0'); }
                    else if (ver == 1) name = Encoding.Unicode.GetString(d, 24, d.Length - 24).Split('\0')[0];
                    else continue;
                    if (name.Equals(original, StringComparison.OrdinalIgnoreCase) && when > bestTime) { bestTime = when; bestI = f; }
                }
            }
            catch { return "Couldn't open the Recycle Bin."; }
            if (bestI == null) return "Not found in the Recycle Bin.";
            string rFile = Path.Combine(Path.GetDirectoryName(bestI), "$R" + Path.GetFileName(bestI).Substring(2));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(original));
                if (Directory.Exists(rFile)) Directory.Move(rFile, original); else File.Move(rFile, original);
                File.Delete(bestI);
                return null;
            }
            catch (Exception e) { return "Couldn't restore: " + e.Message; }
        }

        public static void BinInfo(out long size, out long count)
        {
            var info = new Native.SHQUERYRBINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.SHQUERYRBINFO)) };
            Native.SHQueryRecycleBin(null, ref info);
            size = info.i64Size; count = info.i64NumItems;
        }
    }

    // ───────── quick clean ─────────
    internal class CleanTarget
    {
        public string Id, Title, Desc;
        public long Size, Count;
        public bool Checked = true;
        public Func<IEnumerable<string>> Dirs;
        public TimeSpan OlderThan;
        public Func<string, bool> Filter;
    }

    internal static class Cleaner
    {
        static string Env(string k) { return Environment.GetEnvironmentVariable(k) ?? ""; }

        public static List<CleanTarget> Targets()
        {
            string la = Env("LOCALAPPDATA");
            return new List<CleanTarget>
            {
                new CleanTarget { Id = "temp", Title = "Temporary files", Desc = "Leftovers apps make while they work. Windows recreates them as needed.",
                    Dirs = () => new[] { Path.GetTempPath(), Path.Combine(Protect.WinDir, "Temp") }, OlderThan = TimeSpan.FromDays(1) },
                new CleanTarget { Id = "browser", Title = "Browser caches", Desc = "Saved copies of websites. Safe to clear - close your browsers first to clear the most.",
                    Dirs = () => BrowserCaches(la) },
                new CleanTarget { Id = "crash", Title = "Crash reports", Desc = "Reports saved when apps crashed. Only useful to developers.",
                    Dirs = () => new[] { Path.Combine(la, "CrashDumps"), Path.Combine(la, @"Microsoft\Windows\WER\ReportArchive"), Path.Combine(Env("ProgramData"), @"Microsoft\Windows\WER\ReportArchive") } },
                new CleanTarget { Id = "thumbs", Title = "Thumbnail cache", Desc = "Little picture previews. Windows rebuilds them automatically.",
                    Dirs = () => new[] { Path.Combine(la, @"Microsoft\Windows\Explorer") }, Filter = p => Path.GetFileName(p).StartsWith("thumbcache_", StringComparison.OrdinalIgnoreCase) },
            };
        }

        static IEnumerable<string> BrowserCaches(string la)
        {
            var list = new List<string>();
            foreach (var b in new[] { @"Google\Chrome", @"Microsoft\Edge", @"BraveSoftware\Brave-Browser", "Vivaldi" })
            {
                string ud = Path.Combine(la, b, "User Data");
                if (!Directory.Exists(ud)) continue;
                try
                {
                    foreach (var prof in Directory.GetDirectories(ud))
                    {
                        list.Add(Path.Combine(prof, "Cache"));
                        list.Add(Path.Combine(prof, "Code Cache"));
                    }
                }
                catch { }
            }
            string ff = Path.Combine(la, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(ff)) try { foreach (var p in Directory.GetDirectories(ff)) list.Add(Path.Combine(p, "cache2")); } catch { }
            return list;
        }

        public static void Visit(CleanTarget t, Action<string, long> fn)
        {
            DateTime cutoff = DateTime.Now - t.OlderThan;
            foreach (var root in t.Dirs())
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                var stack = new Stack<string>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    string d = stack.Pop();
                    try
                    {
                        foreach (var sub in Directory.GetDirectories(d))
                            if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0) stack.Push(sub);
                        foreach (var f in Directory.GetFiles(d))
                        {
                            if (t.Filter != null && !t.Filter(f)) continue;
                            try
                            {
                                var fi = new FileInfo(f);
                                if (t.OlderThan > TimeSpan.Zero && fi.LastWriteTime > cutoff) continue;
                                fn(f, fi.Length);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
        }

        public static void Measure(CleanTarget t)
        {
            long s = 0, c = 0;
            Visit(t, (p, size) => { s += size; c++; });
            t.Size = s; t.Count = c;
        }

        public static long Clean(CleanTarget t, out long skipped)
        {
            long freed = 0, sk = 0;
            Visit(t, (p, size) =>
            {
                try { File.Delete(p); freed += size; } catch { sk++; }
            });
            skipped = sk;
            return freed;
        }
    }
}
