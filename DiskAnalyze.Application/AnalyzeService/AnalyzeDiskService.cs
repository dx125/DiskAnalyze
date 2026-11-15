using DiskAnalyze.Application.DiskEntities;

namespace DiskAnalyze.Application.AnalyzeService
{
    public class AnalyzeDiskService : IDiskAnalyzeService
    {
        private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(200);

        public IReadOnlyList<string> GetFixedDrivePaths() =>
            DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady) // type first: IsReady can be slow
                .Select(d => d.RootDirectory.FullName)
                .ToList();

        public Task<List<BaseFileSystemEntity>> AnalyzeAllDrivesAsync(
            IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            return AnalyzePathsAsync(GetFixedDrivePaths(), progress, cancellationToken);
        }

        public async Task<List<BaseFileSystemEntity>> AnalyzePathsAsync(IReadOnlyCollection<string> paths,
            IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var rootItems = new List<BaseFileSystemEntity>();
            var rootDirectories = new List<DirectoryEntityInfo>();

            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
                    if (string.IsNullOrEmpty(name)) // It's a root drive
                    {
                        name = path;
                    }

                    var directory = new DirectoryEntityInfo(name, path, Directory.GetLastWriteTime(path));

                    rootItems.Add(directory);
                    rootDirectories.Add(directory);
                }
                else if (File.Exists(path))
                {
                    rootItems.Add(FileEntityInfo.CreateFromFile(new FileInfo(path)));
                }
            }

            var scanner = new DirectoryTreeScanner();
            var scanTask = scanner.ScanAsync(rootDirectories, cancellationToken);

            if (progress != null)
            {
                while (await Task.WhenAny(scanTask, Task.Delay(ProgressInterval, CancellationToken.None)) != scanTask)
                {
                    progress.Report(scanner.GetProgress());
                }
            }

            await scanTask; // rethrows cancellation or failures

            foreach (var rootDirectory in rootDirectories)
            {
                rootDirectory.CalculateSize();
            }

            progress?.Report(scanner.GetProgress());

            return rootItems;
        }
    }
}
