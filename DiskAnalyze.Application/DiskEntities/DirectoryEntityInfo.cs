namespace DiskAnalyze.Application.DiskEntities
{
    public class DirectoryEntityInfo : BaseFileSystemEntity
    {
        public override bool IsDirectory => true;

        public List<BaseFileSystemEntity> Children { get; }

        private long _size = 0;
        public override long Size => _size;

        public DirectoryEntityInfo(string name, string path, DateTime lastModified) : base(name, path, lastModified)
        {
            Children = new List<BaseFileSystemEntity>();
        }

        public static DirectoryEntityInfo CreateFromDirectory(DirectoryInfo directory)
        {
            return new DirectoryEntityInfo(directory.Name, directory.FullName, directory.LastWriteTime);
        }

        public long CalculateSize()
        {
            long total = 0;

            foreach (var child in Children)
            {
                total += child is DirectoryEntityInfo dir ? dir.CalculateSize() : child.Size;
            }

            _size = total;
            return total;
        }
    }
}
