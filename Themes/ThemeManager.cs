using System;
using System.Windows;
using System.Windows.Media;

namespace MusicWidget.Themes;

public static class ThemeManager
{
    public static string CurrentTheme { get; private set; } = "Dark";
    public static string CurrentStyleVibe { get; private set; } = "Aura";

    public static event Action<string>? ThemeChanged;
    public static event Action<Color, Color, Color, bool>? AuraColorsChanged;

    public static void ApplyTheme(string themeName, string? vibe = null, bool smooth = true)
    {
        try
        {
            CurrentTheme = themeName;
            if (!string.IsNullOrEmpty(vibe))
            {
                CurrentStyleVibe = vibe;
            }

            Color bg, cardBg, border, surface, surfaceHover, accent, accentHover;
            Color textPrimary, textSecondary, textMuted, trackBg;
            Color playBtnNormal, playBtnHover, playBtnIcon, edgeHandle;
            Color aura1, aura2, aura3;

            switch (themeName.ToLowerInvariant())
            {
                case "light":
                    bg = Color.FromRgb(244, 244, 246);
                    cardBg = Color.FromRgb(255, 255, 255);
                    border = Color.FromRgb(228, 228, 231);
                    surface = Color.FromRgb(244, 244, 245);
                    surfaceHover = Color.FromRgb(228, 228, 231);
                    accent = Color.FromRgb(13, 148, 136); // Teal
                    accentHover = Color.FromRgb(15, 118, 110);
                    textPrimary = Color.FromRgb(24, 24, 27);
                    textSecondary = Color.FromRgb(113, 113, 122);
                    textMuted = Color.FromRgb(161, 161, 170);
                    trackBg = Color.FromRgb(228, 228, 231);
                    playBtnNormal = Color.FromRgb(24, 24, 27);
                    playBtnHover = Color.FromRgb(39, 39, 42);
                    playBtnIcon = Color.FromRgb(255, 255, 255);
                    edgeHandle = Color.FromRgb(161, 161, 170);

                    aura1 = Color.FromArgb(140, 13, 148, 136);
                    aura2 = Color.FromArgb(90, 56, 189, 248);
                    aura3 = Color.FromArgb(60, 129, 140, 248);
                    break;

                case "dracula":
                    bg = Color.FromRgb(30, 31, 41);
                    cardBg = Color.FromRgb(40, 42, 54);
                    border = Color.FromRgb(68, 71, 90);
                    surface = Color.FromRgb(52, 55, 70);
                    surfaceHover = Color.FromRgb(68, 71, 90);
                    accent = Color.FromRgb(189, 147, 249); // Dracula Purple
                    accentHover = Color.FromRgb(167, 123, 243);
                    textPrimary = Color.FromRgb(248, 248, 242);
                    textSecondary = Color.FromRgb(191, 191, 208);
                    textMuted = Color.FromRgb(98, 114, 164);
                    trackBg = Color.FromRgb(68, 71, 90);
                    playBtnNormal = Color.FromRgb(80, 250, 123); // Dracula Green
                    playBtnHover = Color.FromRgb(65, 230, 105);
                    playBtnIcon = Color.FromRgb(40, 42, 54);
                    edgeHandle = Color.FromRgb(98, 114, 164);

                    aura1 = Color.FromArgb(180, 189, 147, 249);
                    aura2 = Color.FromArgb(130, 255, 121, 198);
                    aura3 = Color.FromArgb(80, 139, 233, 253);
                    break;

                case "cappuccino":
                    bg = Color.FromRgb(30, 25, 22);
                    cardBg = Color.FromRgb(42, 35, 31);
                    border = Color.FromRgb(66, 55, 49);
                    surface = Color.FromRgb(53, 44, 39);
                    surfaceHover = Color.FromRgb(74, 62, 55);
                    accent = Color.FromRgb(212, 163, 115); // Caramel Latte
                    accentHover = Color.FromRgb(197, 143, 92);
                    textPrimary = Color.FromRgb(253, 251, 247);
                    textSecondary = Color.FromRgb(215, 196, 183);
                    textMuted = Color.FromRgb(141, 123, 112);
                    trackBg = Color.FromRgb(61, 51, 45);
                    playBtnNormal = Color.FromRgb(212, 163, 115);
                    playBtnHover = Color.FromRgb(197, 143, 92);
                    playBtnIcon = Color.FromRgb(30, 25, 22);
                    edgeHandle = Color.FromRgb(141, 123, 112);

                    aura1 = Color.FromArgb(180, 212, 163, 115);
                    aura2 = Color.FromArgb(120, 245, 158, 11);
                    aura3 = Color.FromArgb(70, 154, 52, 18);
                    break;

                case "dark":
                default:
                    CurrentTheme = "Dark";
                    bg = Color.FromRgb(12, 12, 14);
                    cardBg = Color.FromRgb(19, 19, 23);
                    border = Color.FromRgb(38, 38, 46);
                    surface = Color.FromRgb(28, 28, 34);
                    surfaceHover = Color.FromRgb(38, 38, 46);
                    accent = Color.FromRgb(16, 185, 129); // Emerald
                    accentHover = Color.FromRgb(5, 150, 105);
                    textPrimary = Color.FromRgb(237, 237, 237);
                    textSecondary = Color.FromRgb(158, 158, 168);
                    textMuted = Color.FromRgb(102, 102, 114);
                    trackBg = Color.FromRgb(34, 34, 42);
                    playBtnNormal = Color.FromRgb(237, 237, 237);
                    playBtnHover = Color.FromRgb(212, 212, 216);
                    playBtnIcon = Color.FromRgb(12, 12, 14);
                    edgeHandle = Color.FromRgb(102, 102, 114);

                    aura1 = Color.FromArgb(160, 16, 185, 129);
                    aura2 = Color.FromArgb(110, 6, 182, 212);
                    aura3 = Color.FromArgb(70, 99, 102, 241);
                    break;
            }

            SetBrush("BgBrush", bg);
            SetBrush("CardBgBrush", cardBg);
            SetBrush("BorderBrush", border);
            SetBrush("SurfaceBrush", surface);
            SetBrush("SurfaceHoverBrush", surfaceHover);
            SetBrush("AccentBrush", accent);
            SetBrush("AccentHoverBrush", accentHover);
            SetBrush("TextPrimaryBrush", textPrimary);
            SetBrush("TextSecondaryBrush", textSecondary);
            SetBrush("TextMutedBrush", textMuted);
            SetBrush("TrackBgBrush", trackBg);
            SetBrush("PlayBtnNormalBrush", playBtnNormal);
            SetBrush("PlayBtnHoverBrush", playBtnHover);
            SetBrush("PlayBtnIconBrush", playBtnIcon);
            SetBrush("EdgeHandleBrush", edgeHandle);

            SetColor("AuraColor1", aura1);
            SetColor("AuraColor2", aura2);
            SetColor("AuraColor3", aura3);

            AuraColorsChanged?.Invoke(aura1, aura2, aura3, smooth);
            ThemeChanged?.Invoke(CurrentTheme);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApplyTheme exception: {ex.Message}");
        }
    }

    private static void SetBrush(string key, Color targetColor)
    {
        if (Application.Current == null) return;
        try
        {
            var brush = new SolidColorBrush(targetColor);
            brush.Freeze();
            Application.Current.Resources[key] = brush;
        }
        catch { }
    }

    private static void SetColor(string key, Color color)
    {
        if (Application.Current != null)
        {
            try
            {
                Application.Current.Resources[key] = color;
            }
            catch { }
        }
    }
}
