using DiskAnalyze.Core;
using DiskAnalyze.Services;
using System.Windows.Input;

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

        /// <summary>Alternates per duplicate group, so rows of the same group can be shaded together.</summary>
        public bool IsAlternateGroup { get; init; }

        public ICommand ShowLeftInExplorerCommand { get; }

        public ICommand ShowRightInExplorerCommand { get; }

        public DuplicateMatchViewModel()
        {
            ShowLeftInExplorerCommand = new RelayCommand(() => ShellActions.ShowInExplorer(LeftFullPath), () => LeftFullPath != null);
            ShowRightInExplorerCommand = new RelayCommand(() => ShellActions.ShowInExplorer(RightFullPath), () => RightFullPath != null);
        }
    }
}
