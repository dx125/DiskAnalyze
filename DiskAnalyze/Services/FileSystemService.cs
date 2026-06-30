using DiskAnalyze.Application.AnalyzeService;
using DiskAnalyze.Application.DiskEntities;
using DiskAnalyze.ViewModels;
using System.IO;

namespace DiskAnalyze.Services
{
    public interface IFileSystemService
    {
        Task<DirectoryEntityViewModel> LoadFileSystemDataAsync();
    }

    public class FileSystemService : IFileSystemService
    {
        private readonly IDiskAnalyzeService _analyzeService;

        public FileSystemService(IDiskAnalyzeService analyzeService)
        {
            _analyzeService = analyzeService;
        }

        public async Task<DirectoryEntityViewModel> LoadFileSystemDataAsync()
        {
            var allDrives = await _analyzeService.AnalyzeAllDrivesAsync();

            // The tree can be large, so build the view models off the UI thread
            return await Task.Run(() => BuildRootViewModel(allDrives));
        }

        private static DirectoryEntityViewModel BuildRootViewModel(IReadOnlyList<BaseFileSystemEntity> drives)
        {
            // Create a root directory that contains all drives
            var rootViewModel = new DirectoryEntityViewModel
            {
                Name = "Root",
                FullPath = "Root",
                LastModified = DateTime.Now
            };

            foreach (var drive in drives)
            {
                var viewModel = ConvertToViewModel(drive, rootViewModel);
                ApplyDriveUsage(viewModel);
                rootViewModel.Children.Add(viewModel);
                rootViewModel.Size += viewModel.Size;
            }

            return rootViewModel;
        }

        // Drive roots report real capacity/free space, which is the true "% used" — distinct from the
        // scanned size (which omits inaccessible files). Failures (drive not ready) are left as non-drives.
        private static void ApplyDriveUsage(FileSystemEntityViewModel viewModel)
        {
            try
            {
                var driveInfo = new DriveInfo(viewModel.FullPath);
                if (driveInfo.IsReady && driveInfo.TotalSize > 0)
                {
                    long usedBytes = driveInfo.TotalSize - driveInfo.TotalFreeSpace;
                    viewModel.IsDrive = true;
                    viewModel.DiskUsagePercent = (double)usedBytes / driveInfo.TotalSize * 100;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // Not a drive root or not accessible — leave as a plain directory.
            }
        }

        private static FileSystemEntityViewModel ConvertToViewModel(BaseFileSystemEntity entity, DirectoryEntityViewModel? parent)
        {
            switch (entity)
            {
                case DirectoryEntityInfo directoryInfo:
                    var directoryViewModel = new DirectoryEntityViewModel
                    {
                        Name = directoryInfo.Name,
                        FullPath = directoryInfo.FullPath,
                        LastModified = directoryInfo.LastModified,
                        Size = directoryInfo.Size,
                        Parent = parent
                    };

                    foreach (var childEntity in directoryInfo.Children)
                    {
                        directoryViewModel.Children.Add(ConvertToViewModel(childEntity, directoryViewModel));
                    }

                    return directoryViewModel;

                case FileEntityInfo fileInfo:
                    return new FileEntityViewModel
                    {
                        Name = fileInfo.Name,
                        FullPath = fileInfo.FullPath,
                        LastModified = fileInfo.LastModified,
                        Size = fileInfo.Size,
                        Extension = fileInfo.Extension,
                        Parent = parent
                    };

                default:
                    throw new ArgumentException($"Unknown entity type: {entity.GetType().Name}");
            }
        }
    }
}
