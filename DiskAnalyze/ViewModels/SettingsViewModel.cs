using DiskAnalyze.Application.Settings;
using DiskAnalyze.Core;
using DiskAnalyze.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DiskAnalyze.ViewModels
{
    /// <summary>
    /// The Settings view. Every change applies at once and is saved immediately (DEVELOPMENT.md D-031).
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private readonly AppSettingsService _settings;
        private string? _saveError;

        public SettingsViewModel(AppSettingsService settings, ObservableCollection<ScanTarget> scanTargets, Action close)
        {
            _settings = settings;
            ScanTargets = scanTargets;

            BackCommand = new RelayCommand(close);
            DecreasePieSlicesCommand = new RelayCommand(() => PieSlices--, () => PieSlices > AppSettings.MinPieSlices);
            IncreasePieSlicesCommand = new RelayCommand(() => PieSlices++, () => PieSlices < AppSettings.MaxPieSlices);
        }

        public ICommand BackCommand { get; }
        public ICommand DecreasePieSlicesCommand { get; }
        public ICommand IncreasePieSlicesCommand { get; }

        public ThemePreference Theme
        {
            get => _settings.Current.Theme;
            set => Update(s => s with { Theme = value });
        }

        public bool ScanOnStartup
        {
            get => _settings.Current.ScanOnStartup;
            set => Update(s => s with { ScanOnStartup = value });
        }

        /// <summary>The toolbar's targets: all fixed drives, each drive, and folders scanned this session.</summary>
        public ObservableCollection<ScanTarget> ScanTargets { get; }

        /// <summary>
        /// The first target (all fixed drives) is stored as "no path"; any other target by its single path.
        /// </summary>
        public ScanTarget? StartupTarget
        {
            get
            {
                var path = _settings.Current.StartupScanPath;
                return path == null
                    ? ScanTargets.FirstOrDefault()
                    : ScanTargets.Skip(1).FirstOrDefault(t => IsTargetFor(t, path)) ?? ScanTargets.FirstOrDefault();
            }
            set
            {
                if (value == null)
                    return;

                var path = ReferenceEquals(value, ScanTargets.FirstOrDefault()) ? null : value.Paths.FirstOrDefault();
                Update(s => s with { StartupScanPath = path });
            }
        }

        public int PieSlices
        {
            get => _settings.Current.PieSlices;
            set => Update(s => s with { PieSlices = value });
        }

        public int MinPieSlices => AppSettings.MinPieSlices;
        public int MaxPieSlices => AppSettings.MaxPieSlices;

        // Photo analysis: shown disabled until the photo browser ships (Feature B, DEVELOPMENT.md §6.3).
        public PhotoContentAnalysisMode PhotoContentAnalysis => _settings.Current.PhotoAnalysis.PhotoContentAnalysis;
        public ClipOnnxMode ClipOnnx => _settings.Current.PhotoAnalysis.ClipOnnx;
        public ExecutionProviderPreference ExecutionProvider => _settings.Current.PhotoAnalysis.ExecutionProvider;
        public IReadOnlyList<PhotoContentAnalysisMode> PhotoContentAnalysisOptions { get; } = Enum.GetValues<PhotoContentAnalysisMode>();
        public IReadOnlyList<ClipOnnxMode> ClipOnnxOptions { get; } = Enum.GetValues<ClipOnnxMode>();
        public IReadOnlyList<ExecutionProviderPreference> ExecutionProviderOptions { get; } = Enum.GetValues<ExecutionProviderPreference>();

        public string SettingsFilePath => _settings.FilePath;

        /// <summary>Set when the last change couldn't be written to disk.</summary>
        public string? SaveError
        {
            get => _saveError;
            private set => SetProperty(ref _saveError, value);
        }

        public static bool IsTargetFor(ScanTarget target, string path) =>
            target.Paths.Count == 1 && string.Equals(target.Paths[0], path, StringComparison.OrdinalIgnoreCase);

        private void Update(Func<AppSettings, AppSettings> change)
        {
            SaveError = _settings.Update(change);

            // One change can touch several bound values (e.g. a clamped slice count), so refresh them all.
            RaisePropertyChanged(string.Empty);
        }
    }
}
