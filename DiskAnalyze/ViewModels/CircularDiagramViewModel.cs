namespace DiskAnalyze.ViewModels
{
    public class CircularDiagramViewModel : DiagramViewModelBase
    {
        private const int MaxDisplayedItems = 5;

        protected override IReadOnlyList<FileSystemEntityViewModel> CreateDisplayedEntities()
        {
            var sortedChildren = CurrentDirectory.Children
                .Where(c => c.Size > 0)
                .OrderByDescending(c => c.Size)
                .ToList();

            var entities = sortedChildren.Take(MaxDisplayedItems).ToList();

            // Group everything beyond the top items into a single "Other" slice
            if (sortedChildren.Count > MaxDisplayedItems)
            {
                var otherItems = sortedChildren.Skip(MaxDisplayedItems).ToList();

                entities.Add(new OtherCategoryViewModel
                {
                    Size = otherItems.Sum(i => i.Size),
                    FullPath = CurrentDirectory.FullPath + "\\Other",
                    LastModified = DateTime.Now,
                    ContainedEntities = otherItems
                });
            }

            CalculatePercentages(entities, CurrentDirectory.Size);

            return entities;
        }
    }
}
