using System;
using System.Windows.Media;

namespace MusicWidget.Models;

public class MediaTrackInfo
{
    public string Title { get; set; } = "Brak odtwarzania";
    public string Artist { get; set; } = "Uruchom Spotify, YouTube itp.";
    public string Album { get; set; } = string.Empty;
    public ImageSource? Thumbnail { get; set; }
    public string SourceAppName { get; set; } = "Media";
    public bool IsPlaying { get; set; } = false;
    public TimeSpan Position { get; set; } = TimeSpan.Zero;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public bool CanPlayPause { get; set; } = true;
    public bool CanNext { get; set; } = true;
    public bool CanPrevious { get; set; } = true;
    public bool HasMedia { get; set; } = false;

    public double ProgressPercentage
    {
        get
        {
            if (Duration.TotalSeconds <= 0) return 0;
            var pct = (Position.TotalSeconds / Duration.TotalSeconds) * 100.0;
            return Math.Clamp(pct, 0, 100);
        }
    }

    public string FormattedPosition => FormatTime(Position);
    public string FormattedDuration => FormatTime(Duration);

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";
        }
        return $"{time.Minutes:D2}:{time.Seconds:D2}";
    }
}
