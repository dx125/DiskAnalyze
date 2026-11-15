using DiskAnalyze.Application.DiskEntities;

namespace DiskAnalyze.Application.AnalyzeService
{
    public interface IDiskAnalyzeService
    {
        /// <summary>Root paths (e.g. <c>C:\</c>) of all ready fixed drives.</summary>
        IReadOnlyList<string> GetFixedDrivePaths();

        Task<List<BaseFileSystemEntity>> AnalyzeAllDrivesAsync(
            IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Scans the given directories (and/or single files). <paramref name="progress"/> receives
        /// periodic snapshots from a background thread, plus a final one when the scan completes.
        /// </summary>
        Task<List<BaseFileSystemEntity>> AnalyzePathsAsync(IReadOnlyCollection<string> paths,
            IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}
