using System;

namespace MusicWidget.Models;

public class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public bool AlwaysOnTop { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;

    public string LastPlayedTitle { get; set; } = string.Empty;
    public string LastPlayedArtist { get; set; } = string.Empty;
    public string LastPlayedSourceApp { get; set; } = "Spotify";
    public string LastPlayedSourceId { get; set; } = string.Empty;
}
