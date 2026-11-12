using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace MusicWidget.Models;

public class AppAudioSession : INotifyPropertyChanged
{
    private float _volume;
    private bool _isMuted;
    public object? RawSession { get; set; }

    public int ProcessId { get; set; }
    public string AppName { get; set; } = string.Empty;
    public ImageSource? Icon { get; set; }

    public float Volume
    {
        get => _volume;
        set
        {
            if (Math.Abs(_volume - value) > 0.001f)
            {
                _volume = Math.Clamp(value, 0f, 1f);
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormattedPercent));
                OnPropertyChanged(nameof(VolumePercentWidth));
                OnPropertyChanged(nameof(ThumbMargin));
                OnVolumeChanged?.Invoke(this, _volume);
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted != value)
            {
                _isMuted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormattedPercent));
            }
        }
    }

    public string FormattedPercent => IsMuted ? "Muted" : $"{(int)Math.Round(Volume * 100)}%";
    public double VolumePercentWidth => Volume * 100.0;
    public Thickness ThumbMargin => new Thickness(Math.Max(0, VolumePercentWidth - 4.5), 0, 0, 0);

    public Action<AppAudioSession, float>? OnVolumeChanged { get; set; }
    public Action<AppAudioSession>? OnMuteToggled { get; set; }

    public void UpdateVolumeFromSlider(float newVolume)
    {
        Volume = newVolume;
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        OnMuteToggled?.Invoke(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
