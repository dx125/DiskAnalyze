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
        public MainWindow()
        {
            InitializeComponent();

            var analyzeService = new AnalyzeDiskService();
            DataContext = new MainViewModel(
                new FileSystemService(analyzeService),
                new DuplicateFinderService(analyzeService));
        }
    }
}
