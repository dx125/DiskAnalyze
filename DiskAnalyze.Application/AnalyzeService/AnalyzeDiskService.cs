using DiskAnalyze.Application.DiskEntities;

namespace DiskAnalyze.Application.AnalyzeService
{
    public class AnalyzeDiskService : IDiskAnalyzeService
    {
        public Task<List<BaseFileSystemEntity>> AnalyzeAllDrivesAsync(CancellationToken cancellationToken = default)
        {
            var drivePaths = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => d.RootDirectory.FullName)
                .ToList();

            return AnalyzePathsAsync(drivePaths, cancellationToken);
        }

        public async Task<BaseFileSystemEntity?> AnalyzeDriveAsync(string drivePath, CancellationToken cancellationToken = default)
        {
            var result = await AnalyzePathsAsync(new[] { drivePath }, cancellationToken);

            return result.FirstOrDefault();
        }

        public async Task<List<BaseFileSystemEntity>> AnalyzePathsAsync(IReadOnlyCollection<string> paths, CancellationToken cancellationToken = default)
        {
            var rootItems = new List<BaseFileSystemEntity>();
            var rootDirectories = new List<DirectoryEntityInfo>();

            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    var name = Path.GetFileName(path);
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
            await scanner.ScanAsync(rootDirectories, cancellationToken);

            foreach (var rootDirectory in rootDirectories)
            {
                rootDirectory.CalculateSize();
            }

            return rootItems;
        }
    }
}
