using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace MusicWidget.Helpers;

public static class AutoStartHelper
{
    private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string APP_NAME = "MusicWidget";

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, false);
            return key?.GetValue(APP_NAME) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(APP_NAME, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(APP_NAME, false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set autostart: {ex.Message}");
        }
    }
}
