using DiskAnalyze.Application.AnalyzeService;

namespace DiskAnalyze.Application.Tests
{
    public class CloudFilesTests
    {
        [Theory]
        [InlineData(0x0040_1620)] // OneDrive online-only file: RecallOnDataAccess | Offline | ReparsePoint | Sparse | Archive
        [InlineData(0x0040_0000)] // RecallOnDataAccess
        [InlineData(0x0004_0000)] // RecallOnOpen
        [InlineData(0x0000_1000)] // Offline
        public void IsCloudOnly_TrueForPlaceholders(int attributes)
        {
            Assert.True(CloudFiles.IsCloudOnly((FileAttributes)attributes));
        }

        [Theory]
        [InlineData(0x0000_0020)] // plain Archive file
        [InlineData(0x0000_0080)] // Normal
        [InlineData(0x0008_0420)] // OneDrive "always keep on this device": Pinned | ReparsePoint | Archive
        [InlineData(0x0000_0626)] // locally available cloud file: ReparsePoint | Sparse | Archive | System | Hidden
        public void IsCloudOnly_FalseForLocalFiles(int attributes)
        {
            Assert.False(CloudFiles.IsCloudOnly((FileAttributes)attributes));
        }
    }
}
