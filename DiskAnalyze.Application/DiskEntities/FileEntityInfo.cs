using DiskAnalyze.Application.AnalyzeService;

namespace DiskAnalyze.Application.DiskEntities
{
    public class FileEntityInfo : BaseFileSystemEntity
    {
        public override bool IsDirectory => false;

        public string Extension { get; }

        /// <summary>Bytes the file occupies locally: 0 for cloud-only placeholders.</summary>
        public override long Size { get; }

        /// <summary>The content lives only in the cloud; reading the file would download it.</summary>
        public bool IsCloudOnly { get; }

        public FileEntityInfo(string name, string path, long size, string extension, DateTime lastModified, bool isCloudOnly = false)
            : base(name, path, lastModified)
        {
            Size = size;
            Extension = extension;
            IsCloudOnly = isCloudOnly;
        }

        public static FileEntityInfo CreateFromFile(FileInfo file)
        {
            bool isCloudOnly = CloudFiles.IsCloudOnly(file.Attributes);

            return new FileEntityInfo(file.Name, file.FullName, isCloudOnly ? 0 : file.Length, file.Extension,
                file.LastWriteTime, isCloudOnly);
        }
    }
}
