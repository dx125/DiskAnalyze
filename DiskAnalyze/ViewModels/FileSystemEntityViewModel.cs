using DiskAnalyze.Core;
using System.Windows;

namespace DiskAnalyze.ViewModels
{
    public class FileSystemEntityViewModel : ViewModelBase
    {
        private string _name;
        private string _fullPath;
        private DateTime _lastModified;
        private bool _isDirectory;
        private long _size;
        private bool _isHovered;
        private bool _isOtherCategory;
        private int _colorIndex;
        private double _percentageOfTotal;
        private Point _arcStartPoint;
        private Point _arcEndPoint;
        private bool _isLargeArc;
        private double _labelX;
        private double _labelY;
        private double _tooltipX;
        private double _tooltipY;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string FullPath
        {
            get => _fullPath;
            set => SetProperty(ref _fullPath, value);
        }

        public DateTime LastModified
        {
            get => _lastModified;
            set => SetProperty(ref _lastModified, value);
        }

        public bool IsDirectory
        {
            get => _isDirectory;
            set => SetProperty(ref _isDirectory, value);
        }

        public long Size
        {
            get => _size;
            set => SetProperty(ref _size, value);
        }

        public string DisplayName => IsOtherCategory ? "Other" : Name;

        /// <summary>True for a cloud-only placeholder file (e.g. OneDrive "online-only"): it uses no local space.</summary>
        public bool IsCloudOnly { get; set; }

        /// <summary>True for top-level disk entities (C:\, D:\, …), which carry drive space-usage info.</summary>
        public bool IsDrive { get; set; }

        /// <summary>Percentage of the drive's total capacity that is in use. Only meaningful when <see cref="IsDrive"/>.</summary>
        public double DiskUsagePercent { get; set; }

        /// <summary>Used drive space the scan couldn't account for (e.g. folders without access). Drives only.</summary>
        public long UnscannedBytes { get; set; }

        public string UsageInfo
        {
            get
            {
                if (!IsDrive)
                    return string.Empty;

                var usage = $"{DiskUsagePercent:0.#}% used";
                return UnscannedBytes > 0 ? $"{usage} · {FileSizeFormatter.Format(UnscannedBytes)} not scanned" : usage;
            }
        }

        public string ModifiedInfo => IsOtherCategory ? string.Empty : $"Modified {LastModified:yyyy-MM-dd HH:mm}";

        public bool IsHovered
        {
            get => _isHovered;
            set => SetProperty(ref _isHovered, value);
        }

        public bool IsOtherCategory
        {
            get => _isOtherCategory;
            set => SetProperty(ref _isOtherCategory, value);
        }

        /// <summary>Size rank among the displayed siblings; selects the segment color.</summary>
        public int ColorIndex
        {
            get => _colorIndex;
            set => SetProperty(ref _colorIndex, value);
        }

        public DirectoryEntityViewModel? Parent { get; set; }

        public double PercentageOfTotal
        {
            get => _percentageOfTotal;
            set => SetProperty(ref _percentageOfTotal, value);
        }

        // Pie-sector geometry, computed by CircularDiagramView for the currently displayed entities.
        public Point ArcStartPoint
        {
            get => _arcStartPoint;
            set => SetProperty(ref _arcStartPoint, value);
        }

        public Point ArcEndPoint
        {
            get => _arcEndPoint;
            set => SetProperty(ref _arcEndPoint, value);
        }

        public bool IsLargeArc
        {
            get => _isLargeArc;
            set => SetProperty(ref _isLargeArc, value);
        }

        public double LabelX
        {
            get => _labelX;
            set => SetProperty(ref _labelX, value);
        }

        public double LabelY
        {
            get => _labelY;
            set => SetProperty(ref _labelY, value);
        }

        public double TooltipX
        {
            get => _tooltipX;
            set => SetProperty(ref _tooltipX, value);
        }

        public double TooltipY
        {
            get => _tooltipY;
            set => SetProperty(ref _tooltipY, value);
        }

        public FileSystemEntityViewModel()
        {
            _name = string.Empty;
            _fullPath = string.Empty;
            _lastModified = DateTime.Now;
        }
    }

    public class OtherCategoryViewModel : FileSystemEntityViewModel
    {
        private List<FileSystemEntityViewModel> _containedEntities;

        public List<FileSystemEntityViewModel> ContainedEntities
        {
            get => _containedEntities;
            set => SetProperty(ref _containedEntities, value);
        }

        public OtherCategoryViewModel()
        {
            IsOtherCategory = true;
            Name = "Other";
            _containedEntities = new List<FileSystemEntityViewModel>();
        }
    }
}
