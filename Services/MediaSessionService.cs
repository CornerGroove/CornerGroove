using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;
using MusicWidget.Models;

namespace MusicWidget.Services;

public class MediaSessionService
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private readonly DispatcherTimer _playbackTickTimer;
    private DateTimeOffset _lastProgressUpdate = DateTimeOffset.Now;
    private int _tickCount = 0;

    public MediaTrackInfo CurrentTrack { get; private set; } = new();

    public event EventHandler<MediaTrackInfo>? TrackUpdated;

    public MediaSessionService()
    {
        _playbackTickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _playbackTickTimer.Tick += OnPlaybackTick;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                _sessionManager.SessionsChanged += OnSessionsChanged;
                UpdateCurrentSession(_sessionManager.GetCurrentSession());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize MediaSessionManager: {ex.Message}");
        }

        _playbackTickTimer.Start();
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        UpdateCurrentSession(sender.GetCurrentSession());
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        UpdateCurrentSession(sender.GetCurrentSession());
    }

    private void UpdateCurrentSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_currentSession != null)
        {
            try
            {
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            }
            catch { }
        }

        _currentSession = session;

        if (_currentSession != null)
        {
            try
            {
                _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                _currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
            }
            catch { }
        }

        _ = RefreshAllAsync();
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = RefreshAllAsync();
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = RefreshAllAsync();
    }

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        _ = RefreshTimelineAsync();
    }

    private void OnPlaybackTick(object? sender, EventArgs e)
    {
        _tickCount++;

        // Periodic sync to detect new sessions if current session is null or stopped
        if (_tickCount % 4 == 0 && _sessionManager != null)
        {
            try
            {
                var cur = _sessionManager.GetCurrentSession();
                if (cur != null && cur != _currentSession)
                {
                    UpdateCurrentSession(cur);
                    return;
                }
            }
            catch { }
        }

        // Advance progress smoothly if playing
        if (CurrentTrack.IsPlaying && CurrentTrack.HasMedia && CurrentTrack.Duration > TimeSpan.Zero)
        {
            var now = DateTimeOffset.Now;
            var delta = now - _lastProgressUpdate;
            if (delta.TotalMilliseconds >= 400)
            {
                var newPos = CurrentTrack.Position + delta;
                if (newPos > CurrentTrack.Duration)
                {
                    newPos = CurrentTrack.Duration;
                }
                CurrentTrack.Position = newPos;
                _lastProgressUpdate = now;
                TrackUpdated?.Invoke(this, CurrentTrack);
            }
        }
    }

    public async Task RefreshAllAsync()
    {
        if (_currentSession == null)
        {
            CurrentTrack = new MediaTrackInfo
            {
                Title = "Brak odtwarzacza",
                Artist = "Oczekiwanie na Spotify, YouTube itp.",
                HasMedia = false,
                IsPlaying = false,
                SourceAppName = "Media"
            };
            TrackUpdated?.Invoke(this, CurrentTrack);
            return;
        }

        try
        {
            var mediaProps = await _currentSession.TryGetMediaPropertiesAsync();
            var playbackInfo = _currentSession.GetPlaybackInfo();
            var timeline = _currentSession.GetTimelineProperties();
            var appId = _currentSession.SourceAppUserModelId ?? string.Empty;

            var isPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            var track = new MediaTrackInfo
            {
                Title = string.IsNullOrWhiteSpace(mediaProps?.Title) ? "Nieznany utwór" : mediaProps.Title,
                Artist = string.IsNullOrWhiteSpace(mediaProps?.Artist) ? "Nieznany wykonawca" : mediaProps.Artist,
                Album = mediaProps?.AlbumTitle ?? string.Empty,
                SourceAppName = ParseAppSource(appId, mediaProps?.Title ?? string.Empty),
                IsPlaying = isPlaying,
                CanPlayPause = playbackInfo?.Controls?.IsPlayPauseToggleEnabled ?? true,
                CanNext = playbackInfo?.Controls?.IsNextEnabled ?? true,
                CanPrevious = playbackInfo?.Controls?.IsPreviousEnabled ?? true,
                Position = timeline?.Position ?? TimeSpan.Zero,
                Duration = timeline?.EndTime ?? TimeSpan.Zero,
                HasMedia = true
            };

            _lastProgressUpdate = DateTimeOffset.Now;

            // Load thumbnail
            if (mediaProps?.Thumbnail != null)
            {
                track.Thumbnail = await LoadThumbnailAsync(mediaProps.Thumbnail);
            }

            CurrentTrack = track;
            TrackUpdated?.Invoke(this, CurrentTrack);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating track info: {ex.Message}");
        }
    }

    private async Task RefreshTimelineAsync()
    {
        if (_currentSession == null) return;

        try
        {
            var timeline = _currentSession.GetTimelineProperties();
            if (timeline != null)
            {
                CurrentTrack.Position = timeline.Position;
                CurrentTrack.Duration = timeline.EndTime;
                _lastProgressUpdate = DateTimeOffset.Now;
                TrackUpdated?.Invoke(this, CurrentTrack);
            }
        }
        catch { }
    }

    private static async Task<BitmapImage?> LoadThumbnailAsync(IRandomAccessStreamReference thumbnailRef)
    {
        try
        {
            using var ras = await thumbnailRef.OpenReadAsync();
            using var netStream = ras.AsStreamForRead();
            using var ms = new MemoryStream();
            await netStream.CopyToAsync(ms);
            ms.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static string ParseAppSource(string appId, string title)
    {
        var lower = appId.ToLowerInvariant();
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("chrome")) return "Chrome / YouTube";
        if (lower.Contains("msedge") || lower.Contains("edge")) return "Edge / YouTube";
        if (lower.Contains("brave")) return "Brave / YouTube";
        if (lower.Contains("firefox")) return "Firefox / YouTube";
        if (lower.Contains("opera")) return "Opera / YouTube";
        if (lower.Contains("applemusic") || lower.Contains("apple")) return "Apple Music";
        if (lower.Contains("tidal")) return "Tidal";
        if (lower.Contains("vlc")) return "VLC Media";
        if (lower.Contains("zune") || lower.Contains("music.ui") || lower.Contains("mediaplayer")) return "Windows Media";

        if (!string.IsNullOrEmpty(appId))
        {
            var parts = appId.Split(new[] { '\\', '/', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var name = parts[^1];
                if (name.Length > 1) return char.ToUpper(name[0]) + name[1..];
            }
        }
        return "Odtwarzacz";
    }

    public async Task PlayPauseAsync()
    {
        if (_currentSession == null) return;
        try
        {
            var pb = _currentSession.GetPlaybackInfo();
            if (pb?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                await _currentSession.TryPauseAsync();
            }
            else
            {
                await _currentSession.TryPlayAsync();
            }
        }
        catch { }
    }

    public async Task NextAsync()
    {
        if (_currentSession == null) return;
        try
        {
            await _currentSession.TrySkipNextAsync();
        }
        catch { }
    }

    public async Task PreviousAsync()
    {
        if (_currentSession == null) return;
        try
        {
            await _currentSession.TrySkipPreviousAsync();
        }
        catch { }
    }

    public async Task SeekToProgressAsync(double percentage)
    {
        if (_currentSession == null || CurrentTrack.Duration <= TimeSpan.Zero) return;
        try
        {
            var targetSeconds = (percentage / 100.0) * CurrentTrack.Duration.TotalSeconds;
            var targetTime = TimeSpan.FromSeconds(targetSeconds);
            long ticks = (long)(targetTime.TotalSeconds * 10_000_000);
            await _currentSession.TryChangePlaybackPositionAsync(ticks);
            CurrentTrack.Position = targetTime;
            TrackUpdated?.Invoke(this, CurrentTrack);
        }
        catch { }
    }
}
