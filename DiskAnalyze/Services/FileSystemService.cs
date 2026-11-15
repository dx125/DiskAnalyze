using DiskAnalyze.Application.AnalyzeService;
using DiskAnalyze.Application.DiskEntities;
using DiskAnalyze.Core;
using DiskAnalyze.ViewModels;
using System.IO;

namespace DiskAnalyze.Services
{
    public interface IFileSystemService
    {
        /// <summary>"All fixed drives" first, then one target per fixed drive.</summary>
        IReadOnlyList<ScanTarget> GetScanTargets();

        /// <summary>
        /// Scans <paramref name="paths"/> and returns the root to display: the scanned folder or drive itself
        /// when there is one, or a synthetic "This PC" node holding several drives.
        /// </summary>
        Task<DirectoryEntityViewModel> LoadFileSystemDataAsync(IReadOnlyList<string> paths,
            IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default);
    }

    public class FileSystemService : IFileSystemService
    {
        private const string AllDrivesName = "This PC";

        private readonly IDiskAnalyzeService _analyzeService;

        public FileSystemService(IDiskAnalyzeService analyzeService)
        {
            _analyzeService = analyzeService;
        }

        public IReadOnlyList<ScanTarget> GetScanTargets()
        {
            var drives = _analyzeService.GetFixedDrivePaths();

            var targets = new List<ScanTarget> { new("All fixed drives", drives) };
            targets.AddRange(drives.Select(drive => new ScanTarget(DescribeDrive(drive), new[] { drive })));

            return targets;
        }

        public async Task<DirectoryEntityViewModel> LoadFileSystemDataAsync(IReadOnlyList<string> paths,
            IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var roots = await _analyzeService.AnalyzePathsAsync(paths, progress, cancellationToken);

            // The tree can be large, so build the view models off the UI thread
            return await Task.Run(() => BuildRootViewModel(roots, cancellationToken), cancellationToken);
        }

        private static string DescribeDrive(string rootPath)
        {
            try
            {
                var drive = new DriveInfo(rootPath);
                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel;
                return $"{rootPath}  {label} ({FileSizeFormatter.Format(drive.TotalSize)})";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return rootPath;
            }
        }

        private static DirectoryEntityViewModel BuildRootViewModel(IReadOnlyList<BaseFileSystemEntity> roots, CancellationToken ct)
        {
            // A single scanned folder or drive is the root itself, so "Up" stops there.
            if (roots is [DirectoryEntityInfo singleRoot])
            {
                var singleViewModel = (DirectoryEntityViewModel)ConvertTree(singleRoot, parent: null, ct);
                ApplyDriveUsage(singleViewModel);
                return singleViewModel;
            }

            var rootViewModel = new DirectoryEntityViewModel
            {
                Name = AllDrivesName,
                FullPath = AllDrivesName,
                LastModified = DateTime.Now
            };

            foreach (var root in roots)
            {
                var viewModel = ConvertTree(root, rootViewModel, ct);
                ApplyDriveUsage(viewModel);
                rootViewModel.Children.Add(viewModel);
                rootViewModel.Size += viewModel.Size;
            }

            return rootViewModel;
        }

        // Drive roots report real capacity/free space, which is the true "% used" — distinct from the
        // scanned size, which omits inaccessible folders. A sizable gap is surfaced as "not scanned".
        private static void ApplyDriveUsage(FileSystemEntityViewModel viewModel)
        {
            if (!IsDriveRoot(viewModel.FullPath))
                return;

            try
            {
                var driveInfo = new DriveInfo(viewModel.FullPath);
                if (driveInfo.IsReady && driveInfo.TotalSize > 0)
                {
                    long usedBytes = driveInfo.TotalSize - driveInfo.TotalFreeSpace;
                    viewModel.IsDrive = true;
                    viewModel.DiskUsagePercent = (double)usedBytes / driveInfo.TotalSize * 100;

                    // NTFS metadata (MFT, logs) scales with capacity: an empty 4 TB drive already reports
                    // ~25 GB used. Only a gap above 1% of capacity points at folders we couldn't read.
                    long unscannedBytes = usedBytes - viewModel.Size;
                    if (unscannedBytes > driveInfo.TotalSize / 100)
                    {
                        viewModel.UnscannedBytes = unscannedBytes;
                    }
                }
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // Not accessible — leave as a plain directory.
            }
        }

        private static bool IsDriveRoot(string path) =>
            Path.IsPathFullyQualified(path)
            && string.Equals(Path.GetPathRoot(path), path, StringComparison.OrdinalIgnoreCase);

        // Iterative, so arbitrarily deep trees can't overflow the stack.
        private static FileSystemEntityViewModel ConvertTree(BaseFileSystemEntity rootEntity, DirectoryEntityViewModel? parent,
            CancellationToken ct)
        {
            var rootViewModel = CreateViewModel(rootEntity, parent);
            var pending = new Stack<(DirectoryEntityInfo Entity, DirectoryEntityViewModel ViewModel)>();

            if (rootEntity is DirectoryEntityInfo rootDirectory)
            {
                pending.Push((rootDirectory, (DirectoryEntityViewModel)rootViewModel));
            }

            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();

                var (entity, viewModel) = pending.Pop();
                viewModel.Children.Capacity = entity.Children.Count;

                foreach (var childEntity in entity.Children)
                {
                    var childViewModel = CreateViewModel(childEntity, viewModel);
                    viewModel.Children.Add(childViewModel);

                    if (childEntity is DirectoryEntityInfo childDirectory)
                    {
                        pending.Push((childDirectory, (DirectoryEntityViewModel)childViewModel));
                    }
                }
            }

            return rootViewModel;
        }

        private static FileSystemEntityViewModel CreateViewModel(BaseFileSystemEntity entity, DirectoryEntityViewModel? parent) =>
            entity switch
            {
                DirectoryEntityInfo directory => new DirectoryEntityViewModel
                {
                    Name = directory.Name,
                    FullPath = directory.FullPath,
                    LastModified = directory.LastModified,
                    Size = directory.Size,
                    Parent = parent
                },
                FileEntityInfo file => new FileEntityViewModel
                {
                    Name = file.Name,
                    FullPath = file.FullPath,
                    LastModified = file.LastModified,
                    Size = file.Size,
                    Extension = file.Extension,
                    IsCloudOnly = file.IsCloudOnly,
                    Parent = parent
                },
                _ => throw new ArgumentException($"Unknown entity type: {entity.GetType().Name}")
            };
    }
}
