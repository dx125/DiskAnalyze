namespace DiskAnalyze.ViewModels
{
    /// <summary>
    /// A single side-by-side row: one file from folder A paired with its identical counterpart in folder B.
    /// Either side may be null when a group has more matches on one side than the other.
    /// </summary>
    public class DuplicateMatchViewModel
    {
        public string? LeftName { get; init; }

        public string? LeftFullPath { get; init; }

        public string? RightName { get; init; }

        public string? RightFullPath { get; init; }

        public long Size { get; init; }

        public string Extension { get; init; } = string.Empty;
    }
}
