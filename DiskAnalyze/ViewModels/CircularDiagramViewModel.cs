using DiskAnalyze.Application.Settings;

namespace DiskAnalyze.ViewModels
{
    public class CircularDiagramViewModel : DiagramViewModelBase
    {
        private int _sliceCount = AppSettings.DefaultPieSlices;

        /// <summary>
        /// How many of the largest items get their own slice; the rest are grouped as "Other".
        /// Set from the Settings view (<see cref="AppSettings.PieSlices"/>); a change redraws the pie.
        /// </summary>
        public int SliceCount
        {
            get => _sliceCount;
            set
            {
                if (SetProperty(ref _sliceCount, Math.Clamp(value, AppSettings.MinPieSlices, AppSettings.MaxPieSlices)))
                {
                    RefreshDisplayedEntities();
                }
            }
        }

        protected override IReadOnlyList<FileSystemEntityViewModel> CreateDisplayedEntities()
        {
            var sortedChildren = CurrentDirectory.Children
                .Where(c => c.Size > 0)
                .OrderByDescending(c => c.Size)
                .ToList();

            var entities = sortedChildren.Take(SliceCount).ToList();

            // Group everything beyond the top items into a single "Other" slice
            if (sortedChildren.Count > SliceCount)
            {
                var otherItems = sortedChildren.Skip(SliceCount).ToList();

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
