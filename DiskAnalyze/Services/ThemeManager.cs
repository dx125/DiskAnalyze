using DiskAnalyze.Application.Settings;
using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DiskAnalyze.Services
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    /// <summary>
    /// Owns the active colour theme (DEVELOPMENT.md D-028). Switching replaces the colour dictionary, the
    /// first merged dictionary in App.xaml, so every DynamicResource repaints at once. It also recolours the
    /// standard Windows title bar through DWM and turns off Windows 11's rounded window corners (D-030).
    /// With <see cref="ThemePreference.System"/> it follows the Windows app mode while the app runs.
    /// </summary>
    public sealed class ThemeManager : INotifyPropertyChanged
    {
        private static readonly Uri LightColors = new("pack://application:,,,/DiskAnalyze;component/Themes/Colors.Light.xaml");
        private static readonly Uri DarkColors = new("pack://application:,,,/DiskAnalyze;component/Themes/Colors.Dark.xaml");

        // App.xaml loads the light colours, so that is what is on screen before the first switch.
        private AppTheme _appliedTheme = AppTheme.Light;
        private ThemePreference _preference = ThemePreference.System;
        private bool _followsSystem;

        private ThemeManager()
        {
        }

        public static ThemeManager Instance { get; } = new();

        /// <summary>
        /// The theme on screen. Bindings that compute brushes in code (EntityBrushConverter) include it,
        /// so they re-evaluate when it changes.
        /// </summary>
        public AppTheme ActualTheme => _appliedTheme;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void SetPreference(ThemePreference preference)
        {
            _preference = preference;
            FollowSystem(preference == ThemePreference.System);
            Apply(Resolve(preference));
        }

        /// <summary>Colours the window's title bar and border now and after every theme switch.</summary>
        public void Attach(Window window)
        {
            if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
            {
                ApplyWindowFrame(window);
            }
            else
            {
                window.SourceInitialized += (_, _) => ApplyWindowFrame(window);
            }
        }

        /// <summary>Detaches from system events; call on application exit.</summary>
        public void Shutdown() => FollowSystem(false);

        private static AppTheme Resolve(ThemePreference preference) => preference switch
        {
            ThemePreference.Light => AppTheme.Light,
            ThemePreference.Dark => AppTheme.Dark,
            _ => ReadWindowsAppTheme()
        };

        private void Apply(AppTheme theme)
        {
            if (theme == _appliedTheme)
                return;

            var app = System.Windows.Application.Current;
            var dictionaries = app.Resources.MergedDictionaries;
            dictionaries[0] = new ResourceDictionary { Source = theme == AppTheme.Dark ? DarkColors : LightColors };
            _appliedTheme = theme;

            foreach (Window window in app.Windows)
            {
                ApplyWindowFrame(window);
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActualTheme)));
        }

        // Windows stores the "app mode" choice (Settings > Personalization > Colors) per user.
        private static AppTheme ReadWindowsAppTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int value && value == 0 ? AppTheme.Dark : AppTheme.Light;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
            {
                return AppTheme.Light;
            }
        }

        private void FollowSystem(bool follow)
        {
            if (follow == _followsSystem)
                return;

            _followsSystem = follow;
            if (follow)
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            }
            else
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            }
        }

        // A change of the Windows app mode arrives as a General preference change; it may be raised on
        // another thread.
        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General)
                return;

            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (_preference == ThemePreference.System)
                {
                    Apply(ReadWindowsAppTheme());
                }
            });
        }

        private void ApplyWindowFrame(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            // Each call fails harmlessly on Windows versions that lack the attribute (corners and
            // custom colours need Windows 11).
            SetAttribute(hwnd, DwmWindowAttribute.UseImmersiveDarkMode, _appliedTheme == AppTheme.Dark ? 1 : 0);
            SetAttribute(hwnd, DwmWindowAttribute.WindowCornerPreference, DwmCornerDoNotRound);
            SetColorAttribute(hwnd, DwmWindowAttribute.CaptionColor, "Color.Chrome");
            SetColorAttribute(hwnd, DwmWindowAttribute.TextColor, "Color.Text");
            SetColorAttribute(hwnd, DwmWindowAttribute.BorderColor, "Color.LineStrong");
        }

        private static void SetColorAttribute(IntPtr hwnd, DwmWindowAttribute attribute, string colorKey)
        {
            if (System.Windows.Application.Current.TryFindResource(colorKey) is Color color)
            {
                // COLORREF is 0x00BBGGRR.
                SetAttribute(hwnd, attribute, color.R | (color.G << 8) | (color.B << 16));
            }
        }

        private static void SetAttribute(IntPtr hwnd, DwmWindowAttribute attribute, int value)
        {
            _ = DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
        }

        private const int DwmCornerDoNotRound = 1;

        private enum DwmWindowAttribute
        {
            UseImmersiveDarkMode = 20,
            WindowCornerPreference = 33,
            BorderColor = 34,
            CaptionColor = 35,
            TextColor = 36
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute attribute, ref int value, int size);
    }
}
