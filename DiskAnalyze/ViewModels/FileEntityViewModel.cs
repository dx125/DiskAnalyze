namespace DiskAnalyze.ViewModels
{
    public class FileEntityViewModel : FileSystemEntityViewModel
    {
        private string _extension;

        public string Extension
        {
            get => _extension;
            set => SetProperty(ref _extension, value);
        }

        public FileEntityViewModel()
        {
            _extension = string.Empty;
        }
    }
}
