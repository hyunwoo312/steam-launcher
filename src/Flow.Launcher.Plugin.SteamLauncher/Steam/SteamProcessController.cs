using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public sealed class SteamProcessController(ISteamPathResolver pathResolver) : ISteamProcessController
{
    private static readonly HashSet<string> SteamInternalHelpers = new(StringComparer.OrdinalIgnoreCase)
    {
        "steamwebhelper",
        "steamservice",
        "GameOverlayUI"
    };

    // Order matters: steam.exe first so its tree-kill cleans up children before the helpers are killed individually.
    private static readonly string[] SteamProcessNames =
    [
        "steam",
        "steamwebhelper",
        "GameOverlayUI",
        "steamservice"
    ];

    public void KillAllSteamProcesses(TimeSpan timeout)
    {
        foreach (var name in SteamProcessNames)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try
                {
                    p.Kill(entireProcessTree: string.Equals(name, "steam", StringComparison.OrdinalIgnoreCase));
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                finally { p.Dispose(); }
            }
        }

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (!AnySteamProcessRunning()) return;
            Thread.Sleep(250);
        }

        var remaining = new List<string>();
        foreach (var n in SteamProcessNames)
        {
            var procs = Process.GetProcessesByName(n);
            try { if (procs.Length > 0) remaining.Add(n); }
            finally { foreach (var p in procs) { try { p.Dispose(); } catch { } } }
        }
        if (remaining.Count > 0)
            throw new InvalidOperationException(
                $"Steam processes still running after {timeout.TotalSeconds:0.#}s: {string.Join(", ", remaining)}");
    }

    private static bool AnySteamProcessRunning()
    {
        foreach (var name in SteamProcessNames)
        {
            var procs = Process.GetProcessesByName(name);
            try
            {
                if (procs.Length > 0) return true;
            }
            finally
            {
                foreach (var p in procs) { try { p.Dispose(); } catch { } }
            }
        }
        return false;
    }

    public void LaunchSteamCold()
    {
        try
        {
            SteamProcessRunner.Launch(SteamUriBuilder.LibraryHome());
            return;
        }
        catch
        {
            // URI handler may not be registered after a fresh kill — fall back to direct exe.
        }

        LaunchSteamDirect();
    }

    private void LaunchSteamDirect()
    {
        var installPath = pathResolver.GetSteamInstallPath();
        var exe = Path.Combine(installPath, "steam.exe");
        if (!File.Exists(exe))
            throw new SteamPathNotFoundException();

        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false
        });
    }

    public bool IsGameRunning()
    {
        var steamPids = new HashSet<int>();
        foreach (var p in Process.GetProcessesByName("steam"))
        {
            try { steamPids.Add(p.Id); }
            finally { try { p.Dispose(); } catch { } }
        }
        if (steamPids.Count == 0) return false;

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (SteamInternalHelpers.Contains(p.ProcessName)) continue;
                if (string.Equals(p.ProcessName, "steam", StringComparison.OrdinalIgnoreCase)) continue;

                var parentPid = TryGetParentPid(p);
                if (parentPid is { } pid && steamPids.Contains(pid)) return true;
            }
            catch { }
            finally { try { p.Dispose(); } catch { } }
        }
        return false;
    }

    // Steam's desktop UI, settings, friends list and Big Picture are all SDL_app windows in
    // steamwebhelper, so only the title separates them. "Big Picture" is Valve's product
    // name and is left untranslated in most locales; if a locale does translate it,
    // detection simply fails and the caller falls back to opening rather than toggling.
    private const string SteamWindowClass = "SDL_app";
    private const string BigPictureTitleMarker = "Big Picture";

    public bool IsBigPictureRunning()
    {
        var found = false;

        // Held in a local so the delegate cannot be collected while EnumWindows runs.
        EnumWindowsProc callback = (hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            if (!string.Equals(GetWindowClass(hWnd), SteamWindowClass, StringComparison.Ordinal)) return true;
            if (!GetWindowTitle(hWnd).Contains(BigPictureTitleMarker, StringComparison.OrdinalIgnoreCase)) return true;

            found = true;
            return false; // stop enumerating
        };

        try { EnumWindows(callback, IntPtr.Zero); }
        catch (EntryPointNotFoundException) { return false; }
        catch (DllNotFoundException) { return false; }

        return found;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetWindowText(IntPtr hWnd, ref char lpString, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetClassName(IntPtr hWnd, ref char lpClassName, int nMaxCount);

    private static string GetWindowTitle(IntPtr hWnd)
    {
        Span<char> buffer = stackalloc char[256];
        var length = GetWindowText(hWnd, ref MemoryMarshal.GetReference(buffer), buffer.Length);
        return length > 0 ? new string(buffer[..length]) : string.Empty;
    }

    private static string GetWindowClass(IntPtr hWnd)
    {
        Span<char> buffer = stackalloc char[256];
        var length = GetClassName(hWnd, ref MemoryMarshal.GetReference(buffer), buffer.Length);
        return length > 0 ? new string(buffer[..length]) : string.Empty;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);

    private static int? TryGetParentPid(Process process)
    {
        try
        {
            var pbi = default(ProcessBasicInformation);
            var status = NtQueryInformationProcess(
                process.Handle, 0, ref pbi,
                Marshal.SizeOf<ProcessBasicInformation>(), out _);
            if (status != 0) return null;
            return pbi.InheritedFromUniqueProcessId.ToInt32();
        }
        catch { return null; }
    }
}
