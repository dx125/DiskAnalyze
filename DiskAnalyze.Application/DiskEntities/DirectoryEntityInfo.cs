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

        /// <summary>
        /// Computes <see cref="Size"/> for this directory and every directory below it.
        /// Iterative, so arbitrarily deep trees can't overflow the stack.
        /// </summary>
        public long CalculateSize()
        {
            // Pre-order walk: every directory is listed after its parent...
            var directories = new List<DirectoryEntityInfo>();
            var pending = new Stack<DirectoryEntityInfo>();
            pending.Push(this);

            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                directories.Add(directory);

                foreach (var child in directory.Children)
                {
                    if (child is DirectoryEntityInfo subdirectory)
                    {
                        pending.Push(subdirectory);
                    }
                }
            }

            // ...so walking the list backwards sizes all children before their parent.
            for (int i = directories.Count - 1; i >= 0; i--)
            {
                long total = 0;
                foreach (var child in directories[i].Children)
                {
                    total += child.Size;
                }

                directories[i]._size = total;
            }

            return _size;
        }
    }
}
