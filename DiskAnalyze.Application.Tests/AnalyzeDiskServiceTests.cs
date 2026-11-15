using DiskAnalyze.Application.AnalyzeService;
using DiskAnalyze.Application.DiskEntities;

namespace DiskAnalyze.Application.Tests
{
    public class AnalyzeDiskServiceTests
    {
        private readonly AnalyzeDiskService _service = new();

        [Fact]
        public async Task AnalyzePaths_BuildsTreeWithSizes()
        {
            using var temp = new TempDirectory();
            temp.WriteFile("top.bin", new byte[100]);
            temp.WriteFile(@"sub\one.bin", new byte[20]);
            temp.WriteFile(@"sub\deeper\two.bin", new byte[3]);
            temp.CreateDirectory("empty");

            var root = await ScanSingleAsync(temp.Path);

            Assert.Equal(123, root.Size);
            Assert.Equal(3, root.Children.Count); // top.bin, sub, empty

            var sub = Assert.IsType<DirectoryEntityInfo>(root.Children.Single(c => c.Name == "sub"));
            Assert.Equal(23, sub.Size);
            Assert.Equal(0, root.Children.Single(c => c.Name == "empty").Size);
        }

        [Fact]
        public async Task AnalyzePaths_UsesFolderNameForRootWithTrailingSeparator()
        {
            using var temp = new TempDirectory();
            var folder = temp.CreateDirectory("Photos");

            var root = await ScanSingleAsync(folder + Path.DirectorySeparatorChar);

            Assert.Equal("Photos", root.Name);
        }

        [Fact]
        public async Task AnalyzePaths_CountsHiddenAndSystemFiles()
        {
            using var temp = new TempDirectory();
            var hidden = temp.WriteFile("hidden.bin", new byte[50]);
            File.SetAttributes(hidden, FileAttributes.Hidden | FileAttributes.System);

            var root = await ScanSingleAsync(temp.Path);

            Assert.Equal(50, root.Size);
        }

        [Fact]
        public async Task AnalyzePaths_DoesNotFollowJunctions()
        {
            using var temp = new TempDirectory();
            var target = temp.CreateDirectory("target");
            temp.WriteFile(@"target\data.bin", new byte[1000]);
            temp.CreateJunction("link", target);

            var root = await ScanSingleAsync(temp.Path);

            Assert.Equal(1000, root.Size); // counted once, through "target" only
            Assert.DoesNotContain(root.Children, c => c.Name == "link");
        }

        [Fact]
        public async Task AnalyzePaths_ReportsFinalProgress()
        {
            using var temp = new TempDirectory();
            temp.WriteFile("a.bin", new byte[10]);
            temp.WriteFile(@"sub\b.bin", new byte[5]);
            var progress = new RecordingProgress<ScanProgress>();

            await _service.AnalyzePathsAsync(new[] { temp.Path }, progress);

            var final = progress.Reports.Last();
            Assert.Equal(2, final.Files);
            Assert.Equal(2, final.Directories); // root + sub
            Assert.Equal(15, final.Bytes);
        }

        [Fact]
        public async Task AnalyzePaths_HonorsCancellation()
        {
            using var temp = new TempDirectory();
            temp.WriteFile("a.bin", new byte[10]);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _service.AnalyzePathsAsync(new[] { temp.Path }, cancellationToken: cancellation.Token));
        }

        [Fact]
        public async Task AnalyzePaths_IncludesSingleFilePaths()
        {
            using var temp = new TempDirectory();
            var file = temp.WriteFile("single.bin", new byte[7]);

            var roots = await _service.AnalyzePathsAsync(new[] { file });

            var entity = Assert.IsType<FileEntityInfo>(Assert.Single(roots));
            Assert.Equal(7, entity.Size);
        }

        private async Task<DirectoryEntityInfo> ScanSingleAsync(string path)
        {
            var roots = await _service.AnalyzePathsAsync(new[] { path });
            return Assert.IsType<DirectoryEntityInfo>(Assert.Single(roots));
        }
    }
}
