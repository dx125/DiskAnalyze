using DiskAnalyze.Application.Duplicates;
using DiskAnalyze.Core;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace DiskAnalyze.ViewModels
{
    public class DuplicatesViewModel : ViewModelBase
    {
        private readonly IDuplicateFinderService _duplicateFinderService;
        private CancellationTokenSource? _cancellation;
        private string _leftFolderPath = string.Empty;
        private string _rightFolderPath = string.Empty;
        private bool _isScanning;
        private string _statusMessage = "Select two folders to compare.";
        private ObservableCollection<DuplicateMatchViewModel> _matches = new();

        public string LeftFolderPath
        {
            get => _leftFolderPath;
            set => SetProperty(ref _leftFolderPath, value);
        }

        public string RightFolderPath
        {
            get => _rightFolderPath;
            set => SetProperty(ref _rightFolderPath, value);
        }

        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    // WPF re-queries CanExecute only after user input; a search finishing on its own isn't input.
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ObservableCollection<DuplicateMatchViewModel> Matches
        {
            get => _matches;
            private set => SetProperty(ref _matches, value);
        }

        public ICommand BrowseLeftCommand { get; }
        public ICommand BrowseRightCommand { get; }
        public ICommand FindDuplicatesCommand { get; }
        public ICommand CancelCommand { get; }

        public DuplicatesViewModel(IDuplicateFinderService duplicateFinderService)
        {
            _duplicateFinderService = duplicateFinderService;

            BrowseLeftCommand = new RelayCommand(() => BrowseFolder(p => LeftFolderPath = p));
            BrowseRightCommand = new RelayCommand(() => BrowseFolder(p => RightFolderPath = p));
            FindDuplicatesCommand = new RelayCommand(() => _ = FindDuplicatesAsync(), CanFindDuplicates);
            CancelCommand = new RelayCommand(() => _cancellation?.Cancel(), () => IsScanning);
        }

        private bool CanFindDuplicates() =>
            !IsScanning
            && !string.IsNullOrWhiteSpace(LeftFolderPath)
            && !string.IsNullOrWhiteSpace(RightFolderPath)
            && !string.Equals(
                Path.TrimEndingDirectorySeparator(LeftFolderPath),
                Path.TrimEndingDirectorySeparator(RightFolderPath),
                StringComparison.OrdinalIgnoreCase);

        private static void BrowseFolder(Action<string> onSelected)
        {
            var dialog = new OpenFolderDialog { Title = "Select folder" };
            if (dialog.ShowDialog() == true)
            {
                onSelected(dialog.FolderName);
            }
        }

        // Never throws: every failure ends up in the status text.
        private async Task FindDuplicatesAsync()
        {
            if (!Directory.Exists(LeftFolderPath) || !Directory.Exists(RightFolderPath))
            {
                StatusMessage = "Both folders must exist.";
                return;
            }

            var cancellation = new CancellationTokenSource();
            _cancellation = cancellation;
            var progress = new Progress<DuplicateSearchProgress>(ReportProgress);

            try
            {
                IsScanning = true;
                StatusMessage = "Scanning folder A…";
                Matches = new ObservableCollection<DuplicateMatchViewModel>();

                var groups = await _duplicateFinderService.FindDuplicatesAsync(
                    LeftFolderPath, RightFolderPath, progress, cancellation.Token);

                Matches = new ObservableCollection<DuplicateMatchViewModel>(BuildRows(groups));
                StatusMessage = groups.Count == 0 ? "No duplicate files found." : DescribeResults(groups);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Search cancelled.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                _cancellation = null;
                cancellation.Dispose();
                IsScanning = false;
            }
        }

        private void ReportProgress(DuplicateSearchProgress progress)
        {
            if (!IsScanning)
                return; // a report that arrived after the search finished

            StatusMessage = progress.Phase switch
            {
                DuplicateSearchPhase.ScanningLeft => $"Scanning folder A… {progress.Completed:N0} files",
                DuplicateSearchPhase.ScanningRight => $"Scanning folder B… {progress.Completed:N0} files",
                _ => $"Comparing contents… {progress.Completed:N0} / {progress.Total:N0} candidate files"
            };
        }

        private static string DescribeResults(IReadOnlyList<DuplicateFileGroup> groups)
        {
            long files = groups.Sum(g => (long)g.FileCount);
            long reclaimable = groups.Sum(g => g.ReclaimableBytes);

            return $"{groups.Count:N0} duplicate group(s), {files:N0} files · " +
                   $"{FileSizeFormatter.Format(reclaimable)} reclaimable by keeping one copy of each";
        }

        // One row per matched pair; if a group has more files on one side, the extra rows leave the opposite side blank.
        private static IEnumerable<DuplicateMatchViewModel> BuildRows(IEnumerable<DuplicateFileGroup> groups)
        {
            bool alternate = false;

            foreach (var group in groups)
            {
                int rowCount = Math.Max(group.LeftFiles.Count, group.RightFiles.Count);
                for (int i = 0; i < rowCount; i++)
                {
                    var left = i < group.LeftFiles.Count ? group.LeftFiles[i] : null;
                    var right = i < group.RightFiles.Count ? group.RightFiles[i] : null;

                    yield return new DuplicateMatchViewModel
                    {
                        LeftName = left?.Name,
                        LeftFullPath = left?.FullPath,
                        RightName = right?.Name,
                        RightFullPath = right?.FullPath,
                        Size = group.Size,
                        Extension = group.Extension,
                        IsAlternateGroup = alternate
                    };
                }

                alternate = !alternate;
            }
        }
    }
}
