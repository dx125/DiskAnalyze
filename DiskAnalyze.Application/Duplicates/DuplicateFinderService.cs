using System.Collections.Concurrent;
using System.Security.Cryptography;
using DiskAnalyze.Application.AnalyzeService;
using DiskAnalyze.Application.DiskEntities;

namespace DiskAnalyze.Application.Duplicates
{
    public sealed class DuplicateFinderService : IDuplicateFinderService
    {
        private const int HashProgressStep = 32;

        private readonly IDiskAnalyzeService _analyzeService;

        public DuplicateFinderService(IDiskAnalyzeService analyzeService)
        {
            _analyzeService = analyzeService;
        }

        public async Task<IReadOnlyList<DuplicateFileGroup>> FindDuplicatesAsync(string leftFolder, string rightFolder,
            IProgress<DuplicateSearchProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var leftFiles = await EnumerateFilesAsync(leftFolder, DuplicateSearchPhase.ScanningLeft, progress, cancellationToken);
            var rightFiles = await EnumerateFilesAsync(rightFolder, DuplicateSearchPhase.ScanningRight, progress, cancellationToken);

            // Cheap prefilter: only files whose (size, extension) appears on both sides can be duplicates.
            var candidateKeys = leftFiles.Select(GetSizeExtensionKey).ToHashSet();
            candidateKeys.IntersectWith(rightFiles.Select(GetSizeExtensionKey));

            if (candidateKeys.Count == 0)
            {
                return Array.Empty<DuplicateFileGroup>();
            }

            var leftCandidates = leftFiles.Where(f => candidateKeys.Contains(GetSizeExtensionKey(f))).ToList();
            var rightCandidates = rightFiles.Where(f => candidateKeys.Contains(GetSizeExtensionKey(f))).ToList();

            // Confirm with a content hash, computed only for the (few) prefiltered candidates.
            var hashes = await ComputeHashesAsync(leftCandidates.Concat(rightCandidates), progress, cancellationToken);

            var leftByContent = GroupByContent(leftCandidates, hashes);
            var rightByContent = GroupByContent(rightCandidates, hashes);

            var nesting = GetNesting(leftFolder, rightFolder);
            var groups = new List<DuplicateFileGroup>();

            foreach (var (key, left) in leftByContent)
            {
                if (!rightByContent.TryGetValue(key, out var right))
                {
                    continue;
                }

                // When one folder contains the other, files under the inner folder were scanned on both
                // sides. They belong to the inner folder only, which also guarantees a physical file is
                // never reported as a duplicate of itself.
                var leftFiltered = nesting == Nesting.RightInsideLeft ? ExceptPaths(left, right) : left;
                var rightFiltered = nesting == Nesting.LeftInsideRight ? ExceptPaths(right, left) : right;

                if (leftFiltered.Count == 0 || rightFiltered.Count == 0)
                {
                    continue;
                }

                groups.Add(new DuplicateFileGroup(key.Size, key.Extension, key.Hash, leftFiltered, rightFiltered));
            }

            return groups.OrderByDescending(g => g.Size).ToList();
        }

        private async Task<List<FileEntityInfo>> EnumerateFilesAsync(string folder, DuplicateSearchPhase phase,
            IProgress<DuplicateSearchProgress>? progress, CancellationToken ct)
        {
            var scanProgress = progress == null
                ? null
                : new RelayProgress<ScanProgress>(p => progress.Report(new DuplicateSearchProgress(phase, p.Files, 0)));

            var roots = await _analyzeService.AnalyzePathsAsync(new[] { folder }, scanProgress, ct);

            var files = new List<FileEntityInfo>();
            foreach (var root in roots)
            {
                CollectComparableFiles(root, files);
            }

            return files;
        }

        // Empty files carry no reclaimable space, and hashing a cloud-only placeholder would download it.
        private static bool IsComparable(FileEntityInfo file) => file.Size > 0 && !file.IsCloudOnly;

        private static void CollectComparableFiles(BaseFileSystemEntity root, List<FileEntityInfo> files)
        {
            var pending = new Stack<BaseFileSystemEntity>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                switch (pending.Pop())
                {
                    case FileEntityInfo file when IsComparable(file):
                        files.Add(file);
                        break;

                    case DirectoryEntityInfo directory:
                        foreach (var child in directory.Children)
                        {
                            pending.Push(child);
                        }
                        break;
                }
            }
        }

        private static async Task<IReadOnlyDictionary<string, string>> ComputeHashesAsync(
            IEnumerable<FileEntityInfo> files, IProgress<DuplicateSearchProgress>? progress, CancellationToken ct)
        {
            var hashes = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // The same physical path can appear on both sides (nested folder selections); hash it once.
            var distinctPaths = files
                .Select(f => f.FullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            long total = distinctPaths.Count;
            long completed = 0;
            progress?.Report(new DuplicateSearchProgress(DuplicateSearchPhase.Hashing, 0, total));

            await Parallel.ForEachAsync(distinctPaths,
                new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (path, token) =>
                {
                    var hash = await TryComputeHashAsync(path, token);
                    if (hash != null)
                    {
                        hashes[path] = hash;
                    }

                    long done = Interlocked.Increment(ref completed);
                    if (done % HashProgressStep == 0 || done == total)
                    {
                        progress?.Report(new DuplicateSearchProgress(DuplicateSearchPhase.Hashing, done, total));
                    }
                });

            return hashes;
        }

        private static async Task<string?> TryComputeHashAsync(string path, CancellationToken ct)
        {
            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 1 << 16, useAsync: true);

                var hash = await SHA256.HashDataAsync(stream, ct);
                return Convert.ToHexString(hash);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static Dictionary<ContentKey, List<FileEntityInfo>> GroupByContent(
            IEnumerable<FileEntityInfo> files, IReadOnlyDictionary<string, string> hashes)
        {
            var result = new Dictionary<ContentKey, List<FileEntityInfo>>();

            foreach (var file in files)
            {
                if (!hashes.TryGetValue(file.FullPath, out var hash))
                {
                    continue; // unreadable file, skip
                }

                var key = new ContentKey(file.Size, NormalizeExtension(file.Extension), hash);
                if (!result.TryGetValue(key, out var list))
                {
                    list = new List<FileEntityInfo>();
                    result[key] = list;
                }

                list.Add(file);
            }

            return result;
        }

        private static List<FileEntityInfo> ExceptPaths(List<FileEntityInfo> files, List<FileEntityInfo> excluded)
        {
            var excludedPaths = excluded.Select(f => f.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return files.Where(f => !excludedPaths.Contains(f.FullPath)).ToList();
        }

        private static Nesting GetNesting(string leftFolder, string rightFolder)
        {
            if (IsSameOrInside(rightFolder, leftFolder))
                return Nesting.RightInsideLeft;

            if (IsSameOrInside(leftFolder, rightFolder))
                return Nesting.LeftInsideRight;

            return Nesting.None;
        }

        private static bool IsSameOrInside(string path, string folder) =>
            WithTrailingSeparator(path).StartsWith(WithTrailingSeparator(folder), StringComparison.OrdinalIgnoreCase);

        // "C:\" already ends with a separator; "C:\Photos" doesn't.
        private static string WithTrailingSeparator(string path)
        {
            var fullPath = Path.GetFullPath(path);
            return Path.EndsInDirectorySeparator(fullPath) ? fullPath : fullPath + Path.DirectorySeparatorChar;
        }

        private static (long Size, string Extension) GetSizeExtensionKey(FileEntityInfo file) =>
            (file.Size, NormalizeExtension(file.Extension));

        private static string NormalizeExtension(string extension) => extension.ToLowerInvariant();

        private enum Nesting
        {
            None,
            RightInsideLeft,
            LeftInsideRight
        }

        private readonly record struct ContentKey(long Size, string Extension, string Hash);

        /// <summary>Invokes the callback synchronously on the reporting thread (unlike <see cref="Progress{T}"/>).</summary>
        private sealed class RelayProgress<T> : IProgress<T>
        {
            private readonly Action<T> _report;

            public RelayProgress(Action<T> report) => _report = report;

            public void Report(T value) => _report(value);
        }
    }
}
