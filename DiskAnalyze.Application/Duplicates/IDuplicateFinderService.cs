namespace DiskAnalyze.Application.Duplicates
{
    public interface IDuplicateFinderService
    {
        /// <summary>
        /// Finds files that exist in both folders with identical content, regardless of name.
        /// When one folder contains the other, the comparison is "rest of the outer folder" vs "inner folder".
        /// Empty files and cloud-only placeholders are ignored.
        /// </summary>
        Task<IReadOnlyList<DuplicateFileGroup>> FindDuplicatesAsync(string leftFolder, string rightFolder,
            IProgress<DuplicateSearchProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}
