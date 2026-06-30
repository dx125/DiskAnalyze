namespace DiskAnalyze.ViewModels
{
    public class ChartDiagramViewModel : DiagramViewModelBase
    {
        protected override IReadOnlyList<FileSystemEntityViewModel> CreateDisplayedEntities()
        {
            var sortedChildren = CurrentDirectory.Children
                .Where(c => c.Size > 0)
                .OrderByDescending(c => c.Size)
                .ToList();

            CalculatePercentages(sortedChildren, CurrentDirectory.Size);

            return sortedChildren;
        }
    }
}
