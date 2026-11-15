using DiskAnalyze.Application.AnalyzeService;
using DiskAnalyze.Application.Duplicates;
using DiskAnalyze.Services;
using DiskAnalyze.ViewModels;
using System.Windows;

namespace DiskAnalyze
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            var analyzeService = new AnalyzeDiskService();
            _viewModel = new MainViewModel(
                new FileSystemService(analyzeService),
                new DuplicateFinderService(analyzeService));
            DataContext = _viewModel;

            Loaded += OnFirstLoaded;
        }

        // Start scanning once the window is on screen, not from the view model's constructor.
        private void OnFirstLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnFirstLoaded;
            _viewModel.StartInitialScan();
        }
    }
}
