using DiskAnalyze.Application.Settings;

namespace DiskAnalyze.Application.Tests
{
    public class SettingsStoreTests
    {
        [Fact]
        public void Load_MissingFile_ReturnsDefaults()
        {
            using var temp = new TempDirectory();
            var store = new SettingsStore(temp.Combine("settings.json"));

            Assert.Equal(new AppSettings(), store.Load());
        }

        [Fact]
        public void Save_CreatesFolder_AndLoadRoundTrips()
        {
            using var temp = new TempDirectory();
            var store = new SettingsStore(temp.Combine(@"nested\DiskAnalyze\settings.json"));
            var settings = new AppSettings
            {
                Theme = ThemePreference.Dark,
                ScanOnStartup = false,
                StartupScanPath = @"D:\",
                PieSlices = 7,
                PhotoAnalysis = new PhotoAnalysisSettings
                {
                    PhotoContentAnalysis = PhotoContentAnalysisMode.Disabled,
                    ClipOnnx = ClipOnnxMode.Deny,
                    ExecutionProvider = ExecutionProviderPreference.DirectML
                }
            };

            store.Save(settings);

            Assert.Equal(settings, store.Load());
            Assert.False(File.Exists(store.FilePath + ".tmp"));
        }

        [Fact]
        public void Save_WritesReadableNames()
        {
            using var temp = new TempDirectory();
            var store = new SettingsStore(temp.Combine("settings.json"));

            store.Save(new AppSettings
            {
                Theme = ThemePreference.Dark,
                PhotoAnalysis = new PhotoAnalysisSettings { ExecutionProvider = ExecutionProviderPreference.DirectML }
            });

            var json = File.ReadAllText(store.FilePath);
            Assert.Contains("\"theme\": \"dark\"", json);
            Assert.Contains("\"pieSlices\": 5", json);
            Assert.Contains("\"executionProvider\": \"directml\"", json);
        }

        [Fact]
        public void Load_HandEditedFile_AcceptsCommentsCaseAndMissingKeys()
        {
            using var temp = new TempDirectory();
            var path = temp.WriteFile("settings.json", """
                {
                  // edited by hand
                  "Theme": "Light",
                  "photoAnalysis": { "clipOnnx": "deny", },
                }
                """);

            var settings = new SettingsStore(path).Load();

            Assert.Equal(ThemePreference.Light, settings.Theme);
            Assert.Equal(ClipOnnxMode.Deny, settings.PhotoAnalysis.ClipOnnx);
            Assert.True(settings.ScanOnStartup);
            Assert.Equal(AppSettings.DefaultPieSlices, settings.PieSlices);
        }

        [Theory]
        [InlineData(0, AppSettings.MinPieSlices)]
        [InlineData(100, AppSettings.MaxPieSlices)]
        [InlineData(6, 6)]
        public void Load_ClampsPieSlices(int stored, int expected)
        {
            using var temp = new TempDirectory();
            var path = temp.WriteFile("settings.json", $$"""{ "pieSlices": {{stored}} }""");

            Assert.Equal(expected, new SettingsStore(path).Load().PieSlices);
        }

        [Theory]
        [InlineData("{ not json")]
        [InlineData("""{ "theme": "purple" }""")]
        [InlineData("""{ "theme": 2 }""")]
        [InlineData("""{ "pieSlices": "many" }""")]
        public void Load_InvalidFile_ReturnsDefaults(string content)
        {
            using var temp = new TempDirectory();
            var path = temp.WriteFile("settings.json", content);

            Assert.Equal(new AppSettings(), new SettingsStore(path).Load());
        }

        [Fact]
        public void Load_NullSection_FallsBackToDefaults()
        {
            using var temp = new TempDirectory();
            var path = temp.WriteFile("settings.json", """{ "photoAnalysis": null, "startupScanPath": "  " }""");

            var settings = new SettingsStore(path).Load();

            Assert.Equal(new PhotoAnalysisSettings(), settings.PhotoAnalysis);
            Assert.Null(settings.StartupScanPath);
        }
    }
}
