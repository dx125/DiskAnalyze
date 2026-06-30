namespace DiskAnalyze.Application.DiskEntities
{
    public class FileEntityInfo : BaseFileSystemEntity
    {
        public override bool IsDirectory => false;

        public string Extension { get; }

        public override long Size { get; }

        public FileEntityInfo(string name, string path, long size, string extension, DateTime lastModified) : base (name, path, lastModified)
        {
            Size = size;
            Extension = extension;
        }

        public static FileEntityInfo CreateFromFile(System.IO.FileInfo file)
        {
            return new FileEntityInfo(file.Name, file.FullName, file.Length, file.Extension, file.LastWriteTime);
        }
    }
}
