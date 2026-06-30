using DiskAnalyze.Application.Duplicates;
using DiskAnalyze.Core;
using DiskAnalyze.Services;
using System.Windows.Input;

namespace DiskAnalyze.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IFileSystemService _fileSystemService;
        private ViewModelBase _currentViewModel;
        private readonly CircularDiagramViewModel _circularDiagramViewModel;
        private readonly ChartDiagramViewModel _chartDiagramViewModel;
        private readonly DuplicatesViewModel _duplicatesViewModel;
        private bool _isLoading;
        private string _statusMessage;

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand ShowCircularDiagramCommand { get; }
        public ICommand ShowChartDiagramCommand { get; }
        public ICommand ShowDuplicatesCommand { get; }

        public MainViewModel(IFileSystemService fileSystemService, IDuplicateFinderService duplicateFinderService)
        {
            _fileSystemService = fileSystemService;
            _circularDiagramViewModel = new CircularDiagramViewModel();
            _chartDiagramViewModel = new ChartDiagramViewModel();
            _duplicatesViewModel = new DuplicatesViewModel(duplicateFinderService);
            _currentViewModel = _circularDiagramViewModel;
            _statusMessage = "Ready to load file system data";

            ShowCircularDiagramCommand = new RelayCommand(() => CurrentViewModel = _circularDiagramViewModel);
            ShowChartDiagramCommand = new RelayCommand(() => CurrentViewModel = _chartDiagramViewModel);
            ShowDuplicatesCommand = new RelayCommand(() => CurrentViewModel = _duplicatesViewModel);

            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading file system data...";

                // Load once and share the same tree between both diagrams
                var root = await _fileSystemService.LoadFileSystemDataAsync();

                _circularDiagramViewModel.SetRootDirectory(root);
                _chartDiagramViewModel.SetRootDirectory(root);

                StatusMessage = "Data loaded successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
