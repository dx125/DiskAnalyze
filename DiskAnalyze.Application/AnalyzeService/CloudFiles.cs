namespace DiskAnalyze.Application.AnalyzeService
{
    /// <summary>
    /// Recognizes cloud-files placeholders (OneDrive Files On-Demand and other cloud sync providers).
    /// </summary>
    public static class CloudFiles
    {
        // Win32 attributes that have no member in the FileAttributes enum.
        private const FileAttributes RecallOnOpen = (FileAttributes)0x0004_0000;
        private const FileAttributes RecallOnDataAccess = (FileAttributes)0x0040_0000;

        /// <summary>
        /// True when the file's content is not stored locally: it occupies no local disk space,
        /// and opening it for reading would download it from the cloud.
        /// </summary>
        public static bool IsCloudOnly(FileAttributes attributes) =>
            (attributes & (RecallOnDataAccess | RecallOnOpen | FileAttributes.Offline)) != 0;
    }
}
