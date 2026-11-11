using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MusicWidget.Helpers;
using MusicWidget.Models;
using MusicWidget.Services;
using MusicWidget.Themes;

namespace MusicWidget;

public partial class MainWindow : Window
{
    private readonly MediaSessionService _mediaService;
    private readonly VolumeService _volumeService;
    private readonly SettingsService _settingsService;
    private TrayIconManager? _trayManager;

    private readonly DispatcherTimer _cornerCheckTimer;
    private readonly DispatcherTimer _hideTimer;
    private readonly DispatcherTimer _volumeHideTimer;

    private Storyboard? _waveStoryboard;
    private Storyboard? _vinylStoryboard;
    private bool _isPlaybackAnimating = false;

    private bool _isShown = false;
    private bool _isPinned = false;
    private bool _isAnimating = false;
    private bool _isUpperPanelOpen = false;
    private bool _isSettingsActive = false;

    private double _dpiX = 1.0;
    private double _dpiY = 1.0;

    private Grid? _capturedAppSlider;
    private AppAudioSession? _capturedAppSession;

    private const string PLAY_ICON_DATA = "M8 5v14l11-7z";
    private const string PAUSE_ICON_DATA = "M6 19h4V5H6v14zm8-14v14h4V5h-4z";
    private const string SPEAKER_ICON_DATA = "M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02z";
    private const string MUTE_ICON_DATA = "M16.5 12c0-1.77-1.02-3.29-2.5-4.03v2.21l2.45 2.45c.03-.2.05-.41.05-.63zm2.5 0c0 .94-.2 1.82-.54 2.64l1.51 1.51C20.63 14.91 21 13.5 21 12c0-4.28-2.99-7.86-7-8.77v2.06c2.89.86 5 3.54 5 6.71zM4.27 3L3 4.27 7.73 9H3v6h4l5 5v-6.73l4.25 4.25c-.67.52-1.42.93-2.25 1.18v2.06c1.38-.31 2.63-.95 3.69-1.81L19.73 21 21 19.73l-9-9L4.27 3zM12 4L9.91 6.09 12 8.18V4z";

    public MainWindow()
    {
        InitializeComponent();

        _settingsService = new SettingsService();
        _mediaService = new MediaSessionService();
        _mediaService.TrackUpdated += OnTrackUpdated;

        _volumeService = new VolumeService();
        _volumeService.MasterVolumeChanged += (vol, muted) => Dispatcher.Invoke(() => UpdateMasterVolumeUI(vol, muted));

        _hideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1300)
        };
        _hideTimer.Tick += (s, e) =>
        {
            _hideTimer.Stop();
            if (!_isPinned && !_isSettingsActive && !IsMouseOverWidget())
            {
                SlideOut();
            }
        };

        _volumeHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(450)
        };
        _volumeHideTimer.Tick += (s, e) =>
        {
            _volumeHideTimer.Stop();
            if (!_isSettingsActive)
            {
                HideUpperPanel();
            }
        };

        _cornerCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _cornerCheckTimer.Tick += OnCornerCheckTick;

        ThemeManager.AuraColorsChanged += OnAuraColorsChanged;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply saved theme instantly on launch
        ThemeManager.ApplyTheme(_settingsService.Settings.Theme, smooth: false);
        HighlightActiveThemeChip(_settingsService.Settings.Theme);

        // Apply saved pin state
        _isPinned = _settingsService.Settings.AlwaysOnTop;
        UpdatePinIcon();

        // Calculate DPI
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            _dpiX = source.CompositionTarget.TransformToDevice.M11;
            _dpiY = source.CompositionTarget.TransformToDevice.M22;
        }

        // Initialize Storyboards
        try
        {
            _waveStoryboard = (Storyboard)FindResource("WaveStoryboard");
            _vinylStoryboard = (Storyboard)FindResource("VinylStoryboard");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load storyboards: {ex.Message}");
        }

        // Initialize Tray Icon
        try
        {
            _trayManager = new TrayIconManager(this);
            _trayManager.Initialize();
            _trayManager.LeftClicked += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_isShown) SlideOut();
                    else SlideIn();
                });
            };
            _trayManager.RightClicked += ShowTrayContextMenu;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tray icon error: {ex.Message}");
        }

        // Position window in hidden state initially
        PositionWindow(hidden: true);

        ProgressBarContainer.SizeChanged += (s, e) => UpdateProgressBar(_mediaService.CurrentTrack.ProgressPercentage);
        MasterSliderContainer.SizeChanged += (s, e) => UpdateMasterVolumeUI(_volumeService.GetMasterVolume(), _volumeService.IsMasterMuted());

        // Initial Master Volume
        UpdateMasterVolumeUI(_volumeService.GetMasterVolume(), _volumeService.IsMasterMuted());

        // Start checking corners
        _cornerCheckTimer.Start();

        // Initialize Media Sessions
        await _mediaService.InitializeAsync();
    }

    private void PositionWindow(bool hidden)
    {
        var workArea = SystemParameters.WorkArea;
        Top = workArea.Bottom - Height - 12;

        if (hidden)
        {
            Left = workArea.Right - 8;
            _isShown = false;
        }
        else
        {
            Left = workArea.Right - Width - 12;
            _isShown = true;
        }
    }

    private void OnCornerCheckTick(object? sender, EventArgs e)
    {
        if (_isShown || _isAnimating) return;

        if (NativeMethods.GetCursorPos(out var pt))
        {
            var workArea = SystemParameters.WorkArea;
            double dipX = pt.X / _dpiX;
            double dipY = pt.Y / _dpiY;

            // Trigger area: Within 14 pixels of right screen edge and near bottom
            if (dipX >= workArea.Right - 14 && dipY >= workArea.Bottom - 140 && dipY <= workArea.Bottom + 10)
            {
                SlideIn();
            }
        }
    }

    public void SlideIn()
    {
        if (_isShown && !_isAnimating) return;
        _isAnimating = true;

        var workArea = SystemParameters.WorkArea;
        double targetLeft = workArea.Right - Width - 12;

        var anim = new DoubleAnimation
        {
            From = Left,
            To = targetLeft,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        anim.Completed += (s, e) =>
        {
            _isAnimating = false;
            _isShown = true;
            Left = targetLeft;
        };

        Topmost = true;
        BeginAnimation(LeftProperty, anim);
    }

    public void SlideOut()
    {
        if (!_isShown || _isPinned || _isAnimating || _isSettingsActive) return;
        _isAnimating = true;

        HideUpperPanel();

        var workArea = SystemParameters.WorkArea;
        double targetLeft = workArea.Right - 8;

        var anim = new DoubleAnimation
        {
            From = Left,
            To = targetLeft,
            Duration = TimeSpan.FromMilliseconds(230),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        anim.Completed += (s, e) =>
        {
            _isAnimating = false;
            _isShown = false;
            Left = targetLeft;
        };

        BeginAnimation(LeftProperty, anim);
    }

    private bool IsMouseOverWidget()
    {
        if (NativeMethods.GetCursorPos(out var pt))
        {
            double dipX = pt.X / _dpiX;
            double dipY = pt.Y / _dpiY;

            double activeTop = _isUpperPanelOpen ? Top : (Top + Height - 130);

            return dipX >= Left && dipX <= (Left + Width) &&
                   dipY >= activeTop && dipY <= (Top + Height);
        }
        return false;
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _hideTimer.Stop();
        if (!_isShown)
        {
            SlideIn();
        }
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isPinned && !_isSettingsActive)
        {
            _hideTimer.Stop();
            _hideTimer.Start();
        }
    }

    private void EdgeHandle_MouseEnter(object sender, MouseEventArgs e)
    {
        SlideIn();
    }

    // ==================== UPPER PANEL: MIXER & SETTINGS VIEWS ====================

    private void ShowMixerPanel()
    {
        _isSettingsActive = false;
        ViewSettings.Visibility = Visibility.Collapsed;
        ViewMixer.Visibility = Visibility.Visible;

        // Refresh per-app audio sessions
        try
        {
            var sessions = _volumeService.GetAppAudioSessions();
            AppMixerItemsControl.ItemsSource = sessions;
            TxtAppCount.Text = $"{sessions.Count} Active";
            TxtNoAudioApps.Visibility = sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch { }

        // Update Master Volume
        UpdateMasterVolumeUI(_volumeService.GetMasterVolume(), _volumeService.IsMasterMuted());

        ShowUpperPanel();
    }

    private void ShowSettingsPanel()
    {
        _isSettingsActive = true;
        _volumeHideTimer.Stop();
        ViewMixer.Visibility = Visibility.Collapsed;
        ViewSettings.Visibility = Visibility.Visible;

        ChkAlwaysOnTop.IsChecked = _isPinned;
        ChkStartWithWindows.IsChecked = AutoStartHelper.IsAutoStartEnabled();

        ShowUpperPanel();
    }

    private void ShowUpperPanel()
    {
        if (_isUpperPanelOpen) return;
        _isUpperPanelOpen = true;
        _volumeHideTimer.Stop();

        VolumePopupCard.IsHitTestVisible = true;

        var animFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
        var animSlide = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        VolumePopupCard.BeginAnimation(OpacityProperty, animFade);
        VolumePopupTransform.BeginAnimation(TranslateTransform.YProperty, animSlide);
    }

    private void HideUpperPanel()
    {
        if (!_isUpperPanelOpen) return;
        _isUpperPanelOpen = false;
        _isSettingsActive = false;
        VolumePopupCard.IsHitTestVisible = false;

        var animFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160));
        var animSlide = new DoubleAnimation(0, 10, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        VolumePopupCard.BeginAnimation(OpacityProperty, animFade);
        VolumePopupTransform.BeginAnimation(TranslateTransform.YProperty, animSlide);
    }

    private void BtnVolume_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_isSettingsActive)
        {
            ShowMixerPanel();
        }
    }

    private void BtnVolume_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isSettingsActive)
        {
            _volumeHideTimer.Stop();
            _volumeHideTimer.Start();
        }
    }

    private void BtnVolume_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpperPanelOpen && ViewMixer.Visibility == Visibility.Visible)
        {
            HideUpperPanel();
        }
        else
        {
            ShowMixerPanel();
        }
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpperPanelOpen && ViewSettings.Visibility == Visibility.Visible)
        {
            HideUpperPanel();
        }
        else
        {
            if (!_isShown) SlideIn();
            ShowSettingsPanel();
        }
    }

    private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
    {
        HideUpperPanel();
    }

    private void VolumePopupCard_MouseEnter(object sender, MouseEventArgs e)
    {
        _volumeHideTimer.Stop();
        _hideTimer.Stop();
    }

    private void VolumePopupCard_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isSettingsActive)
        {
            _volumeHideTimer.Stop();
            _volumeHideTimer.Start();
        }
        if (!_isPinned && !_isSettingsActive)
        {
            _hideTimer.Stop();
            _hideTimer.Start();
        }
    }

    // ==================== MASTER VOLUME ====================

    private void UpdateMasterVolumeUI(float level, bool isMuted)
    {
        int pct = (int)Math.Round(level * 100);
        TxtMasterVolumePercent.Text = isMuted ? "Muted" : $"{pct}%";

        if (MasterSliderContainer.ActualWidth > 0)
        {
            double fillWidth = level * MasterSliderContainer.ActualWidth;
            MasterSliderFill.Width = Math.Clamp(fillWidth, 0, MasterSliderContainer.ActualWidth);
        }

        if (isMuted)
        {
            IconMasterSpeaker.Data = Geometry.Parse(MUTE_ICON_DATA);
            MasterSliderFill.SetResourceReference(Border.BackgroundProperty, "TextMutedBrush");
        }
        else
        {
            IconMasterSpeaker.Data = Geometry.Parse(SPEAKER_ICON_DATA);
            MasterSliderFill.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
        }
    }

    private void BtnMasterMute_Click(object sender, RoutedEventArgs e)
    {
        _volumeService.ToggleMasterMute();
    }

    private void MasterSlider_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            MasterSliderContainer.CaptureMouse();
            UpdateMasterVolumeFromMouse(e);
        }
    }

    private void MasterSlider_MouseMove(object sender, MouseEventArgs e)
    {
        if (MasterSliderContainer.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateMasterVolumeFromMouse(e);
        }
    }

    private void UpdateMasterVolumeFromMouse(MouseEventArgs e)
    {
        if (MasterSliderContainer.ActualWidth <= 0) return;
        var pos = e.GetPosition(MasterSliderContainer);
        double pct = pos.X / MasterSliderContainer.ActualWidth;
        pct = Math.Clamp(pct, 0.0, 1.0);
        _volumeService.SetMasterVolume((float)pct);
        UpdateMasterVolumeUI((float)pct, _volumeService.IsMasterMuted());
    }

    // ==================== PER-APP VOLUME SLIDERS ====================

    private void AppSlider_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Grid grid && grid.Tag is AppAudioSession session && e.LeftButton == MouseButtonState.Pressed)
        {
            _capturedAppSlider = grid;
            _capturedAppSession = session;
            grid.CaptureMouse();
            UpdateAppVolumeFromMouse(grid, session, e);
        }
    }

    private void AppSlider_MouseMove(object sender, MouseEventArgs e)
    {
        if (_capturedAppSlider != null && _capturedAppSession != null && _capturedAppSlider.IsMouseCaptured)
        {
            UpdateAppVolumeFromMouse(_capturedAppSlider, _capturedAppSession, e);
        }
    }

    private void UpdateAppVolumeFromMouse(Grid grid, AppAudioSession session, MouseEventArgs e)
    {
        if (grid.ActualWidth <= 0) return;
        var pos = e.GetPosition(grid);
        double pct = pos.X / grid.ActualWidth;
        pct = Math.Clamp(pct, 0.0, 1.0);
        session.UpdateVolumeFromSlider((float)pct);
    }

    private void AppMuteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AppAudioSession session)
        {
            session.ToggleMute();
        }
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);
        if (MasterSliderContainer.IsMouseCaptured)
        {
            MasterSliderContainer.ReleaseMouseCapture();
        }
        if (_capturedAppSlider != null && _capturedAppSlider.IsMouseCaptured)
        {
            _capturedAppSlider.ReleaseMouseCapture();
            _capturedAppSlider = null;
            _capturedAppSession = null;
        }
    }

    // ==================== MUSIC TRACK UPDATES & LAST PLAYED ====================

    private void OnTrackUpdated(object? sender, MediaTrackInfo track)
    {
        Dispatcher.Invoke(() =>
        {
            if (track.HasMedia && !string.IsNullOrWhiteSpace(track.Title) && track.Title != "No Media Playing")
            {
                // Active media playing
                TxtTitle.Text = track.Title;
                TxtArtist.Text = track.Artist;
                TxtSourceApp.Text = track.SourceAppName;
                BtnOpenLastPlayed.Visibility = Visibility.Collapsed;

                // Save last played
                _settingsService.Settings.LastPlayedTitle = track.Title;
                _settingsService.Settings.LastPlayedArtist = track.Artist;
                _settingsService.Settings.LastPlayedSourceApp = track.SourceAppName;
                _settingsService.Save();

                // Thumbnail / Vinyl Sleeve Artwork
                if (track.Thumbnail != null)
                {
                    ImgThumbnail.Source = track.Thumbnail;
                    ImgThumbnail.Visibility = Visibility.Visible;
                    PlaceholderIcon.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ImgThumbnail.Source = null;
                    ImgThumbnail.Visibility = Visibility.Collapsed;
                    PlaceholderIcon.Visibility = Visibility.Visible;
                }
            }
            else
            {
                // Idle state: show "Open Last Played"
                TxtTitle.Text = "No Media Playing";
                string lastTitle = _settingsService.Settings.LastPlayedTitle;
                string lastArtist = _settingsService.Settings.LastPlayedArtist;
                string lastSource = _settingsService.Settings.LastPlayedSourceApp;

                TxtArtist.Text = string.IsNullOrEmpty(lastTitle) ? "Ready to listen" : $"Last: {lastArtist} • {lastTitle}";
                TxtSourceApp.Text = string.IsNullOrEmpty(lastSource) ? "Media" : lastSource;

                TxtOpenLastPlayed.Text = $"Open {lastSource}";
                BtnOpenLastPlayed.Visibility = Visibility.Visible;

                ImgThumbnail.Source = null;
                ImgThumbnail.Visibility = Visibility.Collapsed;
                PlaceholderIcon.Visibility = Visibility.Visible;
            }

            // Source Dot color
            SourceDot.Fill = track.IsPlaying
                ? (SolidColorBrush)FindResource("AccentBrush")
                : (SolidColorBrush)FindResource("TextMutedBrush");

            // Play / Pause Icon
            IconPlayPause.Data = Geometry.Parse(track.IsPlaying ? PAUSE_ICON_DATA : PLAY_ICON_DATA);
            BtnPlayPause.ToolTip = track.IsPlaying ? "Pause" : "Play";

            // Control vinyl rotation & audio wave animation
            if (track.IsPlaying)
            {
                if (!_isPlaybackAnimating)
                {
                    _isPlaybackAnimating = true;
                    _waveStoryboard?.Begin(this, isControllable: true);
                    _vinylStoryboard?.Begin(this, isControllable: true);
                }
                else
                {
                    _waveStoryboard?.Resume(this);
                    _vinylStoryboard?.Resume(this);
                }
                WaveContainer.Opacity = 1.0;
            }
            else
            {
                if (_isPlaybackAnimating)
                {
                    _waveStoryboard?.Pause(this);
                    _vinylStoryboard?.Pause(this);
                }
                WaveContainer.Opacity = 0.35;
            }

            // Enabled states
            BtnPlayPause.IsEnabled = track.CanPlayPause;
            BtnNext.IsEnabled = track.CanNext;
            BtnPrev.IsEnabled = track.CanPrevious;

            // Times
            TxtCurrentTime.Text = track.FormattedPosition;
            TxtDuration.Text = track.Duration > TimeSpan.Zero ? track.FormattedDuration : "--:--";

            // Progress Bar
            UpdateProgressBar(track.ProgressPercentage);

            // Update Tray Tooltip
            _trayManager?.UpdateTooltip($"{track.Title} - {track.Artist}");
        });
    }

    private void UpdateProgressBar(double percentage)
    {
        if (ProgressBarContainer.ActualWidth > 0)
        {
            double fillWidth = (percentage / 100.0) * ProgressBarContainer.ActualWidth;
            ProgressBarFill.Width = Math.Clamp(fillWidth, 0, ProgressBarContainer.ActualWidth);
        }
    }

    private void BtnOpenLastPlayed_Click(object sender, RoutedEventArgs e)
    {
        AppLauncher.OpenSourceApp(_settingsService.Settings.LastPlayedSourceApp);
    }

    private async void BtnPlayPause_Click(object sender, RoutedEventArgs e)
    {
        await _mediaService.PlayPauseAsync();
    }

    private async void BtnPrev_Click(object sender, RoutedEventArgs e)
    {
        await _mediaService.PreviousAsync();
    }

    private async void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        await _mediaService.NextAsync();
    }

    private async void ProgressBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ProgressBarContainer.ActualWidth <= 0) return;

        var pos = e.GetPosition(ProgressBarContainer);
        double pct = (pos.X / ProgressBarContainer.ActualWidth) * 100.0;
        pct = Math.Clamp(pct, 0, 100);

        UpdateProgressBar(pct);
        await _mediaService.SeekToProgressAsync(pct);
    }

    // ==================== THEMES & SWITCHES ====================

    private void BtnThemeDark_Click(object sender, RoutedEventArgs e) => ApplyTheme("Dark");
    private void BtnThemeLight_Click(object sender, RoutedEventArgs e) => ApplyTheme("Light");
    private void BtnThemeDracula_Click(object sender, RoutedEventArgs e) => ApplyTheme("Dracula");
    private void BtnThemeCappuccino_Click(object sender, RoutedEventArgs e) => ApplyTheme("Cappuccino");

    private void ApplyTheme(string themeName)
    {
        ThemeManager.ApplyTheme(themeName, smooth: true);
        _settingsService.Settings.Theme = themeName;
        _settingsService.Save();

        HighlightActiveThemeChip(themeName);
        UpdatePinIcon();
    }

    private void HighlightActiveThemeChip(string themeName)
    {
        var activeBrush = (Brush)FindResource("AccentBrush");
        var normalBrush = (Brush)FindResource("BorderBrush");

        BtnThemeDark.BorderBrush = themeName.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? activeBrush : normalBrush;
        BtnThemeLight.BorderBrush = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) ? activeBrush : normalBrush;
        BtnThemeDracula.BorderBrush = themeName.Equals("Dracula", StringComparison.OrdinalIgnoreCase) ? activeBrush : normalBrush;
        BtnThemeCappuccino.BorderBrush = themeName.Equals("Cappuccino", StringComparison.OrdinalIgnoreCase) ? activeBrush : normalBrush;
    }

    private void OnAuraColorsChanged(Color c1, Color c2, Color c3, bool smooth)
    {
        if (smooth)
        {
            var duration = TimeSpan.FromMilliseconds(280);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            AuraStop1?.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(c1, duration) { EasingFunction = easing });
            AuraStop2?.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(c2, duration) { EasingFunction = easing });
            AuraStop3?.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(c3, duration) { EasingFunction = easing });
        }
        else
        {
            if (AuraStop1 != null) { AuraStop1.BeginAnimation(GradientStop.ColorProperty, null); AuraStop1.Color = c1; }
            if (AuraStop2 != null) { AuraStop2.BeginAnimation(GradientStop.ColorProperty, null); AuraStop2.Color = c2; }
            if (AuraStop3 != null) { AuraStop3.BeginAnimation(GradientStop.ColorProperty, null); AuraStop3.Color = c3; }
        }
    }

    private void ChkAlwaysOnTop_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = ChkAlwaysOnTop.IsChecked == true;
        _settingsService.Settings.AlwaysOnTop = _isPinned;
        _settingsService.Save();
        UpdatePinIcon();
    }

    private void ChkStartWithWindows_Click(object sender, RoutedEventArgs e)
    {
        bool enable = ChkStartWithWindows.IsChecked == true;
        AutoStartHelper.SetAutoStart(enable);
        _settingsService.Settings.StartWithWindows = enable;
        _settingsService.Save();
    }

    private void BtnPin_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        _settingsService.Settings.AlwaysOnTop = _isPinned;
        _settingsService.Save();
        UpdatePinIcon();
    }

    private void UpdatePinIcon()
    {
        if (_isPinned)
        {
            IconPin.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "AccentBrush");
            BtnPin.ToolTip = "Unpin Widget (Enable Auto-hide)";
            _hideTimer.Stop();
        }
        else
        {
            IconPin.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "TextMutedBrush");
            BtnPin.ToolTip = "Pin Widget (Always on Top)";
            if (!IsMouseOverWidget() && !_isSettingsActive)
            {
                _hideTimer.Start();
            }
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = false;
        UpdatePinIcon();
        SlideOut();
    }

    // ==================== TRAY CONTEXT MENU ====================

    private void ShowTrayContextMenu()
    {
        Dispatcher.Invoke(() =>
        {
            var menu = new ContextMenu();

            var itemToggle = new MenuItem
            {
                Header = _isShown ? "Hide Widget" : "Show Widget"
            };
            itemToggle.Click += (s, e) =>
            {
                if (_isShown) SlideOut();
                else SlideIn();
            };
            menu.Items.Add(itemToggle);

            var itemPin = new MenuItem
            {
                Header = "Always on Top (Pinned)",
                IsCheckable = true,
                IsChecked = _isPinned
            };
            itemPin.Click += (s, e) => BtnPin_Click(s, new RoutedEventArgs());
            menu.Items.Add(itemPin);

            var itemMixer = new MenuItem
            {
                Header = "Volume Mixer"
            };
            itemMixer.Click += (s, e) =>
            {
                SlideIn();
                ShowMixerPanel();
            };
            menu.Items.Add(itemMixer);

            var itemConfigure = new MenuItem
            {
                Header = "Settings & Themes..."
            };
            itemConfigure.Click += (s, e) =>
            {
                SlideIn();
                ShowSettingsPanel();
            };
            menu.Items.Add(itemConfigure);

            var itemAutoStart = new MenuItem
            {
                Header = "Run on Windows Startup",
                IsCheckable = true,
                IsChecked = AutoStartHelper.IsAutoStartEnabled()
            };
            itemAutoStart.Click += (s, e) =>
            {
                bool newState = !itemAutoStart.IsChecked;
                AutoStartHelper.SetAutoStart(newState);
                itemAutoStart.IsChecked = newState;
                _settingsService.Settings.StartWithWindows = newState;
                _settingsService.Save();
            };
            menu.Items.Add(itemAutoStart);

            menu.Items.Add(new Separator());

            var itemExit = new MenuItem
            {
                Header = "Exit CornerGroove"
            };
            itemExit.Click += (s, e) =>
            {
                _waveStoryboard?.Stop(this);
                _vinylStoryboard?.Stop(this);
                _trayManager?.Dispose();
                Application.Current.Shutdown();
            };
            menu.Items.Add(itemExit);

            menu.IsOpen = true;
        });
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _cornerCheckTimer.Stop();
        _hideTimer.Stop();
        _volumeHideTimer.Stop();
        _waveStoryboard?.Stop(this);
        _vinylStoryboard?.Stop(this);
        _trayManager?.Dispose();
    }
}
