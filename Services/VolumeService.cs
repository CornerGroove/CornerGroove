using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MusicWidget.Models;

namespace MusicWidget.Services;

public class VolumeService
{
    private IAudioEndpointVolume? _endpointVolume;
    private IAudioSessionManager2? _sessionManager2;
    private IMMDevice? _defaultDevice;

    public event Action<float, bool>? MasterVolumeChanged;
    public event Action<float, bool>? VolumeChanged;

    private static readonly ConcurrentDictionary<string, ImageSource?> _iconCache = new();

    public VolumeService()
    {
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            enumerator.GetDefaultAudioEndpoint(0, 0, out _defaultDevice); // 0 = eRender, 0 = eConsole
            if (_defaultDevice != null)
            {
                var epIid = typeof(IAudioEndpointVolume).GUID;
                _defaultDevice.Activate(ref epIid, 23, IntPtr.Zero, out var epObj);
                _endpointVolume = epObj as IAudioEndpointVolume;

                var smIid = typeof(IAudioSessionManager2).GUID;
                _defaultDevice.Activate(ref smIid, 23, IntPtr.Zero, out var smObj);
                _sessionManager2 = smObj as IAudioSessionManager2;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to init VolumeService: {ex.Message}");
        }
    }

    // ==================== MASTER VOLUME ====================

    public float GetMasterVolume()
    {
        if (_endpointVolume == null) return 0.5f;
        try
        {
            _endpointVolume.GetMasterVolumeLevelScalar(out float level);
            return level;
        }
        catch
        {
            return 0.5f;
        }
    }

    public float GetVolume() => GetMasterVolume();

    public void SetMasterVolume(float level)
    {
        if (_endpointVolume == null) return;
        try
        {
            level = Math.Clamp(level, 0f, 1f);
            var guid = Guid.Empty;
            _endpointVolume.SetMasterVolumeLevelScalar(level, ref guid);
            GetMasterMute(out bool isMuted);
            MasterVolumeChanged?.Invoke(level, isMuted);
            VolumeChanged?.Invoke(level, isMuted);
        }
        catch { }
    }

    public void SetVolume(float level) => SetMasterVolume(level);

    public bool IsMasterMuted()
    {
        GetMasterMute(out bool muted);
        return muted;
    }

    public bool IsMuted() => IsMasterMuted();

    private void GetMasterMute(out bool muted)
    {
        muted = false;
        if (_endpointVolume == null) return;
        try
        {
            _endpointVolume.GetMute(out muted);
        }
        catch { }
    }

    public void ToggleMasterMute()
    {
        if (_endpointVolume == null) return;
        try
        {
            _endpointVolume.GetMute(out bool current);
            var guid = Guid.Empty;
            _endpointVolume.SetMute(!current, ref guid);
            _endpointVolume.GetMasterVolumeLevelScalar(out float level);
            MasterVolumeChanged?.Invoke(level, !current);
            VolumeChanged?.Invoke(level, !current);
        }
        catch { }
    }

    public void ToggleMute() => ToggleMasterMute();

    // ==================== PER-APP AUDIO SESSIONS (VOLUME MIXER) ====================

    public List<AppAudioSession> GetAppAudioSessions()
    {
        var result = new List<AppAudioSession>();
        if (_defaultDevice == null) Initialize();
        if (_sessionManager2 == null) return result;

        try
        {
            _sessionManager2.GetSessionEnumerator(out var sessionEnum);
            if (sessionEnum == null) return result;

            sessionEnum.GetCount(out int count);
            var seenPids = new HashSet<int>();

            for (int i = 0; i < count; i++)
            {
                sessionEnum.GetSession(i, out var sessionCtrl);
                if (sessionCtrl == null) continue;

                var sessionCtrl2 = sessionCtrl as IAudioSessionControl2;
                var simpleVol = sessionCtrl as ISimpleAudioVolume;
                if (sessionCtrl2 == null || simpleVol == null) continue;

                sessionCtrl2.GetProcessId(out uint pid);
                if (pid == 0 || pid == Environment.ProcessId) continue;
                if (!seenPids.Add((int)pid)) continue;

                string appName;
                ImageSource? icon = null;

                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    appName = GetFriendlyName(proc.ProcessName);
                    icon = GetAppIcon(proc);
                }
                catch
                {
                    continue;
                }

                simpleVol.GetMasterVolume(out float vol);
                simpleVol.GetMute(out bool mute);

                var appSession = new AppAudioSession
                {
                    ProcessId = (int)pid,
                    AppName = appName,
                    Icon = icon,
                    Volume = vol,
                    IsMuted = mute,
                    RawSession = simpleVol
                };

                appSession.OnVolumeChanged = (s, newVol) =>
                {
                    if (s.RawSession is ISimpleAudioVolume v)
                    {
                        var g = Guid.Empty;
                        v.SetMasterVolume(newVol, ref g);
                    }
                };

                appSession.OnMuteToggled = (s) =>
                {
                    if (s.RawSession is ISimpleAudioVolume v)
                    {
                        var g = Guid.Empty;
                        v.SetMute(s.IsMuted, ref g);
                    }
                };

                result.Add(appSession);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting audio sessions: {ex.Message}");
        }

        return result;
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
        if (lower.Contains("chrome")) return "Google Chrome";
        if (lower.Contains("msedge")) return "Microsoft Edge";
        if (lower.Contains("brave")) return "Brave Browser";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("opera")) return "Opera";
        if (lower.Contains("discord")) return "Discord";
        if (lower.Contains("steam")) return "Steam";
        if (lower.Contains("vlc")) return "VLC Media Player";
        if (lower.Contains("wmplayer") || lower.Contains("mediaplayer")) return "Windows Media Player";

        if (procName.Length > 1)
        {
            return char.ToUpper(procName[0]) + procName[1..];
        }
        return procName;
    }

    // ==================== COM INTERFACES ====================

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    [Guid("77AA99A0-1BD6-444F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        int GetAudioSessionControl(ref Guid AudioSessionGuid, uint StreamFlags, out IntPtr SessionControl);
        int GetSimpleAudioVolume(ref Guid AudioSessionGuid, uint StreamFlags, out IntPtr AudioVolume);
        int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);
    }

    [Guid("E2F5EE11-070C-401E-A521-A611E1125A30"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        int GetCount(out int SessionCount);
        int GetSession(int SessionIndex, out IAudioSessionControl Session);
    }

    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        int GetState(out int pRetVal);
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        int GetGroupingParam(out Guid pRetVal);
        int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        int RegisterAudioSessionNotification(IntPtr NewNotifications);
        int UnregisterAudioSessionNotification(IntPtr NewNotifications);
    }

    [Guid("bfb7ff88-7239-4fc9-8fa8-882cbc522c4d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2 : IAudioSessionControl
    {
        new int GetState(out int pRetVal);
        new int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        new int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        new int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        new int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        new int GetGroupingParam(out Guid pRetVal);
        new int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        new int RegisterAudioSessionNotification(IntPtr NewNotifications);
        new int UnregisterAudioSessionNotification(IntPtr NewNotifications);

        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        int GetProcessId(out uint pRetVal);
        int IsSystemSoundsSession();
        int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [Guid("87CE5498-68D6-44E5-AC21-427D7C101470"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        int SetMasterVolume(float fLevel, ref Guid EventContext);
        int GetMasterVolume(out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid EventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }
}
