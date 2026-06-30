using System.Collections.Concurrent;
using System.Security.Cryptography;
using DiskAnalyze.Application.AnalyzeService;
using DiskAnalyze.Application.DiskEntities;

namespace DiskAnalyze.Application.Duplicates
{
    public sealed class DuplicateFinderService : IDuplicateFinderService
    {
        private readonly IDiskAnalyzeService _analyzeService;

        public DuplicateFinderService(IDiskAnalyzeService analyzeService)
        {
            _analyzeService = analyzeService;
        }

        public async Task<IReadOnlyList<DuplicateFileGroup>> FindDuplicatesAsync(
            string leftFolder, string rightFolder, CancellationToken cancellationToken = default)
        {
            var leftFiles = await EnumerateFilesAsync(leftFolder, cancellationToken);
            var rightFiles = await EnumerateFilesAsync(rightFolder, cancellationToken);

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
            var hashes = await ComputeHashesAsync(leftCandidates.Concat(rightCandidates), cancellationToken);

            var leftByContent = GroupByContent(leftCandidates, hashes);
            var rightByContent = GroupByContent(rightCandidates, hashes);

            var groups = new List<DuplicateFileGroup>();

            foreach (var (key, left) in leftByContent)
            {
                if (!rightByContent.TryGetValue(key, out var right))
                {
                    continue;
                }

                // Guard overlapping/nested folder selections: a physical file scanned on both
                // sides is not a duplicate of itself.
                var rightFiltered = right
                    .Where(r => left.All(l => !PathsEqual(l.FullPath, r.FullPath)))
                    .ToList();

                if (rightFiltered.Count == 0)
                {
                    continue;
                }

                groups.Add(new DuplicateFileGroup(key.Size, key.Extension, key.Hash, left, rightFiltered));
            }

            return groups.OrderByDescending(g => g.Size).ToList();
        }

        private async Task<List<FileEntityInfo>> EnumerateFilesAsync(string folder, CancellationToken ct)
        {
            var roots = await _analyzeService.AnalyzePathsAsync(new[] { folder }, ct);

            var files = new List<FileEntityInfo>();
            foreach (var root in roots)
            {
                Flatten(root, files);
            }

            return files;
        }

        private static void Flatten(BaseFileSystemEntity entity, List<FileEntityInfo> files)
        {
            switch (entity)
            {
                case FileEntityInfo file:
                    files.Add(file);
                    break;
                case DirectoryEntityInfo directory:
                    foreach (var child in directory.Children)
                    {
                        Flatten(child, files);
                    }
                    break;
            }
        }

        private static async Task<IReadOnlyDictionary<string, string>> ComputeHashesAsync(
            IEnumerable<FileEntityInfo> files, CancellationToken ct)
        {
            var hashes = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // The same physical path can appear more than once (e.g. nested folder selections); hash it once.
            var distinctPaths = files
                .Select(f => f.FullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            await Parallel.ForEachAsync(distinctPaths,
                new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (path, token) =>
                {
                    var hash = await TryComputeHashAsync(path, token);
                    if (hash != null)
                    {
                        hashes[path] = hash;
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

        private static (long Size, string Extension) GetSizeExtensionKey(FileEntityInfo file) =>
            (file.Size, NormalizeExtension(file.Extension));

        private static string NormalizeExtension(string extension) => extension.ToLowerInvariant();

        private static bool PathsEqual(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private readonly record struct ContentKey(long Size, string Extension, string Hash);
    }
}
