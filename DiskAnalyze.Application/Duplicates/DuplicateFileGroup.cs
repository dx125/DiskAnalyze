using DiskAnalyze.Application.DiskEntities;

namespace DiskAnalyze.Application.Duplicates
{
    /// <summary>
    /// A set of byte-identical files that appear in both of the compared folders.
    /// Files are identical when they share the same size, extension and content hash.
    /// </summary>
    public sealed class DuplicateFileGroup
    {
        public long Size { get; }

        public string Extension { get; }

        public string ContentHash { get; }

        public IReadOnlyList<FileEntityInfo> LeftFiles { get; }

        public IReadOnlyList<FileEntityInfo> RightFiles { get; }

        public int FileCount => LeftFiles.Count + RightFiles.Count;

        /// <summary>Bytes freed by keeping a single copy and removing all the others.</summary>
        public long ReclaimableBytes => Size * (FileCount - 1);

        public DuplicateFileGroup(long size, string extension, string contentHash,
            IReadOnlyList<FileEntityInfo> leftFiles, IReadOnlyList<FileEntityInfo> rightFiles)
        {
            Size = size;
            Extension = extension;
            ContentHash = contentHash;
            LeftFiles = leftFiles;
            RightFiles = rightFiles;
        }
    }
}
