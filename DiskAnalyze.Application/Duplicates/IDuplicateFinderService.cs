namespace DiskAnalyze.Application.Duplicates
{
    public interface IDuplicateFinderService
    {
        /// <summary>
        /// Finds files that exist in both folders with identical content, regardless of name.
        /// </summary>
        Task<IReadOnlyList<DuplicateFileGroup>> FindDuplicatesAsync(
            string leftFolder, string rightFolder, CancellationToken cancellationToken = default);
    }
}
