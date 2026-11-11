using System;
using System.Diagnostics;
using MusicWidget.Services;

namespace MusicWidget.Helpers;

public static class AppLauncher
{
    public static void OpenSourceApp(string sourceApp)
    {
        try
        {
            var lower = sourceApp.ToLowerInvariant();

            if (lower.Contains("spotify"))
            {
                // Try spotify protocol first
                try
                {
                    Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true });
                    return;
                }
                catch { }
            }
            else if (lower.Contains("youtube") || lower.Contains("chrome") || lower.Contains("edge") || lower.Contains("brave") || lower.Contains("firefox"))
            {
                Process.Start(new ProcessStartInfo("https://music.youtube.com") { UseShellExecute = true });
                return;
            }
            else if (lower.Contains("apple"))
            {
                try
                {
                    Process.Start(new ProcessStartInfo("applemusic:") { UseShellExecute = true });
                    return;
                }
                catch { }
            }

            // Fallback: try default web or media
            Process.Start(new ProcessStartInfo("https://open.spotify.com") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open source app: {ex.Message}");
        }
    }
}
