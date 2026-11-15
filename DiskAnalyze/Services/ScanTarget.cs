namespace DiskAnalyze.Services
{
    /// <summary>Something the user can scan: all fixed drives, a single drive, or a folder.</summary>
    public sealed record ScanTarget(string DisplayName, IReadOnlyList<string> Paths)
    {
        public override string ToString() => DisplayName;
    }
}
