using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using MusicWidget.Helpers;

namespace MusicWidget.Helpers;

public class TrayIconManager : IDisposable
{
    private const int WM_USER = 0x0400;
    public const int WM_TRAYICON = WM_USER + 102;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpdata);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly Window _window;
    private IntPtr _hwnd;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isAdded = false;

    public event Action? LeftClicked;
    public event Action? RightClicked;

    public TrayIconManager(Window window)
    {
        _window = window;
    }

    public void Initialize()
    {
        var helper = new WindowInteropHelper(_window);
        _hwnd = helper.EnsureHandle();

        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);

        CreateIcon();
        AddTrayIcon();
    }

    private void CreateIcon()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var icoPath = Path.Combine(baseDir, "logo.ico");
            if (!File.Exists(icoPath))
            {
                icoPath = Path.Combine(Environment.CurrentDirectory, "logo.ico");
            }

            if (File.Exists(icoPath))
            {
                using var ico = new Icon(icoPath, new System.Drawing.Size(16, 16));
                _hIcon = ico.Handle;
                return;
            }

            var pngPath = Path.Combine(baseDir, "logo.png");
            if (!File.Exists(pngPath))
            {
                pngPath = Path.Combine(Environment.CurrentDirectory, "logo.png");
            }

            if (File.Exists(pngPath))
            {
                using var srcBmp = new Bitmap(pngPath);
                using var scaledBmp = new Bitmap(16, 16);
                using (var g = Graphics.FromImage(scaledBmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(srcBmp, 0, 0, 16, 16);
                }
                _hIcon = scaledBmp.GetHicon();
                return;
            }
        }
        catch
        {
            // Fall through to fallback
        }

        try
        {
            // Fallback programmatic green dot
            using var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                using var brush = new SolidBrush(System.Drawing.Color.FromArgb(16, 185, 129));
                g.FillEllipse(brush, 1, 1, 14, 14);
            }
            _hIcon = bmp.GetHicon();
        }
        catch
        {
            _hIcon = IntPtr.Zero;
        }
    }

    private void AddTrayIcon()
    {
        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "CornerGroove - Vinyl Music Dock"
        };

        _isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    public void UpdateTooltip(string tooltip)
    {
        if (!_isAdded) return;
        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_TIP,
            szTip = tooltip.Length > 120 ? tooltip[..120] : tooltip
        };
        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            var l = lParam.ToInt32();
            if (l == WM_LBUTTONUP)
            {
                LeftClicked?.Invoke();
                handled = true;
            }
            else if (l == WM_RBUTTONUP)
            {
                RightClicked?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_isAdded)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = 1001
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _isAdded = false;
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }
}
