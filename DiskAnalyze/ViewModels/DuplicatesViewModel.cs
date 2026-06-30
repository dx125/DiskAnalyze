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
        private string _leftFolderPath = string.Empty;
        private string _rightFolderPath = string.Empty;
        private bool _isScanning;
        private string _statusMessage = "Select two folders to compare.";

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
            set => SetProperty(ref _isScanning, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ObservableCollection<DuplicateMatchViewModel> Matches { get; } = new();

        public ICommand BrowseLeftCommand { get; }
        public ICommand BrowseRightCommand { get; }
        public ICommand FindDuplicatesCommand { get; }

        public DuplicatesViewModel(IDuplicateFinderService duplicateFinderService)
        {
            _duplicateFinderService = duplicateFinderService;

            BrowseLeftCommand = new RelayCommand(() => BrowseFolder(p => LeftFolderPath = p));
            BrowseRightCommand = new RelayCommand(() => BrowseFolder(p => RightFolderPath = p));
            FindDuplicatesCommand = new RelayCommand(async () => await FindDuplicatesAsync(), CanFindDuplicates);
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

        private async Task FindDuplicatesAsync()
        {
            if (!Directory.Exists(LeftFolderPath) || !Directory.Exists(RightFolderPath))
            {
                StatusMessage = "Both folders must exist.";
                return;
            }

            try
            {
                IsScanning = true;
                StatusMessage = "Scanning and comparing files...";
                Matches.Clear();

                var groups = await _duplicateFinderService.FindDuplicatesAsync(LeftFolderPath, RightFolderPath);

                foreach (var row in BuildRows(groups))
                {
                    Matches.Add(row);
                }

                StatusMessage = Matches.Count == 0
                    ? "No duplicate files found."
                    : $"{Matches.Count} duplicate file(s) found.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        // One row per matched pair; if a group has more files on one side, the extra rows leave the opposite side blank.
        private static IEnumerable<DuplicateMatchViewModel> BuildRows(IEnumerable<DuplicateFileGroup> groups)
        {
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
                        Extension = group.Extension
                    };
                }
            }
        }
    }
}
