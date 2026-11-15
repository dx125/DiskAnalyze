using DiskAnalyze.Core;
using DiskAnalyze.Services;
using System.Collections.ObjectModel;
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
        private IReadOnlyList<FileSystemEntityViewModel> _segmentContents;

        public DirectoryEntityViewModel CurrentDirectory
        {
            get => _currentDirectory;
            set
            {
                if (SetProperty(ref _currentDirectory, value))
                {
                    RefreshDisplayedEntities();
                    CanNavigateUp = _currentDirectory.Parent != null;
                    CurrentPath = _currentDirectory.FullPath;
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
                    // Replaced wholesale: adding thousands of items one by one would raise a change event per item.
                    SegmentContents = value == null
                        ? Array.Empty<FileSystemEntityViewModel>()
                        : GetImmediateChildren(value).OrderByDescending(c => c.Size).ToList();

                    RaisePropertyChanged(nameof(ContentsTitle));
                    RaisePropertyChanged(nameof(HasContents));
                }
            }
        }

        /// <summary>Immediate files and folders of <see cref="ContentsTarget"/>, sorted largest first.</summary>
        public IReadOnlyList<FileSystemEntityViewModel> SegmentContents
        {
            get => _segmentContents;
            private set => SetProperty(ref _segmentContents, value);
        }

        public bool HasContents => _contentsTarget != null;

        public string ContentsTitle => _contentsTarget != null ? $"Contents of \"{_contentsTarget.DisplayName}\"" : "Contents";

        public ICommand NavigateToDirectoryCommand { get; }
        public ICommand NavigateUpCommand { get; }
        public ICommand ShowContentsCommand { get; }
        public ICommand HideContentsCommand { get; }
        public ICommand ShowInExplorerCommand { get; }

        protected DiagramViewModelBase()
        {
            _currentDirectory = new DirectoryEntityViewModel();
            _displayedEntities = new ObservableCollection<FileSystemEntityViewModel>();
            _currentPath = string.Empty;
            _segmentContents = Array.Empty<FileSystemEntityViewModel>();

            NavigateToDirectoryCommand = new RelayCommand(NavigateToDirectory, p => p is DirectoryEntityViewModel);
            NavigateUpCommand = new RelayCommand(NavigateUp, () => CanNavigateUp);
            ShowContentsCommand = new RelayCommand(ShowContents, p => p is FileSystemEntityViewModel);
            HideContentsCommand = new RelayCommand(() => ContentsTarget = null);
            ShowInExplorerCommand = new RelayCommand(ShowInExplorer, CanShowInExplorer);
        }

        public void SetRootDirectory(DirectoryEntityViewModel root)
        {
            CurrentDirectory = root;
        }

        protected abstract IReadOnlyList<FileSystemEntityViewModel> CreateDisplayedEntities();

        /// <summary>Rebuilds the segments of the current directory, e.g. after a display setting changed.</summary>
        protected void RefreshDisplayedEntities()
        {
            var entities = CreateDisplayedEntities();
            AssignColors(entities);
            DisplayedEntities = new ObservableCollection<FileSystemEntityViewModel>(entities);

            // The previously inspected segment may no longer exist (a new directory, a different "Other").
            ContentsTarget = null;
        }

        protected static void CalculatePercentages(IEnumerable<FileSystemEntityViewModel> items, long totalSize)
        {
            foreach (var item in items)
            {
                item.PercentageOfTotal = totalSize > 0 ? (double)item.Size / totalSize * 100 : 0;
            }
        }

        // Colors follow the size rank. Every view sorts siblings the same way (largest first), so an
        // entity keeps its color across the pie and the bars, and the top pie slices never share one.
        private static void AssignColors(IReadOnlyList<FileSystemEntityViewModel> entities)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                entities[i].ColorIndex = i;
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

        private static void ShowInExplorer(object? parameter)
        {
            if (parameter is FileSystemEntityViewModel { IsOtherCategory: false } entity)
            {
                ShellActions.ShowInExplorer(entity.FullPath);
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
