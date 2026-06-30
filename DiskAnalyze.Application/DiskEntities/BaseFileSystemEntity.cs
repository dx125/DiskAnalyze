namespace DiskAnalyze.Application.DiskEntities
{
    public abstract class BaseFileSystemEntity
    {
        public string Name { get; }

        public string FullPath { get; }

        public DateTime LastModified { get; }  

        public abstract bool IsDirectory { get; }

        public abstract long Size { get; }

        public BaseFileSystemEntity(string name, string path, DateTime lastModified)
        {
            Name = name;
            FullPath = path;
            LastModified = lastModified;
        }
    }
}
