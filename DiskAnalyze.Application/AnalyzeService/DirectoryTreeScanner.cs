using DiskAnalyze.Application.DiskEntities;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Threading.Channels;

namespace DiskAnalyze.Application.AnalyzeService
{
    /// <summary>
    /// Walks directory trees in parallel. Discovered subdirectories are fed back into a shared
    /// work channel; the channel is completed once the last outstanding directory has been scanned.
    /// </summary>
    internal sealed class DirectoryTreeScanner
    {
        private static readonly EnumerationOptions ListingOptions = new()
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            AttributesToSkip = 0, // hidden and system files take space too
            ReturnSpecialDirectories = false
        };

        private readonly Channel<DirectoryEntityInfo> _pendingDirectories = Channel.CreateUnbounded<DirectoryEntityInfo>();
        private int _outstandingDirectories;

        private long _directoriesScanned;
        private long _filesScanned;
        private long _bytesScanned;
        private volatile string? _currentPath;

        public ScanProgress GetProgress() => new(
            Interlocked.Read(ref _directoriesScanned),
            Interlocked.Read(ref _filesScanned),
            Interlocked.Read(ref _bytesScanned),
            _currentPath);

        public async Task ScanAsync(IReadOnlyCollection<DirectoryEntityInfo> roots, CancellationToken cancellationToken)
        {
            if (roots.Count == 0)
            {
                return;
            }

            foreach (var root in roots)
            {
                EnqueueDirectory(root);
            }

            var workerCount = Math.Max(1, Environment.ProcessorCount);
            var workers = Enumerable.Range(0, workerCount)
                .Select(_ => Task.Run(() => ProcessPendingDirectoriesAsync(cancellationToken), cancellationToken))
                .ToArray();

            await Task.WhenAll(workers);
        }

        private void EnqueueDirectory(DirectoryEntityInfo directory)
        {
            Interlocked.Increment(ref _outstandingDirectories);
            _pendingDirectories.Writer.TryWrite(directory);
        }

        private async Task ProcessPendingDirectoriesAsync(CancellationToken cancellationToken)
        {
            await foreach (var directory in _pendingDirectories.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    ScanDirectory(directory);
                }
                finally
                {
                    if (Interlocked.Decrement(ref _outstandingDirectories) == 0)
                    {
                        _pendingDirectories.Writer.Complete();
                    }
                }
            }
        }

        // Each directory is owned by exactly one worker, so its Children list needs no locking.
        // One enumeration pass yields both files and subdirectories without allocating FileInfo objects.
        private void ScanDirectory(DirectoryEntityInfo directory)
        {
            _currentPath = directory.FullPath;
            long files = 0;
            long bytes = 0;

            try
            {
                var entries = new FileSystemEnumerable<BaseFileSystemEntity?>(directory.FullPath, CreateEntity, ListingOptions);

                foreach (var entity in entries)
                {
                    switch (entity)
                    {
                        case DirectoryEntityInfo subdirectory:
                            directory.Children.Add(subdirectory);
                            EnqueueDirectory(subdirectory);
                            break;

                        case FileEntityInfo file:
                            directory.Children.Add(file);
                            files++;
                            bytes += file.Size;
                            break;

                        // null: a junction or symlink that is deliberately not followed.
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Keep whatever was listed before the failure (e.g. the directory vanished mid-scan).
                Debug.WriteLine($"Error listing {directory.FullPath}: {ex.Message}");
            }

            // Publish counters once per directory rather than per file to avoid cross-core contention.
            Interlocked.Increment(ref _directoriesScanned);
            Interlocked.Add(ref _filesScanned, files);
            Interlocked.Add(ref _bytesScanned, bytes);
        }

        private static BaseFileSystemEntity? CreateEntity(ref FileSystemEntry entry)
        {
            var name = entry.FileName.ToString();
            var fullPath = entry.ToFullPath();
            var lastModified = entry.LastWriteTimeUtc.LocalDateTime;

            if (entry.IsDirectory)
            {
                // Junctions and symlinks can create cycles (e.g. legacy C:\Users junctions) and
                // double-count their targets, so don't follow them. Other reparse points are real
                // folders, notably OneDrive's cloud-files folders, and must be scanned.
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0 && IsLink(fullPath))
                {
                    return null;
                }

                return new DirectoryEntityInfo(name, fullPath, lastModified);
            }

            // Cloud-only placeholders report their full logical length but occupy no local space.
            bool isCloudOnly = CloudFiles.IsCloudOnly(entry.Attributes);
            long size = isCloudOnly ? 0 : entry.Length;

            return new FileEntityInfo(name, fullPath, size, Path.GetExtension(name), lastModified, isCloudOnly);
        }

        // LinkTarget is non-null only for symbolic links and junctions/mount points; reading it
        // opens the reparse point itself, so it never triggers a cloud download.
        private static bool IsLink(string directoryPath)
        {
            try
            {
                return new DirectoryInfo(directoryPath).LinkTarget != null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return true; // can't tell: don't follow, so a cycle is impossible
            }
        }
    }
}
