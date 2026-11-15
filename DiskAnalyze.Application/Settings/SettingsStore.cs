using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiskAnalyze.Application.Settings
{
    /// <summary>
    /// Reads and writes <see cref="AppSettings"/> as JSON. A missing, unreadable or malformed file
    /// yields the defaults, so a bad hand edit never stops the app from starting.
    /// </summary>
    public sealed class SettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
        };

        public SettingsStore(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; }

        /// <summary><c>%LOCALAPPDATA%\DiskAnalyze\settings.json</c></summary>
        public static string DefaultFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiskAnalyze", "settings.json");

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new AppSettings();

                using var stream = File.OpenRead(FilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(stream, JsonOptions);
                return (settings ?? new AppSettings()).Normalize();
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                return new AppSettings();
            }
        }

        /// <summary>
        /// Writes to a temporary file first and then replaces the old one, so a crash mid-write leaves the
        /// previous settings intact.
        /// </summary>
        /// <exception cref="IOException">The folder or file can't be written.</exception>
        /// <exception cref="UnauthorizedAccessException">Access to the folder or file is denied.</exception>
        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            var tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(settings.Normalize(), JsonOptions));
            File.Move(tempPath, FilePath, overwrite: true);
        }
    }
}
