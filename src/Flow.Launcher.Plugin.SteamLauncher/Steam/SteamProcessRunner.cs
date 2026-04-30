using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public static class SteamProcessRunner
{
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    public static void Launch(string steamUri)
    {
        if (string.IsNullOrEmpty(steamUri) || !steamUri.StartsWith("steam://", StringComparison.Ordinal))
            throw new ArgumentException("Only steam:// URIs are allowed", nameof(steamUri));

        // Without AllowSetForegroundWindow, Windows minimizes Steam after URI activation since
        // Flow Launcher is the foreground process.
        foreach (var p in Process.GetProcessesByName("steam"))
        {
            try { AllowSetForegroundWindow((uint)p.Id); }
            catch { }
            finally { p.Dispose(); }
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = steamUri,
            UseShellExecute = true
        });

        // ASFW alone isn't enough for chat/DM windows on some setups — pull forward via
        // AttachThreadInput after Steam has had time to create the target window.
        _ = Task.Run(async () =>
        {
            await Task.Delay(250).ConfigureAwait(false);
            try { BringSteamToForeground(); } catch { }
        });
    }

    private static void BringSteamToForeground()
    {
        using var steam = Process.GetProcessesByName("steam").FirstOrDefault();
        if (steam is null) return;

        var hwnd = steam.MainWindowHandle;
        if (hwnd == IntPtr.Zero) return;

        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);

        var foregroundHwnd = GetForegroundWindow();
        if (foregroundHwnd == IntPtr.Zero || foregroundHwnd == hwnd) return;

        var currentThread = GetCurrentThreadId();
        var foregroundThread = GetWindowThreadProcessId(foregroundHwnd, out _);

        if (foregroundThread != 0 && foregroundThread != currentThread)
        {
            AttachThreadInput(currentThread, foregroundThread, true);
            try { SetForegroundWindow(hwnd); }
            finally { AttachThreadInput(currentThread, foregroundThread, false); }
        }
        else
        {
            SetForegroundWindow(hwnd);
        }
    }
}
