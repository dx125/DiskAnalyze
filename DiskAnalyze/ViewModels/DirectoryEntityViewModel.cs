namespace DiskAnalyze.ViewModels
{
    public class DirectoryEntityViewModel : FileSystemEntityViewModel
    {
        private List<FileSystemEntityViewModel> _children;

        public List<FileSystemEntityViewModel> Children
        {
            get => _children;
            set => SetProperty(ref _children, value);
        }

        public DirectoryEntityViewModel()
        {
            IsDirectory = true;
            _children = new List<FileSystemEntityViewModel>();
        }
    }
}
