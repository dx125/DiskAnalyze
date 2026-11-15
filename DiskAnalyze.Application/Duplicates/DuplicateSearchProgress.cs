namespace DiskAnalyze.Application.Duplicates
{
    public enum DuplicateSearchPhase
    {
        ScanningLeft,
        ScanningRight,
        Hashing
    }

    /// <summary>
    /// Progress of a duplicate search. While scanning, <see cref="Completed"/> counts files found so far
    /// and <see cref="Total"/> is 0 (unknown); while hashing, both count candidate files.
    /// </summary>
    public readonly record struct DuplicateSearchProgress(DuplicateSearchPhase Phase, long Completed, long Total);
}
