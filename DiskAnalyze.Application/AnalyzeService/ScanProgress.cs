namespace DiskAnalyze.Application.AnalyzeService
{
    /// <summary>
    /// Snapshot of a running scan. Counters are cumulative since the scan started;
    /// <see cref="CurrentPath"/> is the directory most recently picked up by a worker.
    /// </summary>
    public readonly record struct ScanProgress(long Directories, long Files, long Bytes, string? CurrentPath);
}
