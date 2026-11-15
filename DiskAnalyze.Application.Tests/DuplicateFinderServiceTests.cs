using DiskAnalyze.Application.AnalyzeService;
using DiskAnalyze.Application.Duplicates;

namespace DiskAnalyze.Application.Tests
{
    public class DuplicateFinderServiceTests
    {
        private readonly DuplicateFinderService _service = new(new AnalyzeDiskService());

        [Fact]
        public async Task FindsIdenticalFilesRegardlessOfName()
        {
            using var temp = new TempDirectory();
            var a = temp.CreateDirectory("A");
            var b = temp.CreateDirectory("B");
            temp.WriteFile(@"A\holiday.jpg", "same content");
            temp.WriteFile(@"B\IMG_0001.jpg", "same content");

            var groups = await _service.FindDuplicatesAsync(a, b);

            var group = Assert.Single(groups);
            Assert.Equal("holiday.jpg", Assert.Single(group.LeftFiles).Name);
            Assert.Equal("IMG_0001.jpg", Assert.Single(group.RightFiles).Name);
        }

        [Fact]
        public async Task IgnoresFilesWithSameSizeButDifferentContent()
        {
            using var temp = new TempDirectory();
            var a = temp.CreateDirectory("A");
            var b = temp.CreateDirectory("B");
            temp.WriteFile(@"A\x.txt", "aaaa");
            temp.WriteFile(@"B\x.txt", "bbbb");

            Assert.Empty(await _service.FindDuplicatesAsync(a, b));
        }

        // D-013: the extension is part of the duplicate key.
        [Fact]
        public async Task IgnoresIdenticalContentWithDifferentExtension()
        {
            using var temp = new TempDirectory();
            var a = temp.CreateDirectory("A");
            var b = temp.CreateDirectory("B");
            temp.WriteFile(@"A\x.txt", "same");
            temp.WriteFile(@"B\x.md", "same");

            Assert.Empty(await _service.FindDuplicatesAsync(a, b));
        }

        [Fact]
        public async Task IgnoresEmptyFiles()
        {
            using var temp = new TempDirectory();
            var a = temp.CreateDirectory("A");
            var b = temp.CreateDirectory("B");
            temp.WriteFile(@"A\empty.log", Array.Empty<byte>());
            temp.WriteFile(@"B\empty.log", Array.Empty<byte>());

            Assert.Empty(await _service.FindDuplicatesAsync(a, b));
        }

        // Regression for I-1: a copy elsewhere in the outer folder was lost when B is inside A.
        [Fact]
        public async Task NestedFolders_FindsDuplicateBetweenOuterFolderAndInnerFolder()
        {
            using var temp = new TempDirectory();
            var photos = temp.CreateDirectory("Photos");
            var year = temp.CreateDirectory(@"Photos\2020");
            temp.WriteFile(@"Photos\x.jpg", "picture bytes");
            temp.WriteFile(@"Photos\2020\x.jpg", "picture bytes");

            var groups = await _service.FindDuplicatesAsync(photos, year);

            var group = Assert.Single(groups);
            Assert.Equal(temp.Combine(@"Photos\x.jpg"), Assert.Single(group.LeftFiles).FullPath);
            Assert.Equal(temp.Combine(@"Photos\2020\x.jpg"), Assert.Single(group.RightFiles).FullPath);
        }

        [Fact]
        public async Task NestedFolders_WorksWhenLeftIsInsideRight()
        {
            using var temp = new TempDirectory();
            var photos = temp.CreateDirectory("Photos");
            var year = temp.CreateDirectory(@"Photos\2020");
            temp.WriteFile(@"Photos\x.jpg", "picture bytes");
            temp.WriteFile(@"Photos\2020\x.jpg", "picture bytes");

            var groups = await _service.FindDuplicatesAsync(year, photos);

            var group = Assert.Single(groups);
            Assert.Equal(temp.Combine(@"Photos\2020\x.jpg"), Assert.Single(group.LeftFiles).FullPath);
            Assert.Equal(temp.Combine(@"Photos\x.jpg"), Assert.Single(group.RightFiles).FullPath);
        }

        [Fact]
        public async Task NestedFolders_FileIsNeverItsOwnDuplicate()
        {
            using var temp = new TempDirectory();
            var photos = temp.CreateDirectory("Photos");
            var year = temp.CreateDirectory(@"Photos\2020");
            temp.WriteFile(@"Photos\2020\only.jpg", "unique picture");

            Assert.Empty(await _service.FindDuplicatesAsync(photos, year));
        }

        [Fact]
        public async Task ReclaimableBytesCountsAllButOneCopy()
        {
            using var temp = new TempDirectory();
            var a = temp.CreateDirectory("A");
            var b = temp.CreateDirectory("B");
            var content = new byte[100];
            temp.WriteFile(@"A\one.bin", content);
            temp.WriteFile(@"A\two.bin", content);
            temp.WriteFile(@"B\three.bin", content);

            var group = Assert.Single(await _service.FindDuplicatesAsync(a, b));

            Assert.Equal(3, group.FileCount);
            Assert.Equal(200, group.ReclaimableBytes);
        }

        [Fact]
        public async Task ReportsHashingProgressUpToTotal()
        {
            using var temp = new TempDirectory();
            var a = temp.CreateDirectory("A");
            var b = temp.CreateDirectory("B");
            temp.WriteFile(@"A\x.bin", "dup");
            temp.WriteFile(@"B\y.bin", "dup");
            var progress = new RecordingProgress<DuplicateSearchProgress>();

            await _service.FindDuplicatesAsync(a, b, progress);

            var final = progress.Reports.Last(r => r.Phase == DuplicateSearchPhase.Hashing);
            Assert.Equal(2, final.Total);
            Assert.Equal(2, final.Completed);
        }
    }
}
