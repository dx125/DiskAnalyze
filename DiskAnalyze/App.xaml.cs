using System.Windows;

namespace DiskAnalyze
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set up global exception handling
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                MessageBox.Show($"An unhandled exception occurred: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            };

            // Set up UI thread exception handling
            Current.DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"An unhandled exception occurred: {args.Exception.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}
