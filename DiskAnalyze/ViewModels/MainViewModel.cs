using DiskAnalyze.Application.AnalyzeService;
using DiskAnalyze.Application.Duplicates;
using DiskAnalyze.Core;
using DiskAnalyze.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace DiskAnalyze.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IFileSystemService _fileSystemService;
        private readonly AppSettingsService _settings;
        private readonly CircularDiagramViewModel _circularDiagramViewModel;
        private readonly ChartDiagramViewModel _chartDiagramViewModel;
        private readonly DuplicatesViewModel _duplicatesViewModel;
        private readonly SettingsViewModel _settingsViewModel;
        private ViewModelBase _currentViewModel;
        private ViewModelBase _viewBeforeSettings;
        private ScanTarget? _selectedScanTarget;
        private CancellationTokenSource? _scanCancellation;
        private bool _hasScanResults;
        private bool _isLoading;
        private string _statusMessage;
        private string _scanProgressText = string.Empty;
        private string _scanCurrentPath = string.Empty;

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                if (SetProperty(ref _currentViewModel, value))
                {
                    RaisePropertyChanged(nameof(IsCircularSelected));
                    RaisePropertyChanged(nameof(IsChartSelected));
                    RaisePropertyChanged(nameof(IsDuplicatesSelected));
                    RaisePropertyChanged(nameof(IsSettingsSelected));
                    RaiseContentVisibilityChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// Bound two-way to the toolbar's Settings toggle. Closing returns to the view that was open before.
        /// </summary>
        public bool IsSettingsSelected
        {
            get => CurrentViewModel == _settingsViewModel;
            set
            {
                if (value && !IsSettingsSelected)
                {
                    _viewBeforeSettings = CurrentViewModel;
                    CurrentViewModel = _settingsViewModel;
                }
                else if (!value && IsSettingsSelected)
                {
                    CurrentViewModel = _viewBeforeSettings;
                }
            }
        }

        /// <summary>Shown in place of an empty diagram when nothing has been scanned yet.</summary>
        public bool ShowEmptyHint => !_hasScanResults && !IsLoading && (IsCircularSelected || IsChartSelected);

        /// <summary>The current view is hidden behind the loading overlay and the empty-state hint.</summary>
        public bool ShowContent => !IsLoading && !ShowEmptyHint;

        private void RaiseContentVisibilityChanged()
        {
            RaisePropertyChanged(nameof(ShowEmptyHint));
            RaisePropertyChanged(nameof(ShowContent));
        }

        // Bound two-way to the toolbar's view toggles, so the active view is always highlighted.
        public bool IsCircularSelected
        {
            get => CurrentViewModel == _circularDiagramViewModel;
            set { if (value) CurrentViewModel = _circularDiagramViewModel; }
        }

        public bool IsChartSelected
        {
            get => CurrentViewModel == _chartDiagramViewModel;
            set { if (value) CurrentViewModel = _chartDiagramViewModel; }
        }

        public bool IsDuplicatesSelected
        {
            get => CurrentViewModel == _duplicatesViewModel;
            set { if (value) CurrentViewModel = _duplicatesViewModel; }
        }

        public ObservableCollection<ScanTarget> ScanTargets { get; }

        public ScanTarget? SelectedScanTarget
        {
            get => _selectedScanTarget;
            set => SetProperty(ref _selectedScanTarget, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    RaiseContentVisibilityChanged();

                    // WPF re-queries CanExecute only after user input; a scan finishing on its own isn't input.
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>Live counters shown in the loading overlay: files, folders, bytes, elapsed time.</summary>
        public string ScanProgressText
        {
            get => _scanProgressText;
            set => SetProperty(ref _scanProgressText, value);
        }

        public string ScanCurrentPath
        {
            get => _scanCurrentPath;
            set => SetProperty(ref _scanCurrentPath, value);
        }

        public ICommand ScanCommand { get; }
        public ICommand ScanFolderCommand { get; }
        public ICommand CancelScanCommand { get; }
        public ICommand CloseSettingsCommand { get; }

        public MainViewModel(IFileSystemService fileSystemService, IDuplicateFinderService duplicateFinderService,
            AppSettingsService settings)
        {
            _fileSystemService = fileSystemService;
            _settings = settings;
            _circularDiagramViewModel = new CircularDiagramViewModel { SliceCount = settings.Current.PieSlices };
            _chartDiagramViewModel = new ChartDiagramViewModel();
            _duplicatesViewModel = new DuplicatesViewModel(duplicateFinderService);
            _currentViewModel = _circularDiagramViewModel;
            _viewBeforeSettings = _circularDiagramViewModel;
            _statusMessage = "Ready to scan";

            ScanTargets = new ObservableCollection<ScanTarget>(fileSystemService.GetScanTargets());
            _selectedScanTarget = FindOrAddTarget(settings.Current.StartupScanPath);
            _settingsViewModel = new SettingsViewModel(settings, ScanTargets, () => IsSettingsSelected = false);

            ScanCommand = new RelayCommand(() => _ = ScanAsync(SelectedScanTarget), () => !IsLoading && SelectedScanTarget != null);
            ScanFolderCommand = new RelayCommand(ScanFolder, () => !IsLoading);
            CancelScanCommand = new RelayCommand(() => _scanCancellation?.Cancel(), () => IsLoading);
            CloseSettingsCommand = new RelayCommand(() => IsSettingsSelected = false, () => IsSettingsSelected);

            settings.Changed += (_, current) => _circularDiagramViewModel.SliceCount = current.PieSlices;
        }

        /// <summary>
        /// Called once the main window is shown: scans the startup target from Settings, unless scanning on
        /// startup is turned off.
        /// </summary>
        public void StartInitialScan()
        {
            if (_settings.Current.ScanOnStartup)
            {
                _ = ScanAsync(SelectedScanTarget);
            }
            else
            {
                StatusMessage = "Ready. Choose what to scan and press Scan.";
            }
        }

        private void ScanFolder()
        {
            var dialog = new OpenFolderDialog { Title = "Select a folder to scan" };
            if (dialog.ShowDialog() != true)
                return;

            var target = FindOrAddTarget(dialog.FolderName);
            SelectedScanTarget = target;
            _ = ScanAsync(target);
        }

        // null means all fixed drives (the first target). A folder that no longer exists falls back to it too.
        private ScanTarget? FindOrAddTarget(string? path)
        {
            if (path == null)
                return ScanTargets.FirstOrDefault();

            var existing = ScanTargets.Skip(1).FirstOrDefault(t => SettingsViewModel.IsTargetFor(t, path));
            if (existing != null)
                return existing;

            if (!Directory.Exists(path))
                return ScanTargets.FirstOrDefault();

            var target = new ScanTarget(path, new[] { path });
            ScanTargets.Add(target);
            return target;
        }

        // Never throws: every failure ends up in the status bar.
        private async Task ScanAsync(ScanTarget? target)
        {
            if (target == null || IsLoading)
                return;

            var cancellation = new CancellationTokenSource();
            _scanCancellation = cancellation;

            var stopwatch = Stopwatch.StartNew();
            var progress = new Progress<ScanProgress>(p => ReportScanProgress(p, stopwatch.Elapsed));

            try
            {
                IsLoading = true;
                ScanProgressText = "Starting…";
                ScanCurrentPath = string.Empty;
                StatusMessage = $"Scanning {target.DisplayName}…";

                var root = await _fileSystemService.LoadFileSystemDataAsync(target.Paths, progress, cancellation.Token);

                // One tree, shared by both diagrams.
                _circularDiagramViewModel.SetRootDirectory(root);
                _chartDiagramViewModel.SetRootDirectory(root);
                _hasScanResults = true;
                RaiseContentVisibilityChanged();

                StatusMessage = $"Scanned {target.DisplayName}: {FileSizeFormatter.Format(root.Size)} in {stopwatch.Elapsed:m\\:ss}";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = _hasScanResults ? "Scan cancelled; showing the previous results." : "Scan cancelled.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error scanning {target.DisplayName}: {ex.Message}";
            }
            finally
            {
                _scanCancellation = null;
                cancellation.Dispose();
                IsLoading = false;
            }
        }

        private void ReportScanProgress(ScanProgress progress, TimeSpan elapsed)
        {
            ScanProgressText = $"{progress.Files:N0} files · {progress.Directories:N0} folders · " +
                               $"{FileSizeFormatter.Format(progress.Bytes)} · {elapsed:m\\:ss}";
            ScanCurrentPath = progress.CurrentPath ?? string.Empty;
        }
    }
}
