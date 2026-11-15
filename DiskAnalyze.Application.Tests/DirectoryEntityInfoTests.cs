using DiskAnalyze.Application.DiskEntities;

namespace DiskAnalyze.Application.Tests
{
    public class DirectoryEntityInfoTests
    {
        [Fact]
        public void CalculateSize_SumsFilesAtAllLevels()
        {
            var root = Directory("root");
            var sub = Directory("sub");
            var subSub = Directory("subsub");

            root.Children.Add(File("a.bin", 100));
            root.Children.Add(sub);
            sub.Children.Add(File("b.bin", 20));
            sub.Children.Add(subSub);
            subSub.Children.Add(File("c.bin", 3));

            Assert.Equal(123, root.CalculateSize());
            Assert.Equal(23, sub.Size);
            Assert.Equal(3, subSub.Size);
        }

        [Fact]
        public void CalculateSize_EmptyDirectoryIsZero()
        {
            Assert.Equal(0, Directory("empty").CalculateSize());
        }

        // Regression for I-17: the recursive version overflowed the stack on very deep trees.
        [Fact]
        public void CalculateSize_HandlesVeryDeepTrees()
        {
            const int depth = 200_000;
            var root = Directory("level0");
            var current = root;

            for (int i = 1; i < depth; i++)
            {
                var next = Directory($"level{i}");
                current.Children.Add(next);
                current = next;
            }

            current.Children.Add(File("deepest.bin", 42));

            Assert.Equal(42, root.CalculateSize());
        }

        private static DirectoryEntityInfo Directory(string name) => new(name, @"C:\" + name, DateTime.Now);

        private static FileEntityInfo File(string name, long size) =>
            new(name, @"C:\" + name, size, Path.GetExtension(name), DateTime.Now);
    }
}
