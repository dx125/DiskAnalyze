using DiskAnalyze.Application.DiskEntities;

namespace DiskAnalyze.Application.AnalyzeService
{
    public interface IDiskAnalyzeService
    {
        Task<List<BaseFileSystemEntity>> AnalyzeAllDrivesAsync(CancellationToken cancellationToken = default);

        Task<BaseFileSystemEntity?> AnalyzeDriveAsync(string drivePath, CancellationToken cancellationToken = default);

        Task<List<BaseFileSystemEntity>> AnalyzePathsAsync(IReadOnlyCollection<string> paths, CancellationToken cancellationToken = default);
    }
}
