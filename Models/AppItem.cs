using System;
using System.Windows.Media;

namespace MusicWidget.Models;

public class AppItem
{
    public string AppName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public ImageSource? Icon { get; set; }
    public IntPtr HWnd { get; set; }
    public int ProcessId { get; set; }
}
