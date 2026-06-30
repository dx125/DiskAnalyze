using DiskAnalyze.Application.DiskEntities;
using System.Diagnostics;
using System.Threading.Channels;

namespace DiskAnalyze.Application.AnalyzeService
{
    /// <summary>
    /// Walks directory trees in parallel. Discovered subdirectories are fed back into a shared
    /// work channel; the channel is completed once the last outstanding directory has been scanned.
    /// </summary>
    internal sealed class DirectoryTreeScanner
    {
        private readonly Channel<DirectoryEntityInfo> _pendingDirectories = Channel.CreateUnbounded<DirectoryEntityInfo>();
        private int _outstandingDirectories;

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
        private void ScanDirectory(DirectoryEntityInfo directory)
        {
            var directoryInfo = new DirectoryInfo(directory.FullPath);

            foreach (var file in SafeGetFiles(directoryInfo))
            {
                directory.Children.Add(FileEntityInfo.CreateFromFile(file));
            }

            foreach (var subdirectory in SafeGetDirectories(directoryInfo))
            {
                // Junctions and symlinks can create cycles (e.g. legacy C:\Users junctions)
                // and double-count their targets, so don't follow them.
                if ((subdirectory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                var child = DirectoryEntityInfo.CreateFromDirectory(subdirectory);
                directory.Children.Add(child);

                EnqueueDirectory(child);
            }
        }

        private static FileInfo[] SafeGetFiles(DirectoryInfo directoryInfo)
        {
            try
            {
                return directoryInfo.GetFiles();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                Debug.WriteLine($"Error listing files in {directoryInfo.FullName}: {ex.Message}");
                return Array.Empty<FileInfo>();
            }
        }

        private static DirectoryInfo[] SafeGetDirectories(DirectoryInfo directoryInfo)
        {
            try
            {
                return directoryInfo.GetDirectories();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                Debug.WriteLine($"Error listing subdirectories of {directoryInfo.FullName}: {ex.Message}");
                return Array.Empty<DirectoryInfo>();
            }
        }
    }
}
