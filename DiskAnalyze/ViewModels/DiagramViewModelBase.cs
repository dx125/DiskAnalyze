using DiskAnalyze.Core;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace DiskAnalyze.ViewModels
{
    /// <summary>
    /// Shared navigation and display logic for the diagram views.
    /// Derived classes only decide which entities of the current directory are shown.
    /// </summary>
    public abstract class DiagramViewModelBase : ViewModelBase
    {
        private DirectoryEntityViewModel _currentDirectory;
        private ObservableCollection<FileSystemEntityViewModel> _displayedEntities;
        private bool _canNavigateUp;
        private string _currentPath;
        private FileSystemEntityViewModel? _contentsTarget;

        public DirectoryEntityViewModel CurrentDirectory
        {
            get => _currentDirectory;
            set
            {
                if (SetProperty(ref _currentDirectory, value))
                {
                    DisplayedEntities = new ObservableCollection<FileSystemEntityViewModel>(CreateDisplayedEntities());
                    CanNavigateUp = _currentDirectory.Parent != null;
                    CurrentPath = _currentDirectory.FullPath;

                    // The previously inspected segment no longer belongs to this directory.
                    ContentsTarget = null;
                }
            }
        }

        public ObservableCollection<FileSystemEntityViewModel> DisplayedEntities
        {
            get => _displayedEntities;
            set => SetProperty(ref _displayedEntities, value);
        }

        public bool CanNavigateUp
        {
            get => _canNavigateUp;
            set => SetProperty(ref _canNavigateUp, value);
        }

        public string CurrentPath
        {
            get => _currentPath;
            set => SetProperty(ref _currentPath, value);
        }

        /// <summary>
        /// The segment whose immediate contents are pinned in the side panel, or null when none is shown.
        /// </summary>
        public FileSystemEntityViewModel? ContentsTarget
        {
            get => _contentsTarget;
            private set
            {
                if (SetProperty(ref _contentsTarget, value))
                {
                    SegmentContents.Clear();

                    if (value != null)
                    {
                        foreach (var child in GetImmediateChildren(value).OrderByDescending(c => c.Size))
                        {
                            SegmentContents.Add(child);
                        }
                    }

                    RaisePropertyChanged(nameof(ContentsTitle));
                    RaisePropertyChanged(nameof(HasContents));
                }
            }
        }

        /// <summary>Immediate files and folders of <see cref="ContentsTarget"/>, sorted largest first.</summary>
        public ObservableCollection<FileSystemEntityViewModel> SegmentContents { get; }

        public bool HasContents => _contentsTarget != null;

        public string ContentsTitle => _contentsTarget != null ? $"Contents of \"{_contentsTarget.Name}\"" : "Contents";

        public ICommand NavigateToDirectoryCommand { get; }
        public ICommand NavigateUpCommand { get; }
        public ICommand ShowContentsCommand { get; }
        public ICommand ShowInExplorerCommand { get; }

        protected DiagramViewModelBase()
        {
            _currentDirectory = new DirectoryEntityViewModel();
            _displayedEntities = new ObservableCollection<FileSystemEntityViewModel>();
            _currentPath = string.Empty;
            SegmentContents = new ObservableCollection<FileSystemEntityViewModel>();

            NavigateToDirectoryCommand = new RelayCommand(NavigateToDirectory, p => p is DirectoryEntityViewModel);
            NavigateUpCommand = new RelayCommand(NavigateUp, () => CanNavigateUp);
            ShowContentsCommand = new RelayCommand(ShowContents, p => p is FileSystemEntityViewModel);
            ShowInExplorerCommand = new RelayCommand(ShowInExplorer, CanShowInExplorer);
        }

        public void SetRootDirectory(DirectoryEntityViewModel root)
        {
            CurrentDirectory = root;
        }

        protected abstract IReadOnlyList<FileSystemEntityViewModel> CreateDisplayedEntities();

        protected static void CalculatePercentages(IEnumerable<FileSystemEntityViewModel> items, long totalSize)
        {
            foreach (var item in items)
            {
                item.PercentageOfTotal = totalSize > 0 ? (double)item.Size / totalSize * 100 : 0;
            }
        }

        private void NavigateToDirectory(object? parameter)
        {
            if (parameter is DirectoryEntityViewModel directory)
            {
                CurrentDirectory = directory;
            }
        }

        private void NavigateUp()
        {
            if (CurrentDirectory.Parent != null)
            {
                CurrentDirectory = CurrentDirectory.Parent;
            }
        }

        private void ShowContents(object? parameter)
        {
            ContentsTarget = parameter as FileSystemEntityViewModel;
        }

        // The synthetic "Other" bucket has no real filesystem path, so it can't be revealed.
        private static bool CanShowInExplorer(object? parameter) =>
            parameter is FileSystemEntityViewModel { IsOtherCategory: false } entity
            && !string.IsNullOrEmpty(entity.FullPath);

        private void ShowInExplorer(object? parameter)
        {
            if (parameter is not FileSystemEntityViewModel entity || string.IsNullOrEmpty(entity.FullPath))
                return;

            try
            {
                // Open the folder directly; for files, reveal them selected inside their parent folder.
                var arguments = entity.IsDirectory && Directory.Exists(entity.FullPath)
                    ? $"\"{entity.FullPath}\""
                    : $"/select,\"{entity.FullPath}\"";

                Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
            }
            catch
            {
                // Revealing in Explorer is best-effort; ignore failures (e.g. path no longer exists).
            }
        }

        private static IEnumerable<FileSystemEntityViewModel> GetImmediateChildren(FileSystemEntityViewModel entity) =>
            entity switch
            {
                DirectoryEntityViewModel directory => directory.Children,
                OtherCategoryViewModel other => other.ContainedEntities,
                _ => Enumerable.Empty<FileSystemEntityViewModel>()
            };
    }
}
