using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MusicWidget.Models;

namespace MusicWidget.Services;

public class RunningAppsService
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int SW_RESTORE = 9;

    private static readonly ConcurrentDictionary<string, ImageSource?> _iconCache = new();

    private static readonly HashSet<string> _systemProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "dwm", "explorer", "shellexperiencehost", "searchhost", "textinputhost",
        "lockapp", "systemsettings", "applicationframehost", "startmenuexperiencehost",
        "cortana", "taskmgr", "musicwidget", "powershell", "conhost", "cmd"
    };

    private static readonly HashSet<string> _ignoredTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Program Manager", "Settings", "Windows Input Experience", "Task View",
        "NVIDIA GeForce Overlay", "Microsoft Text Input Application", ""
    };

    public List<AppItem> GetRunningApps()
    {
        var result = new List<AppItem>();
        var seenProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int currentPid = Environment.ProcessId;

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) return true;

            if (!GetWindowRect(hWnd, out var rect)) return true;
            if ((rect.Right - rect.Left < 100) || (rect.Bottom - rect.Top < 100)) return true;

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            string title = sb.ToString().Trim();

            if (string.IsNullOrWhiteSpace(title) || _ignoredTitles.Contains(title)) return true;

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == currentPid || pid == 0) return true;

            try
            {
                using var proc = Process.GetProcessById((int)pid);
                string procName = proc.ProcessName;

                if (_systemProcessNames.Contains(procName)) return true;

                // Deduplicate per process name (e.g. 1 entry for Chrome, 1 for Spotify)
                if (!seenProcesses.Add(procName)) return true;

                string friendlyName = GetFriendlyName(procName);
                var icon = GetAppIcon(proc);

                result.Add(new AppItem
                {
                    AppName = friendlyName,
                    WindowTitle = title,
                    Icon = icon,
                    HWnd = hWnd,
                    ProcessId = (int)pid
                });
            }
            catch { }

            return true;
        }, IntPtr.Zero);

        return result;
    }

    public static void ActivateWindow(IntPtr hWnd)
    {
        try
        {
            ShowWindow(hWnd, SW_RESTORE);
            SetForegroundWindow(hWnd);
        }
        catch { }
    }

    private static ImageSource? GetAppIcon(Process proc)
    {
        try
        {
            string? exePath = null;
            try
            {
                exePath = proc.MainModule?.FileName;
            }
            catch { }

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return null;

            return _iconCache.GetOrAdd(exePath, path =>
            {
                try
                {
                    using var sysIcon = Icon.ExtractAssociatedIcon(path);
                    if (sysIcon != null)
                    {
                        var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                            sysIcon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bitmapSource.Freeze();
                        return bitmapSource;
                    }
                }
                catch { }
                return null;
            });
        }
        catch
        {
            return null;
        }
    }

    private static string GetFriendlyName(string procName)
    {
        var lower = procName.ToLowerInvariant();
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("msedge")) return "Edge";
        if (lower.Contains("brave")) return "Brave";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("opera")) return "Opera";
        if (lower.Contains("discord")) return "Discord";
        if (lower.Contains("code")) return "VS Code";
        if (lower.Contains("steam")) return "Steam";
        if (lower.Contains("telegram")) return "Telegram";
        if (lower.Contains("vlc")) return "VLC";
        if (lower.Contains("notepad")) return "Notatnik";

        if (procName.Length > 1)
        {
            return char.ToUpper(procName[0]) + procName[1..];
        }
        return procName;
    }
}
